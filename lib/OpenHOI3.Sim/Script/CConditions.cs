using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// Whether a condition holds in a scope, as the original answers it: the blocks and the scopes
/// here, the leaves in the classes beside it. What the simulation does not hold yet answers false
/// and goes in the report. See <c>docs/mechanics/triggers.md</c>.
/// </summary>
public static class CConditions
{
	public static bool Holds(CGameState state, CTrigger trigger, CEventScope scope, CScriptReport? report = null)
	{
		// With none given, the game's own - which only a tool sets (CGameState.ScriptReport).
		report ??= state.ScriptReport;
		switch (trigger.Kind)
		{
			case ETriggerKind.Keyword:
				return Keyword(state, trigger, scope, report);
			case ETriggerKind.Scope:
				return CScopeSwitch.Narrow(state, scope, trigger.Argument, report) is { } narrowed
					&& All(state, trigger, narrowed, report);
			case ETriggerKind.Region:
				return CScopeSets.Region(state, trigger, scope, report);
			case ETriggerKind.Ideology:
				return CPoliticsConditions.IdeologyBlock(state, trigger, scope);
			case ETriggerKind.CombinedArms:
				// A battle's side is asked, and nothing here has one.
				return false;
			default:
				return CNamedConditions.Holds(state, trigger, scope);
		}
	}

