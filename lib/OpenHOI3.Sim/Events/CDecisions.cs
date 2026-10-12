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
