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

	private static bool Keyword(CGameState state, CTrigger trigger, CEventScope scope, CScriptReport? report)
	{
		string keyword = trigger.Keyword;
		switch (keyword)
		{
			case "and":
				return All(state, trigger, scope, report);
			case "or":
				foreach (CTrigger child in trigger.Children)
				{
					if (Holds(state, child, scope, report))
					{
						return true;
					}
				}
				return false;
			case "not":
				// A NOR: false at the first that holds.
				foreach (CTrigger child in trigger.Children)
				{
					if (Holds(state, child, scope, report))
					{
						return false;
					}
				}
				return true;
			case "any_owned_province":
				return CScopeSets.OwnedProvince(state, trigger, scope, report);
			case "any_neighbor_province":
				return CScopeSets.NeighbourProvince(state, trigger, scope, report);
			case "any_neighbor_country":
				return CScopeSets.NeighbourCountry(state, trigger, scope, report);
			case "any_core":
				return CScopeSets.Core(state, trigger, scope, report);
			case "enemy":
				// The other side of a battle, and there is none.
				return false;
		}
		if (CUnansweredConditions.Reason(keyword) is { } why)
		{
			report?.Add(trigger.Key, why);
			return false;
		}
		if (CClockConditions.TryAnswer(state, trigger, scope, out bool answer)
			|| CMarkConditions.TryAnswer(state, trigger, scope, out answer)
			|| CCountryConditions.TryAnswer(state, trigger, scope, out answer)
			|| CPoliticsConditions.TryAnswer(state, trigger, scope, out answer)
			|| CEspionageConditions.TryAnswer(state, trigger, scope, out answer)
			|| CRelationConditions.TryAnswer(state, trigger, scope, out answer)
			|| CProvinceConditions.TryAnswer(state, trigger, scope, out answer))
		{
			return answer;
		}
		throw new InvalidOperationException($"'{trigger.Key}' is no condition anything answers");
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
