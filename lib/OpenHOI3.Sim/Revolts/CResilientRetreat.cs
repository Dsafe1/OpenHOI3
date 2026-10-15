using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// The original's <c>RebelFaction_ResilientRetreat</c>: a resilient faction's armies break off a battle against a
/// country they count a friend. No type of Their Finest Hour or BlackICE is resilient. A smart type's weighing of the
/// odds, which asks a unit's power, is not built. See <c>docs/mechanics/rebels.md</c>, section 11.
/// </summary>
public static class CResilientRetreat
{
	private const long PerRebelBrigade = 1000;
	private const long PerOtherBrigade = -1000;
	private const long HeldByRebels = 4000;
	private const long HeldByFriend = -10000;
	private const long Floor = -1000;

	public static void Run(CGameState state, CRebelFaction faction)
	{
		if (!faction.Type.Resilient)
		{
			return;
		}
		var weighed = new HashSet<CUnit>();
		foreach (int id in new List<int>(faction.Armies))
		{
			if (CRebelFactions.FindUnit(state, id) is not { } unit || !unit.IsFighting || weighed.Contains(unit))
			{
				continue;
			}
			foreach (CCombat combat in new List<CCombat>(unit.Combats))
			{
				if (combat.SideOf(unit) is not { } ours)
				{
					continue;
				}
				foreach (CUnit fellow in ours.Units)
				{
					weighed.Add(fellow);
				}
				if (!FightsAFriend(state, faction, combat.Other(ours)))
				{
					continue;
				}
				Retreat(state, faction, combat, ours);
				break;
			}
		}
	}

	private static bool FightsAFriend(CGameState state, CRebelFaction faction, CCombatSide them)
	{
		foreach (CCountryTag country in them.Countries)
		{
			if (CRebelFactions.IsFriendly(state, faction, country))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// To the wanted land neighbour of the battle's province scoring highest above -1000: 1000 for each brigade of a
	/// rebel army there and -1000 for every other, 4000 where the rebels hold it, -10000 where a friend does; the first
	/// of equals. The faction's armies standing in the battle's province are sent one step there.
	/// </summary>
	private static void Retreat(CGameState state, CRebelFaction faction, CCombat combat, CCombatSide ours)
	{
		CProvince battle = state.Province(combat.Province);
		int chosen = 0;
		long best = Floor;
		foreach (CProvinceEdge edge in battle.Edges)
		{
			CProvince next = state.Province(edge.To);
			if (edge.Kind == EProvinceEdgeKind.Impassable || next.IsSea || !CRebelFactions.IsHostile(state, faction, next))
			{
				continue;
			}
			long score = 0;
			foreach (CUnit there in state.UnitsIn(next.Id))
			{
				score += there.SubUnits.Count * (CRebelFactions.IsRebels(there.Owner) ? PerRebelBrigade : PerOtherBrigade);
			}
			if (CRebelFactions.IsRebels(next.Controller))
			{
				score += HeldByRebels;
			}
			if (CRebelFactions.IsFriendly(state, faction, next.Controller))
			{
				score += HeldByFriend;
			}
			if (score > best)
			{
				best = score;
				chosen = next.Id;
			}
		}
		if (chosen == 0)
		{
			return;
		}
		foreach (CUnit unit in new List<CUnit>(ours.Units))
		{
			if (faction.Armies.Contains(unit.Id) && unit.Location == battle.Id)
			{
				CCombats.LeaveForMarch(state, unit);
				CMovement.SetPath(unit, new[] { chosen }, clear: true);
			}
		}
	}
}
