using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The AI's land units left in other countries' land it may not widen its fronts into, walked home: to the nearest of
/// its own provinces it can reach - a quarter of the way in its capital's area - else the nearest of a neighbouring
/// area it may use, else a division to its own area's nearest port, else the capital; with nowhere at all, disbanded.
/// The original's <c>CEU3AI_RecallStrandedLandUnits</c>. See <c>docs/mechanics/moving.md</c>, section 13.
/// </summary>
public static class CStrandedRecall
{
	private const long Whole = 1000;

	private const long Controlled = 10_000;

	public static void Run(CGameState state, CCountry country)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (!IsStranded(state, country, network, unit))
			{
				continue;
			}
			int where = HomeFor(state, country, network, unit);
			if (where > 0)
			{
				CAiMoves.Move(state, country, unit, where, safe: true);
			}
			else
			{
				state.Outbox.Add(new CDisbandCommand(unit.Id));
			}
		}
	}

	private static bool IsStranded(CGameState state, CCountry country, CSupplyNetwork network, CUnit unit)
	{
		if (unit.IsRemoved || !CTheatres.IsLand(unit) || unit.Location <= 0 || unit.Transport is not null || unit.Plane is not null
			|| unit.IsFighting || unit.IsRetreating)
		{
			return false;
		}
		CProvince here = state.Province(unit.Location);
		if (here.IsSea || here.Controller is not { } holder || holder == country.Tag || CCountryLists.IsEnemy(state, country, holder)
			|| MayStay(state, country, network.AreaOf(here.Id)))
		{
			return false;
		}
		int bound = CAgentUnits.Destination(unit);
		if (bound > 0 && bound != here.Id && network.AreaOf(bound) is { } area)
		{
			CProvince there = state.Province(bound);
			if (there.Controller is { } other && (other == country.Tag || CCountryLists.IsEnemy(state, country, other)
				|| MayStay(state, country, area)))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>An area the country may widen its fronts into (<see cref="CFrontSearch"/>).</summary>
	private static bool MayStay(CGameState state, CCountry country, CSupplyArea? area)
	{
		return area is not null && CFrontSearch.Allows(state, country, area, anyUnplayed: false, out _);
	}

	private static int HomeFor(CGameState state, CCountry country, CSupplyNetwork network, CUnit unit)
	{
		CProvince here = state.Province(unit.Location);
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		var ours = new List<(int Province, long Score)>();
		foreach (CSupplyArea area in network.Areas)
		{
			if (area.Controller != country.Tag)
			{
				continue;
			}
			foreach (int id in area.Provinces)
			{
				long distance = CMapDistance.Between(here, state.Province(id), state.MapWidth) * Whole;
				if (ReferenceEquals(area, capitalArea))
				{
					distance /= 4;
				}
				Insert(ours, id, distance);
			}
		}
		foreach ((int id, long _) in ours)
		{
			if (CRouteFinder.Find(state, country, unit, unit.Location, id) is not null)
			{
				return id;
			}
		}
		int best = 0;
		long least = long.MaxValue;
		if (network.AreaOf(here.Id) is { } own)
		{
			foreach (CSupplyArea beside in own.Neighbours)
			{
				if (!CApproaches.IsFriendly(state, country, beside.Controller) || CAreaHostility.TouchesHostileArea(state, beside))
				{
					continue;
				}
				foreach (int id in beside.Provinces)
				{
					long score = CMapDistance.Between(here, state.Province(id), state.MapWidth) * Whole
						- (state.Province(id).Controller == country.Tag ? Controlled * Whole : 0);
					if (score < least)
					{
						least = score;
						best = id;
					}
				}
			}
			if (best == 0 && unit.Kind == EUnitKind.Division && CTheatreFigures.NavalBase(state) is { } naval)
			{
				foreach (int id in own.Ports)
				{
					if (state.Province(id).BuildingMaxLevel(naval).Raw < Whole)
					{
						continue;
					}
					long score = CMapDistance.Between(here, state.Province(id), state.MapWidth);
					if (score < least)
					{
						least = score;
						best = id;
					}
				}
			}
		}
		return best > 0 ? best : country.ActingCapital;
	}

	private static void Insert(List<(int Province, long Score)> list, int province, long score)
	{
		int at = list.Count;
		while (at > 0 && list[at - 1].Score > score)
		{
			at--;
		}
		list.Insert(at, (province, score));
	}
}
