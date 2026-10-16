using System;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The foreign minister of every country nobody plays: once a day, in an hour of its country's own, the install's Lua
/// <c>ForeignMinister_Tick</c> - its decisions, calls to arms, transit rights, wars, peace, influence and alignment - then
/// the best offer it proposed, the pruning and the walk of its prepared wars, which posts a ready war or its decision;
/// and every new pair at war told to it. See <c>docs/mechanics/foreignminister.md</c>.
/// </summary>
public static class CForeignAi
{
	/// <summary>The foreign minister's place in the day: its country's number plus thirteen.</summary>
	private const int HourOffset = 13;

	/// <summary>A proposal is posted at a score above this.</summary>
	private const double ProposalThreshold = 50.0;

	/// <summary>A proposed war is replaced only by one scoring more than this above it.</summary>
	private const double WarProposalMargin = 40.0;

	/// <summary>
	/// The foreign minister of every country nobody plays: a draw of one to three hours on the game's generator, made
	/// before the production and tech ministers', as the original makes it first, and its first work to be skipped.
	/// </summary>
	public static void SetUpAtStart(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			bool played = state.Played.Contains(country.Tag);
			country.ForeignCountdown = played ? 0 : state.Random.Next() % 3 + 1;
			country.ForeignMinister.SkipOnce = !played;
		}
		// The original's AI, when it starts, tells its ministers of every war already being fought.
		foreach (CWar war in state.Wars)
		{
			foreach (CCountryTag attacker in war.Attackers)
			{
				foreach (CCountryTag defender in war.Defenders)
				{
					Tell(state, attacker, defender, war);
				}
			}
		}
	}

	/// <summary>
	/// The hour's work of every foreign minister, in the countries' order, counting down to its first work as the other
	/// ministers do - that first work only clearing its skip - and doing anything only in its hour of the day,
	/// <c>(number + 13) mod 24</c>.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		foreach (CCountry country in state.Countries)
		{
			if (!Research.CResearchAi.Works(state, country, country.ForeignCountdown))
			{
				continue;
			}
			if (--country.ForeignCountdown > 0)
			{
				continue;
			}
			country.ForeignCountdown = 1;
			if (country.ForeignMinister.SkipOnce)
			{
				country.ForeignMinister.SkipOnce = false;
				continue;
			}
			if (Research.CResearchAi.Hour(country, HourOffset) == hour)
			{
				RunDay(state, country);
			}
		}
	}

	/// <summary>
	/// The minister's day: the Lua's <c>ForeignMinister_Tick</c>, then the best offer it proposed where it scored above
	/// 50, the pruning for a great country, and the walk of its prepared wars.
	/// </summary>
	public static void RunDay(CGameState state, CCountry country)
	{
		state.AiScripts?.RunForeign(state, country);
		PostProposal(state, country);
		if (CWarTargets.Prunes(country))
		{
			CWarTargets.Prune(state, country);
		}
		WalkWarTargets(state, country);
	}

	/// <summary>
	/// The decisions pass, <c>minister:ExecuteDiploDecisions()</c>: one scope with one draw for its state, every
	/// diplomatic decision whose <c>potential</c> and <c>allow</c> hold in it put to
	/// <c>ForeignMinister_EvaluateDecision</c>, and each answered above nought posted. A failed call ends the pass and is
	/// answered, for the script calling this to fail with.
	/// </summary>
	public static string? ExecuteDiploDecisions(CGameState state, CCountry country)
	{
		if (state.AiScripts is not { } scripts)
		{
			return null;
		}
		CEventScope scope = CEventScope.Start(country.Tag, randomState: (uint)state.Random.Next());
		foreach (CDecision decision in state.Decisions)
		{
			if (decision.Group != EDecisionGroup.Diplomatic
				|| !CConditions.Holds(state, decision.Potential, scope) || !CConditions.Holds(state, decision.Allow, scope))
			{
				continue;
			}
			if (scripts.EvaluateDecision(state, country, decision, scope, out string? error) is not { } score)
			{
				return error ?? $"ForeignMinister_EvaluateDecision failed for {decision.Name}";
			}
			if (score > 0)
			{
				state.Outbox.Add(new CExecuteDecisionCommand(decision.Name, scope.Detached()));
			}
		}
		return null;
	}

	/// <summary>
	/// <c>minister:Propose(action, score)</c>: the offer kept as the day's proposal where it scores strictly above the one
	/// kept and may be pressed; otherwise nothing.
	/// </summary>
	public static void Propose(CGameState state, CCountry country, COffer offer, float score)
	{
		CForeignMinister minister = country.ForeignMinister;
		if (minister.ProposalScore < score && COfferRules.IsSelectable(state, offer))
		{
			minister.Proposal = offer;
			minister.ProposalScore = score;
		}
	}

	/// <summary>
	/// The kept proposal posted where it scored above 50, and its score set to -1; one at 50 or under stays kept.
	/// </summary>
	public static void PostProposal(CGameState state, CCountry country)
	{
		CForeignMinister minister = country.ForeignMinister;
		if (minister.Proposal is { } offer && minister.ProposalScore > ProposalThreshold)
		{
			state.Outbox.Add(new COfferCommand(offer));
			minister.Proposal = null;
			minister.ProposalScore = -1f;
		}
	}

	/// <summary><c>minister:ProposeWar(tag, score)</c>: kept where it scores more than 40 above the war kept.</summary>
	public static void ProposeWar(CCountry country, CCountryTag target, float score)
	{
		CForeignMinister minister = country.ForeignMinister;
		if (score > minister.ProposedWarScore + WarProposalMargin)
		{
			minister.ProposedWar = target;
			minister.ProposedWarScore = score;
		}
	}

	/// <summary><c>minister:ClearWarProposal()</c>: no war proposed, its score 0.</summary>
	public static void ClearWarProposal(CCountry country)
	{
		country.ForeignMinister.ProposedWar = null;
		country.ForeignMinister.ProposedWarScore = 0f;
	}

	/// <summary>
	/// <c>minister:PercOccupied(tag)</c>: of the provinces a country owns, the hundredths it does not control, to the
	/// nearest by way of single precision; 0 where it owns none.
	/// </summary>
	public static int PercOccupied(CGameState state, CCountryTag tag)
	{
		int owned = 0;
		int occupied = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == tag)
			{
				owned++;
				if (province.Controller != tag)
				{
					occupied++;
				}
			}
		}
		if (owned == 0)
		{
			return 0;
		}
		return (int)Math.Round((float)((double)occupied * 100.0 / owned), MidpointRounding.ToEven);
	}

	/// <summary>
	/// A new pair at war - a joiner and a country of the other side - told to every foreign minister, by
	/// <c>ForeignMinister_OnWar</c>. Nothing while a game is being made: the original's AI hears of those wars when it
	/// starts.
	/// </summary>
	public static void TellWar(CGameState state, CCountryTag joiner, CCountryTag enemy, CWar war)
	{
		if (!state.IsBeingMade)
		{
			Tell(state, joiner, enemy, war);
		}
	}

	private static void Tell(CGameState state, CCountryTag joiner, CCountryTag enemy, CWar war)
	{
		if (state.AiScripts is not { } scripts)
		{
			return;
		}
		foreach (CCountry country in state.Countries)
		{
			if (country.ForeignCountdown > 0)
			{
				scripts.ForeignOnWar(state, country, joiner, enemy, war);
			}
		}
	}

	/// <summary>
	/// The walk of the prepared wars. First, in order: transit rights held into a target are given up and end the day; a
	/// neighbour is put to the test, a ready one going on to the second walk at once and an unready one ending the day
	/// once the walk is done. Then every target again: a ready one with a decision has it posted, asked in a fresh scope,
	/// and one without is declared.
	/// </summary>
	public static void WalkWarTargets(CGameState state, CCountry country)
	{
		if (country.WarTargets.Count == 0)
		{
			return;
		}
		CCountryFacts facts = CCountryFacts.Of(state);
		bool unready = false;
		bool ready = false;
		foreach (CWarTarget entry in country.WarTargets)
		{
			if (state.Country(entry.Target) is { } other && CGameState.Status(country, other).HasMilitaryAccess)
			{
				state.Outbox.Add(new COfferCommand(new COffer(EOfferKind.AskMilitaryAccess, country.Tag, entry.Target,
					EOfferStage.Proposed, false, state.TotalHours)));
				return;
			}
			if (facts.AreNeighbours(country.Tag, entry.Target))
			{
				if (IsReady(state, country, entry.Target))
				{
					ready = true;
					break;
				}
				unready = true;
			}
		}
		if (unready && !ready)
		{
			return;
		}
		foreach (CWarTarget entry in country.WarTargets.ToArray())
		{
			if (!IsReady(state, country, entry.Target))
			{
				continue;
			}
			if (entry.Decision is { } decision)
			{
				CEventScope scope = CEventScope.Start(country.Tag, randomState: (uint)state.Random.Next());
				if (CConditions.Holds(state, decision.Potential, scope) && CConditions.Holds(state, decision.Allow, scope))
				{
					state.Outbox.Add(new CExecuteDecisionCommand(decision.Name, scope.Detached()));
				}
			}
			else
			{
				Declare(state, country, entry);
			}
		}
	}

	/// <summary>
	/// A ready war with no decision declared, where its row may be pressed: limited where it was prepared so or the
	/// country's faction has the rule; the goal <c>conquer</c>, or for three pairs a goal of their own where its
	/// <c>is_valid</c> holds.
	/// </summary>
	private static void Declare(CGameState state, CCountry country, CWarTarget entry)
	{
		if (state.Country(entry.Target) is not { } target || !CDeclarations.IsSelectable(state, country, target))
		{
			return;
		}
		bool limited = entry.Limited || CFactions.HasRule(state, country, CFactions.LimitedWarRule);
		CWarGoal goal = Goal(state, "conquer", country.Tag, target.Tag);
		if (SpecialGoal(country.Tag, target.Tag) is { } special)
		{
			CWarGoal own = Goal(state, special, country.Tag, target.Tag);
			if (own.Type.IsReal && CCasusBellis.IsValid(state, country.Tag, target.Tag, own.Type))
			{
				goal = own;
			}
		}
		state.Outbox.Add(new COfferCommand(new COffer(EOfferKind.DeclareWar, country.Tag, target.Tag,
			EOfferStage.Proposed, !limited, state.TotalHours) { WarGoal = goal }));
	}

	private static CWarGoal Goal(CGameState state, string type, CCountryTag actor, CCountryTag recipient) =>
		new(CCasusBellis.Find(state, type), actor, recipient);

	/// <summary>The three pairs the original gives a war goal of their own by their tags.</summary>
	private static string? SpecialGoal(CCountryTag actor, CCountryTag recipient) => (actor.Value, recipient.Value) switch
	{
		("GER", "FRA") => "vichy_war_goal",
		("GER", "SOV") => "barbarossa_war_goal",
		("JAP", "USA") => "japan_USA_goal",
		_ => null,
	};

	/// <summary>
	/// Whether a prepared war is ready, as far as is built: where no area the country holds touches an area the target
	/// holds, every count of the original's test is nought and it answers ready; otherwise it counts the fronts its
	/// theatre layer draws, which are not built, and here it is not (foreignminister.md, section 6).
	/// </summary>
	public static bool IsReady(CGameState state, CCountry country, CCountryTag target)
	{
		foreach (CSupplyArea area in CSupplyNetworks.Refresh(state).Areas)
		{
			if (area.Controller != country.Tag)
			{
				continue;
			}
			foreach (CSupplyArea beside in area.Neighbours)
			{
				if (beside.Controller == target)
				{
					return false;
				}
			}
		}
		return true;
	}
}
