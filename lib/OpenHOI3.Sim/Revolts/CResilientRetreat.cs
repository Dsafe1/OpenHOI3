using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// The original's <c>RebelFaction_ResilientRetreat</c>: a resilient faction's armies break off a battle against a
/// country they count a friend, and a smart type's also one whose odds are too long. No type of Their Finest Hour or
/// BlackICE is resilient. See <c>docs/mechanics/rebels.md</c>, section 11.
/// </summary>
public static class CResilientRetreat
{
	private const long PerRebelBrigade = 1000;
	private const long PerOtherBrigade = -1000;
	private const long HeldByRebels = 4000;
	private const long HeldByFriend = -10000;
	private const long Floor = -1000;

	/// <summary>Under this many of their brigades to one of ours, in thousandths, nothing is weighed.</summary>
	private const long LeastRatio = 750;

	/// <summary>What our power counts as where it is nought.</summary>
	private const long LeastPower = 10;

	/// <summary>Their power over ours, times the ratio, above which the rebels fall back: five to one.</summary>
	private const long TooLong = 5000;

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
				if (!FightsAFriend(state, faction, combat.Other(ours))
					&& !(faction.Type.Smart && OddsAreTooLong(state, combat, ours)))
				{
					continue;
				}
				Retreat(state, faction, combat, ours);
				break;
			}
		}
	}

	/// <summary>
	/// A smart type's weighing of a battle. Units marching into its province from a land neighbour join the sides - the
	/// rebels' to ours, everybody else's to theirs. Their brigades over ours, ours at least one, land units alone: under
	/// three to four nothing is weighed. Then each side's power together, ours ten where nought; the side whose dice are
	/// the higher has its power raised - theirs by an eighteenth a point of the difference, ours by one in four and a
	/// half - and their power over ours, times the ratio, above five is too long.
	/// </summary>
	private static bool OddsAreTooLong(CGameState state, CCombat combat, CCombatSide ours)
	{
		CCombatSide them = combat.Other(ours);
		var our = new List<CUnit>(ours.Units);
		var their = new List<CUnit>(them.Units);
		foreach (CProvinceEdge edge in state.Province(combat.Province).Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (edge.Kind == EProvinceEdgeKind.Impassable || beside.IsSea)
			{
				continue;
			}
			foreach (CUnit marching in state.UnitsIn(beside.Id))
			{
				if (marching.Path.Count > 0 && marching.Path[^1] == combat.Province
					&& !our.Contains(marching) && !their.Contains(marching))
				{
					(CRebelFactions.IsRebels(marching.Owner) ? our : their).Add(marching);
				}
			}
		}
		long ourBrigades = System.Math.Max(1, Brigades(our));
		long ratio = Brigades(their) * 1000 / ourBrigades;
		if (ratio < LeastRatio)
		{
			return false;
		}
		long ourPower = Power(our);
		long theirPower = Power(their);
		if (ourPower == 0)
		{
			ourPower = LeastPower;
		}
		long dice = them.Dice - ours.Dice;
		if (dice > 0)
		{
			theirPower = theirPower * (1000 + dice * 1000 / 18) / 1000;
		}
		else if (dice < 0)
		{
			ourPower = ourPower * (1000 + -dice * 1000 * 1000 / 4500) / 1000;
		}
		return theirPower * 1000 / ourPower * ratio / 1000 > TooLong;
	}

	private static long Brigades(List<CUnit> units)
	{
		long brigades = 0;
		foreach (CUnit unit in units)
		{
			if (CMovementRules.IsLandUnit(unit))
			{
				brigades += unit.SubUnits.Count;
			}
		}
		return brigades;
	}

	private static long Power(List<CUnit> units)
	{
		long power = 0;
		foreach (CUnit unit in units)
		{
			power += Agents.CUnitPower.Of(unit);
		}
		return power;
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
		// The faction's armies still marching into the battle turn back.
		foreach (int id in faction.Armies)
		{
			if (CRebelFactions.FindUnit(state, id) is { IsRetreating: false } marching && marching.Location != battle.Id
				&& marching.Path.Count > 0 && marching.Path[^1] == battle.Id)
			{
				CMovement.Cancel(marching);
			}
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
