using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's held-back divisions put on the second ring behind its area, each ring province with room by its roads,
/// unless one holds a strong place on a border or stands where the enemy outnumbers the country's idle divisions. The
/// original's <c>CAIUnit_PlaceReserves</c>. See <c>docs/mechanics/moving.md</c>, section 7.5.
/// </summary>
public static class CReservePlacing
{
	private const long Whole = 1000;

	private const long Fit = 300;

	public static bool Place(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.HeldBack.Count == 0 || agent.SecondRing.Count == 0)
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var room = new Dictionary<int, int>();
		var have = new Dictionary<int, int>();
		foreach (int id in agent.SecondRing)
		{
			long infrastructure = CSupplyFigures.EffectiveInfrastructure(state.Province(id));
			room[id] = (int)((10 * infrastructure + Whole / 2) / Whole);
		}
		// Counted as a position's units are: a division standing in one and bound for another goes unseen in both.
		CPositionCount.Count(state, country, agent.HeldBack, agent.SecondRing, province => have[province] = have.GetValueOrDefault(province) + 1,
			_ => { }, divisionsOnly: false);
		bool moved = false;
		foreach (CUnit unit in agent.HeldBack)
		{
			// A division on its way is looked at: the busy test here is the order's alone, not the moving one's (FINDINGS-liveread.md, section 23).
			if (unit.IsRetreating || CCombats.IsHoldingGround(unit) || unit.Order is not (EUnitOrder.None or EUnitOrder.Move) || unit.IsGarrison
				|| unit.Location <= 0)
			{
				continue;
			}
			int bound = CAgentUnits.DestinationOrPlace(unit);
			if (room.TryGetValue(bound, out int roomHere) && have.GetValueOrDefault(bound) <= roomHere)
			{
				continue;
			}
			if (HoldsStrongPlace(state, country, unit) || Outnumbered(state, country, unit))
			{
				continue;
			}
			int hops = network.HopsOf(unit.Location);
			int best = 0;
			long least = long.MaxValue;
			foreach (int id in agent.SecondRing)
			{
				// Never where it is bound: over its room there, it is sent on, and back again the next pass if the other is too.
				if (id == bound)
				{
					continue;
				}
				long key = CMapDistance.Between(state.Province(unit.Location), state.Province(id), state.MapWidth)
					+ 20 * have.GetValueOrDefault(id) - 30 * (hops - network.HopsOf(id));
				if (key < least)
				{
					least = key;
					best = id;
				}
			}
			if (best > 0 && CAiMoves.Move(state, country, unit, best, safe: true))
			{
				moved = true;
				if (have.ContainsKey(bound))
				{
					have[bound]--;
				}
				have[best] = have.GetValueOrDefault(best) + 1;
			}
		}
		return moved;
	}

	/// <summary>Beside a war candidate across a river or in a fort, with strength and organisation over 0.3.</summary>
	private static bool HoldsStrongPlace(CGameState state, CCountry country, CUnit unit)
	{
		CProvince here = state.Province(unit.Location);
		bool fort = here.Modifier[CCombatModifiers.FortLevel].Raw >= Whole;
		foreach (CProvinceEdge edge in here.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea || beside.Controller is not { } holder || !CFrontWorthy.IsWarCandidate(state, country, holder))
			{
				continue;
			}
			if (fort || edge.Kind == EProvinceEdgeKind.River)
			{
				CUnitShares.Of(state, unit, out long strength, out long organisation);
				return strength > Fit && organisation > Fit;
			}
		}
		return false;
	}

	/// <summary>Beside a war candidate's units, fit, with fewer of the country's idle divisions there than half of those.</summary>
	private static bool Outnumbered(CGameState state, CCountry country, CUnit unit)
	{
		CProvince here = state.Province(unit.Location);
		int theirs = 0;
		foreach (CProvinceEdge edge in here.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea || beside.Controller is not { } holder || !CFrontWorthy.IsWarCandidate(state, country, holder))
			{
				continue;
			}
			foreach (CUnit other in state.Positions.Of(edge.To))
			{
				if (CTheatres.IsLand(other) && other.Owner == holder)
				{
					theirs++;
				}
			}
		}
		if (theirs == 0)
		{
			return false;
		}
		CUnitShares.Of(state, unit, out long strength, out long organisation);
		if (strength <= Fit || organisation <= Fit)
		{
			return false;
		}
		int ours = 0;
		foreach (CUnit other in state.Positions.Of(unit.Location))
		{
			if (!ReferenceEquals(other, unit) && other.Owner == country.Tag && other.Kind == EUnitKind.Division
				&& !CCommandLadder.IsBlocked(other))
			{
				ours++;
			}
		}
		return ours * 2 < theirs;
	}
}
