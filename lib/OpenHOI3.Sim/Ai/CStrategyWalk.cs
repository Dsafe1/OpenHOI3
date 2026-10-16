using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The walk of a faction's chosen victory conditions: which provinces and countries they argue the strategy should take,
/// antagonise or court, and how strongly. See <c>docs/mechanics/aistrategy.md</c>, section 4.
/// </summary>
public static class CStrategyWalk
{
	/// <summary>What every chosen condition is walked with.</summary>
	public const int ConditionWeight = 200;

	/// <summary>Every condition chosen for the country's faction, in order, walked in a scope of the country alone.</summary>
	public static void Walk(CGameState state, CCountry country, CAiStrategy strategy)
	{
		if (country.Faction.Length == 0)
		{
			return;
		}
		foreach ((string faction, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			if (faction != country.Faction)
			{
				continue;
			}
			foreach (CVictoryCondition condition in conditions)
			{
				if (condition.Chosen)
				{
					Walk(new CWalker(state, country, strategy), condition.Trigger, CEventScope.Start(country.Tag), false,
						ConditionWeight);
				}
			}
			return;
		}
	}

	/// <summary>The walking country, its strategy and the game.</summary>
	private sealed record CWalker(CGameState State, CCountry Country, CAiStrategy Strategy);

	/// <summary>
	/// One condition: a block gives its weight to each child, <c>or</c> shares it among them, <c>not</c> turns what follows
	/// round, a scope walks its children where it resolves and a set of them once for each member; the leaves report.
	/// </summary>
	private static void Walk(CWalker walker, CTrigger trigger, CEventScope scope, bool negated, int weight)
	{
		CGameState state = walker.State;
		switch (trigger.Kind)
		{
			case ETriggerKind.Scope:
				if (CScopeSwitch.Narrow(state, scope, trigger.Argument, null) is { } narrowed)
				{
					Children(walker, trigger, narrowed, negated, weight);
				}
				return;
			case ETriggerKind.Region:
				foreach (Provinces.CRegion region in state.Regions)
				{
					if (region.Name.Equals(trigger.Key, StringComparison.Ordinal))
					{
						foreach (int province in region.Provinces)
						{
							Children(walker, trigger, scope.With(scope.Country, province), negated, weight);
						}
						return;
					}
				}
				return;
			case ETriggerKind.Building:
			case ETriggerKind.UnitType:
				// They fill the strategy's buildings and brigades to aim for, which are the military AI's and not kept.
				if (!negated)
				{
					state.ScriptReport?.Add(trigger.Key, EScriptGap.Unbuilt);
				}
				return;
			case ETriggerKind.Keyword:
				Keyword(walker, trigger, scope, negated, weight);
				return;
		}
	}

	private static void Keyword(CWalker walker, CTrigger trigger, CEventScope scope, bool negated, int weight)
	{
		CGameState state = walker.State;
		CCountry country = walker.Country;
		CAiStrategy strategy = walker.Strategy;
		switch (trigger.Keyword)
		{
			case "and":
				Children(walker, trigger, scope, negated, weight);
				return;
			case "or":
				Children(walker, trigger, scope, negated, Math.Max(weight / Math.Max(trigger.Children.Count, 1), 1));
				return;
			case "not":
				Children(walker, trigger, scope, !negated, weight);
				return;
			case "any_owned_province":
				foreach (CProvince province in state.Provinces)
				{
					if (scope.Country is { } owner && province.Owner == owner)
					{
						Children(walker, trigger, scope.With(scope.Country, province.Id), negated, weight);
					}
				}
				return;
			case "any_neighbor_province":
				foreach (CProvinceEdge edge in CConditions.Here(state, scope).Edges)
				{
					if (state.Province(edge.To).Owner is not null)
					{
						Children(walker, trigger, scope.With(scope.Country, edge.To), negated, weight);
					}
				}
				return;
			case "any_neighbor_country":
				if (state.Country(scope.Country) is { } beside)
				{
					foreach (CCountry neighbour in CCountryLists.Neighbours(state, beside))
					{
						Children(walker, trigger, scope.With(neighbour.Tag, scope.Province), negated, weight);
					}
				}
				return;
			case "any_core":
				foreach (CCountryTag core in CConditions.Here(state, scope).Cores.ToArray())
				{
					Children(walker, trigger, scope.With(core, scope.Province), negated, weight);
				}
				return;
			case "faction":
				Faction(walker, trigger, scope, negated, weight);
				return;
			case "controls":
			case "owns":
				if (!negated && scope.Country == country.Tag && !CConditions.Holds(state, trigger, scope))
				{
					int province = trigger.Argument.IsThis || trigger.Argument.IsFrom ? 0 : (int)CConditions.WholeOf(trigger.Argument.Amount);
					strategy.ConquerProvinces.Add(province, weight, CStrategyProvinces.ConquerCap(strategy));
				}
				return;
			case "owned_by":
				OwnedBy(walker, trigger, scope, negated, weight);
				return;
			case "exists":
				// Only turned round, and where the country named exists: antagonised.
				if (negated && CConditions.Holds(state, trigger, scope) && CEventScope.Known(trigger.Argument.Tag) is { } named)
				{
					CStrategyFigures.Add(country, strategy.Antagonize, named, weight, CStrategyFigures.AntagonizeCap);
				}
				return;
			case "war_with":
			case "undeclared_war_with":
				// Not turned round, the scope or the tag the walking country: the other antagonised, whether it holds or not.
				if (!negated && Other(walker, scope, CConditions.TagByOwner(state, trigger, scope)) is { } enemy)
				{
					CStrategyFigures.Add(country, strategy.Antagonize, enemy, weight, CStrategyFigures.AntagonizeCap);
				}
				return;
			case "alliance_with":
				// The other courted and not antagonised, the other way round where turned round; whether it holds or not.
				if (Other(walker, scope, CConditions.TagByOwner(state, trigger, scope)) is { } ally)
				{
					int signed = negated ? -weight : weight;
					CStrategyFigures.Add(country, strategy.Befriend, ally, signed, CStrategyFigures.FriendCap(strategy));
					CStrategyFigures.Add(country, strategy.Antagonize, ally, -signed, CStrategyFigures.AntagonizeCap);
				}
				return;
			case "relation":
				if (Other(walker, scope, CRelationConditions.RelationTarget(state, trigger, scope)) is { } felt)
				{
					if (negated)
					{
						CStrategyFigures.Add(country, strategy.Antagonize, felt, weight, CStrategyFigures.AntagonizeCap);
					}
					else
					{
						CStrategyFigures.Add(country, strategy.Befriend, felt, weight, CStrategyFigures.FriendCap(strategy));
					}
				}
				return;
			case "vassal_of":
				VassalOf(walker, trigger, scope, negated, weight);
				return;
		}
	}

	private static void Children(CWalker walker, CTrigger trigger, CEventScope scope, bool negated, int weight)
	{
		foreach (CTrigger child in trigger.Children)
		{
			Walk(walker, child, scope, negated, weight);
		}
	}

	/// <summary>Where the scope or the tag is the walking country, the other of the two; otherwise none.</summary>
	private static CCountryTag? Other(CWalker walker, CEventScope scope, CCountryTag? tag)
	{
		CCountryTag self = walker.Country.Tag;
		if (tag == self)
		{
			return scope.Country;
		}
		return scope.Country == self ? tag : null;
	}

	/// <summary>
	/// <c>faction</c>: where the walking country is in the faction named and the condition does not hold, the scope's
	/// province - or THIS's - is to be taken, or where it has none, its country antagonised.
	/// </summary>
	private static void Faction(CWalker walker, CTrigger trigger, CEventScope scope, bool negated, int weight)
	{
		CCountry country = walker.Country;
		if (negated || country.Faction.Length == 0 || trigger.Argument.Text != country.Faction
			|| CConditions.Holds(walker.State, trigger, scope))
		{
			return;
		}
		int province = scope.Province != 0 ? scope.Province : scope.Root.Province;
		if (province != 0)
		{
			walker.Strategy.ConquerProvinces.Add(province, weight, CStrategyProvinces.ConquerCap(walker.Strategy));
		}
		else if (scope.Country is { } other)
		{
			CStrategyFigures.Add(country, walker.Strategy.Antagonize, other, weight, CStrategyFigures.AntagonizeCap);
		}
	}

	/// <summary>
	/// <c>owned_by</c>, in a province: not turned round, where the tag is the walking country and the condition does not
	/// hold; turned round, where the tag is another and it holds - either way the province is to be taken.
	/// </summary>
	private static void OwnedBy(CWalker walker, CTrigger trigger, CEventScope scope, bool negated, int weight)
	{
		if (scope.Province == 0)
		{
			return;
		}
		bool ours = CConditions.TagByOwner(walker.State, trigger, scope) == walker.Country.Tag;
		bool holds = CConditions.Holds(walker.State, trigger, scope);
		if (negated ? !ours && holds : ours && !holds)
		{
			walker.Strategy.ConquerProvinces.Add(scope.Province, weight, CStrategyProvinces.ConquerCap(walker.Strategy));
		}
	}

	/// <summary>
	/// <c>vassal_of</c> the walking country, not turned round and not yet so: the scope's country wanted as a puppet, and
	/// antagonised and not courted - courted and not antagonised by a diplomat.
	/// </summary>
	private static void VassalOf(CWalker walker, CTrigger trigger, CEventScope scope, bool negated, int weight)
	{
		CAiStrategy strategy = walker.Strategy;
		if (negated || CConditions.TagByOwner(walker.State, trigger, scope) != walker.Country.Tag
			|| scope.Country is not { } subject || CConditions.Holds(walker.State, trigger, scope))
		{
			return;
		}
		CCountry country = walker.Country;
		int signed = strategy.Personality == EAiPersonality.Diplomacy ? -weight : weight;
		CStrategyFigures.Add(country, strategy.Vassal, subject, weight, CStrategyFigures.VassalCap);
		CStrategyFigures.Add(country, strategy.Antagonize, subject, signed, CStrategyFigures.AntagonizeCap);
		CStrategyFigures.Add(country, strategy.Befriend, subject, -signed, CStrategyFigures.FriendCap(strategy));
	}
}