	/// <summary>Whether every condition of the block holds: true for none.</summary>
	public static bool All(CGameState state, CTrigger block, CEventScope scope, CScriptReport? report)
	{
		foreach (CTrigger child in block.Children)
		{
			if (!Holds(state, child, scope, report))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>What answers a keyword: the blocks and scope sets here, none for one not built, else a class beside this.</summary>
	private const int Unknown = 0, And = 1, Or = 2, Not = 3, OwnedProvince = 4, NeighbourProvince = 5, NeighbourCountry = 6,
		Core = 7, Enemy = 8, Unanswered = 9, Clock = 10, Mark = 11, Country = 12, Politics = 13, Espionage = 14,
		Relation = 15, Province = 16, Revolt = 17, Battle = 18;

	private static bool Keyword(CGameState state, CTrigger trigger, CEventScope scope, CScriptReport? report)
	{
		// Which answers it is found the first time it is asked and kept on it (CTrigger.Answerer): the classes beside this
		// take a keyword or leave it by the keyword alone, so the chain gives the same one every time.
		int answerer = trigger.Answerer;
		if (answerer == Unknown)
		{
			return FirstTime(state, trigger, scope, report);
		}
		bool answer;
		switch (answerer)
		{
			case And:
				return All(state, trigger, scope, report);
			case Or:
				return AnyHolds(state, trigger, scope, report);
			case Not:
				return !AnyHolds(state, trigger, scope, report);
			case OwnedProvince:
				return CScopeSets.OwnedProvince(state, trigger, scope, report);
			case NeighbourProvince:
				return CScopeSets.NeighbourProvince(state, trigger, scope, report);
			case NeighbourCountry:
				return CScopeSets.NeighbourCountry(state, trigger, scope, report);
			case Core:
				return CScopeSets.Core(state, trigger, scope, report);
			case Enemy:
				// The other side of a battle, and there is none.
				return false;
			case Unanswered:
				report?.Add(trigger.Key, CUnansweredConditions.Reason(trigger.Keyword)!.Value);
				return false;
			case Clock:
				CClockConditions.TryAnswer(state, trigger, scope, out answer);
				return answer;
			case Mark:
				CMarkConditions.TryAnswer(state, trigger, scope, out answer);
				return answer;
			case Country:
				CCountryConditions.TryAnswer(state, trigger, scope, out answer);
				return answer;
			case Politics:
				CPoliticsConditions.TryAnswer(state, trigger, scope, out answer);
				return answer;
			case Espionage:
				CEspionageConditions.TryAnswer(state, trigger, scope, out answer);
				return answer;
			case Relation:
				CRelationConditions.TryAnswer(state, trigger, scope, out answer);
				return answer;
			case Province:
				CProvinceConditions.TryAnswer(state, trigger, scope, out answer);
				return answer;
			case Revolt:
				CRevoltConditions.TryAnswer(state, trigger, scope, out answer);
				return answer;
			default:
				// The last battle's figures: whether they answer hangs on whether there has been one, so they are asked
				// each time.
				if (CBattleConditions.TryAnswer(state, trigger, out answer))
				{
					return answer;
				}
				throw new InvalidOperationException($"'{trigger.Key}' is no condition anything answers");
		}
	}

	/// <summary>A keyword asked for the first time: the whole chain walked, and what answered it kept on it.</summary>
	private static bool FirstTime(CGameState state, CTrigger trigger, CEventScope scope, CScriptReport? report)
	{
		string keyword = trigger.Keyword;
		int block = keyword switch
		{
			"and" => And,
			"or" => Or,
			"not" => Not,
			"any_owned_province" => OwnedProvince,
			"any_neighbor_province" => NeighbourProvince,
			"any_neighbor_country" => NeighbourCountry,
			"any_core" => Core,
			"enemy" => Enemy,
			_ => CUnansweredConditions.Reason(keyword) is not null ? Unanswered : Unknown,
		};
		if (block != Unknown)
		{
			trigger.Answerer = block;
			return Keyword(state, trigger, scope, report);
		}
		bool answer;
		int found =
			CClockConditions.TryAnswer(state, trigger, scope, out answer) ? Clock
			: CMarkConditions.TryAnswer(state, trigger, scope, out answer) ? Mark
			: CCountryConditions.TryAnswer(state, trigger, scope, out answer) ? Country
			: CPoliticsConditions.TryAnswer(state, trigger, scope, out answer) ? Politics
			: CEspionageConditions.TryAnswer(state, trigger, scope, out answer) ? Espionage
			: CRelationConditions.TryAnswer(state, trigger, scope, out answer) ? Relation
			: CProvinceConditions.TryAnswer(state, trigger, scope, out answer) ? Province
			: CRevoltConditions.TryAnswer(state, trigger, scope, out answer) ? Revolt
			: Battle;
		trigger.Answerer = found;
		if (found != Battle)
		{
			return answer;
		}
		if (CBattleConditions.TryAnswer(state, trigger, out answer))
		{
			return answer;
		}
		throw new InvalidOperationException($"'{trigger.Key}' is no condition anything answers");
	}

	/// <summary>Whether any condition of the block holds, the first that does ending the walk: an <c>or</c>, and a <c>not</c> turned round.</summary>
	private static bool AnyHolds(CGameState state, CTrigger block, CEventScope scope, CScriptReport? report)
	{
		foreach (CTrigger child in block.Children)
		{
			if (Holds(state, child, scope, report))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// THIS for a leaf that reads it by owner: the root's country, or the owner of the root's
	/// province where the root has none.
	/// </summary>
	public static CCountryTag? ThisByOwner(CGameState state, CEventScope scope)
	{
		return scope.Root.Country ?? state.Province(scope.Root.Province).Owner;
	}

	/// <summary>The country a leaf's value names: THIS by owner, FROM, or the tag written.</summary>
	public static CCountryTag? TagByOwner(CGameState state, CTrigger trigger, CEventScope scope)
	{
		CScriptArgument value = trigger.Argument;
		return value.IsThis ? ThisByOwner(state, scope) : value.IsFrom ? scope.FromCountry : CEventScope.Known(value.Tag);
	}

	/// <summary>The country a leaf's value names, THIS read straight off the root.</summary>
	public static CCountryTag? TagDirect(CTrigger trigger, CEventScope scope)
	{
		CScriptArgument value = trigger.Argument;
		return value.IsThis ? scope.Root.Country : value.IsFrom ? scope.FromCountry : CEventScope.Known(value.Tag);
	}

	/// <summary>One of a block condition's own keys - <c>relation</c>'s <c>who</c> - or null where it was not written.</summary>
	public static CScriptArgument? Field(CTrigger trigger, string key)
	{
		foreach (CScriptField field in trigger.Fields)
		{
			if (field.Key == key)
			{
				return field.Argument;
			}
		}
		return null;
	}

	/// <summary>What a yes-or-no leaf answers: whether what it found is what was written.</summary>
	public static bool Is(CTrigger trigger, bool found)
	{
		return found == trigger.Argument.IsYes;
	}

	/// <summary>A whole number written as a number: its thousandths cut back to whole, toward nought.</summary>
	public static long WholeOf(CFixedPoint value)
	{
		return value.Raw / CFixedPoint.Scale;
	}

	internal static CProvince Here(CGameState state, CEventScope scope)
	{
		return state.Province(scope.Province);
	}
}
