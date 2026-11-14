using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Events;

/// <summary>
/// Which decisions a country is offered and may take, and finding one by name. Only the
/// diplomatic decisions are ever offered. See <c>docs/mechanics/events.md</c>, sections 9 and 12.
/// </summary>
public static class CDecisions
{
	/// <summary>
	/// The decisions offered to a country - the diplomatic ones whose <c>potential</c> holds - in the
	/// order the list shows them: those that may be taken first, in reverse file order, then those
	/// that may not, in file order.
	/// </summary>
	public static List<(CDecision Decision, bool MayTake)> Offered(CGameState state, CCountryTag country)
	{
		var taken = new List<(CDecision, bool)>();
		var refused = new List<(CDecision, bool)>();
		foreach (CDecision decision in state.Decisions)
		{
			if (decision.Group != EDecisionGroup.Diplomatic)
			{
				continue;
			}
			CEventScope scope = ScopeOf(country);
			if (!CConditions.Holds(state, decision.Potential, scope))
			{
				continue;
			}
			if (CConditions.Holds(state, decision.Allow, scope))
			{
				taken.Insert(0, (decision, true));
			}
			else
			{
				refused.Add((decision, false));
			}
		}
		taken.AddRange(refused);
		return taken;
	}

	/// <summary>The scope a country's decisions are asked and taken in: the country, nothing else, a seed of nought.</summary>
	public static CEventScope ScopeOf(CCountryTag country)
	{
		return CEventScope.Start(country, randomState: 0);
	}

	/// <summary>A decision by its name: the diplomatic ones first, then the political.</summary>
	public static CDecision? Find(CGameState state, string name)
	{
		return Find(state, name, EDecisionGroup.Diplomatic) ?? Find(state, name, EDecisionGroup.Political);
	}

	/// <summary>
	/// What a new game does with the decisions each country's history says it has taken (<c>decision = name</c>, less
	/// any <c>remove_decision</c>, by the start date): for each, in the history's order, the decision of that name - the
	/// political ones looked in first, then the diplomatic, the reverse of <see cref="Find(CGameState, string)"/> - and
	/// its effect run in the country's scope, one scope for all the country's decisions so that each draws on where the
	/// one before left its chance. Neither <c>potential</c> nor <c>allow</c> is asked, and nothing is told. Answers the
	/// names no decision has, each with its country, for the caller to report. See <c>docs/mechanics/events.md</c>,
	/// section 13.
	/// </summary>
	public static List<string> TakeHistoryAtNewGame(CGameState state)
	{
		var unknown = new List<string>();
		foreach (CCountry country in state.Countries)
		{
			if (country.HistoryDecisions.Count == 0)
			{
				continue;
			}
			CEventScope scope = ScopeOf(country.Tag);
			foreach (string name in country.HistoryDecisions)
			{
				if ((Find(state, name, EDecisionGroup.Political) ?? Find(state, name, EDecisionGroup.Diplomatic)) is { } decision)
				{
					CEffects.Run(state, decision.Effects, scope);
				}
				else
				{
					unknown.Add($"{country.Tag} has unknown decision '{name}'");
				}
			}
		}
		return unknown;
	}

	/// <summary>Whether it may be taken now: its state of chance set back to its seed, then <c>potential</c> and <c>allow</c>.</summary>
	public static bool MayTake(CGameState state, CDecision decision, CEventScope scope)
	{
		scope.ResetToSeed();
		return CConditions.Holds(state, decision.Potential, scope) && CConditions.Holds(state, decision.Allow, scope);
	}

	private static CDecision? Find(CGameState state, string name, EDecisionGroup group)
	{
		foreach (CDecision decision in state.Decisions)
		{
			if (decision.Group == group && decision.Name.Equals(name, StringComparison.Ordinal))
			{
				return decision;
			}
		}
		return null;
	}
}
