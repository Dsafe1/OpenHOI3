using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What an AI country's agent does each day about units standing on somebody else's land: a unit on loan goes home when
/// its lender may no longer operate there, and one of its own, on a friend's land outside its theatre, is lent to whoever
/// holds it. The original's <c>CAIUnit::ManageExpeditions</c>. See <c>docs/mechanics/agents.md</c>, section 10.5.
/// </summary>
public static class CAiExpeditions
{
	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		if (!country.UnitAi.RunsUnits)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CUnit unit in new List<CUnit>(agent.Land))
		{
			if (unit.Location <= 0 || network.AreaOf(unit.Location) is not { } area)
			{
				continue;
			}
			if (unit.Lender is { } lender && state.Country(lender) is { InExile: false } owner)
			{
				GiveBack(state, country, agent, unit, owner, area);
			}
			else
			{
				Lend(state, country, agent, unit, area);
			}
		}
	}

	private static void GiveBack(CGameState state, CCountry country, CAgent agent, CUnit unit, CCountry owner, CSupplyArea area)
	{
		if (CCommandLadder.IsBlocked(unit) || state.Played.Contains(owner.Tag) || MayOperateIn(state, owner, area))
		{
			return;
		}
		foreach (CUnit child in unit.Children)
		{
			state.Outbox.Add(new CDetachUnitCommand(country.Tag, child.Id));
		}
		agent.Land.Remove(unit);
		state.Outbox.Add(new CSendExpeditionCommand(country.Tag, unit.Id, owner.Tag));
	}

	private static void Lend(CGameState state, CCountry country, CAgent agent, CUnit unit, CSupplyArea area)
	{
		CProvince here = state.Province(unit.Location);
		if (state.Province(CAgentUnits.DestinationOrPlace(unit)).Controller == country.Tag || here.Controller == country.Tag
			|| here.Controller is not { } tag || state.Country(tag) is not { } holder)
		{
			return;
		}
		if (CCountryLists.IsEnemy(state, holder, country.Tag) || Movement.CMovementRules.IsEnemyIn(state, holder.Tag, country.Tag, here.Id)
			|| holder.Faction != country.Faction || unit.Children.Count > 0 || unit.Kind == EUnitKind.Theatre
			|| !MayOperateIn(state, country, area))
		{
			return;
		}
		if (state.Played.Contains(area.Controller) && !HoldsPriorityProvince(state, country, area))
		{
			return;
		}
		if (CAgents.TheatreOf(state, country, agent) is { } theatre && CTheatreSort.Covers(state, country, theatre, area))
		{
			return;
		}
		agent.Land.Remove(unit);
		state.Outbox.Add(new CSendExpeditionCommand(country.Tag, unit.Id, holder.Tag));
	}

	/// <summary>The original's <c>AreaMayWidenFrontSearch</c> as these two ask it, any unplayed country being enough.</summary>
	private static bool MayOperateIn(CGameState state, CCountry country, CSupplyArea area)
	{
		return CFrontSearch.Allows(state, country, area, anyUnplayed: true, out _);
	}

	private static bool HoldsPriorityProvince(CGameState state, CCountry country, CSupplyArea area)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (int id in country.UnitAi.PriorityProvinces)
		{
			if (ReferenceEquals(network.AreaOf(id), area))
			{
				return true;
			}
		}
		return false;
	}
}
