using System.Linq;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Where the sealift sends and ships from: an area's port nearest the plan's objectives by sea, a landing province of an
/// enemy's area, the embark port nearest the troops bound for debark; and whether the sea between two areas is open at
/// war, the straits the defines name deciding. The original's <c>CAIUnit_PickPortNearestObjectives</c>,
/// <c>CAIUnit_PickLandingProvince</c>, <c>CAIUnit_PickEmbarkPort</c>, <c>AreaPair_SeaRouteOpen</c>,
/// <c>CCountry_IsSeaRouteOpen</c> and <c>SeaRouteCrossesClosedStrait</c>. See <c>docs/mechanics/moving.md</c>, section 15.
/// </summary>
public static class CSealiftPickers
{
	private const long Whole = 1000;

	private const long Unreached = 1_000_000;

	private const int MostEnemies = 5;

	private const long StraitOnly = 1000;

	private const long PerEnemy = 34;

	public const string GibraltarBlocker = "GIBRALTAR_BLOCKER";

	public const string SuezBlocker = "SUEZ_BLOCKER";

	public const string BlackSeaBlocker = "BLACKSEA_BLOCKER";

	/// <summary>Of the area's ports with a naval base, the one with the least mean distance by sea to the objectives (else the area of operations, else the headquarters).</summary>
	public static int PortNearObjectives(CGameState state, CAgent agent, CSupplyArea area)
	{
		if (CTheatreFigures.NavalBase(state) is not { } naval)
		{
			return 0;
		}
		List<int> aims = Aims(agent);
		int best = 0;
		long least = Unreached * Whole;
		foreach (int id in area.Provinces)
		{
			CProvince port = state.Province(id);
			if (port.PortSea <= 0 || port.BuildingMaxLevel(naval).Raw < Whole || aims.Count == 0)
			{
				continue;
			}
			long sum = 0;
			foreach (int aim in aims)
			{
				sum += System.Math.Max(0, CSeaTargets.Sea(state, id, aim));
			}
			long mean = sum / aims.Count;
			if (mean < least)
			{
				least = mean;
				best = id;
			}
		}
		return best;
	}

	private static List<int> Aims(CAgent agent)
	{
		var aims = new List<int>();
		if (agent.Unit is not { } unit)
		{
			return aims;
		}
		foreach (CObjective objective in unit.Plan.Objectives)
		{
			aims.Add(objective.Province);
		}
		if (aims.Count == 0)
		{
			aims.AddRange(agent.Area);
		}
		if (aims.Count == 0 && unit.Location > 0)
		{
			aims.Add(unit.Location);
		}
		return aims;
	}

	/// <summary>The province of an enemy's area to land on: near the objectives in it, a naval base and friends beside it, few enemies.</summary>
	public static int LandingProvince(CGameState state, CCountry country, CAgent agent, CSupplyArea area)
	{
		CBuilding? naval = CTheatreFigures.NavalBase(state);
		var objectives = new List<int>();
		if (agent.Unit is { } unit)
		{
			foreach (CObjective objective in unit.Plan.Objectives)
			{
				if (area.Provinces.Contains(objective.Province))
				{
					objectives.Add(objective.Province);
				}
			}
		}
		int best = 0;
		long least = Unreached * Whole;
		foreach (int id in area.Provinces)
		{
			CProvince p = state.Province(id);
			long level = naval is null ? 0 : p.BuildingMaxLevel(naval).Raw;
			int friends = 0;
			bool straitsOnly = true;
			foreach (CProvinceEdge edge in p.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (edge.Kind == EProvinceEdgeKind.Impassable || beside.IsSea || beside.Controller != p.Controller)
				{
					continue;
				}
				friends++;
				straitsOnly &= edge.Kind == EProvinceEdgeKind.Crossing;
			}
			long s = friends * level;
			if (!((p.PortSea > 0 && level >= Whole) || s >= Whole))
			{
				continue;
			}
			int enemies = 0;
			foreach (CUnit standing in state.Positions.Of(id))
			{
				if (standing.Kind is not (EUnitKind.Navy or EUnitKind.Air) && CCountryLists.IsEnemy(state, country, standing.Owner))
				{
					enemies++;
				}
			}
			if (enemies > MostEnemies)
			{
				continue;
			}
			long mean = 0;
			if (objectives.Count > 0)
			{
				long sum = 0;
				foreach (int aim in objectives)
				{
					sum += CMapDistance.Between(p, state.Province(aim), state.MapWidth) * Whole;
				}
				mean = sum / objectives.Count;
				if (objectives.Contains(id))
				{
					mean /= 10;
				}
			}
			long score = mean - level - s / 2 - 10 * friends * Whole + PerEnemy * enemies * Whole
				+ (straitsOnly && friends > 0 ? StraitOnly * Whole : 0);
			if (score < least)
			{
				least = score;
				best = id;
			}
		}
		return best;
	}

	/// <summary>Of the area's ports, the one with the least mean distance by sea to the troops bound for debark standing in it, with a way by sea to debark.</summary>
	public static int EmbarkPort(CGameState state, CAgent agent, List<CUnit> listed, CSupplyArea area, int debark)
	{
		var troops = new List<int>();
		foreach (CUnit unit in listed)
		{
			if (unit.SealiftTarget == debark && area.Provinces.Contains(unit.Location))
			{
				troops.Add(unit.Location);
			}
		}
		if (troops.Count == 0)
		{
			return 0;
		}
		int best = 0;
		long least = long.MaxValue;
		foreach (int port in area.Ports)
		{
			long sum = 0;
			foreach (int at in troops)
			{
				sum += System.Math.Max(0, CSeaTargets.Sea(state, port, at));
			}
			long mean = sum / troops.Count;
			if (mean < least && CSeaTargets.Sea(state, port, debark) >= 0)
			{
				least = mean;
				best = port;
			}
		}
		return best;
	}

	/// <summary>At peace, within one area, or with some pair of their ports between which the sea is open.</summary>
	public static bool SeaRouteOpen(CGameState state, CCountry country, CSupplyArea from, CSupplyArea to)
	{
		if (!state.IsAtWar(country.Tag) || ReferenceEquals(from, to))
		{
			return true;
		}
		foreach (int a in from.Ports)
		{
			foreach (int b in to.Ports)
			{
				if (IsSeaRouteOpen(state, country, a, b))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>True at peace; at war, unless the way crosses into or out of the Mediterranean with Gibraltar and Suez both held by enemies, or the Black Sea with its blocker held.</summary>
	public static bool IsSeaRouteOpen(CGameState state, CCountry country, int from, int to)
	{
		if (!state.IsAtWar(country.Tag))
		{
			return true;
		}
		int a = Sea(state, from);
		int b = Sea(state, to);
		bool inMed = InRegion(state, a, "mediterranean_region") || InRegion(state, a, "black_sea_region");
		bool toMed = InRegion(state, b, "mediterranean_region") || InRegion(state, b, "black_sea_region");
		if (inMed != toMed && HeldByEnemy(state, country, GibraltarBlocker) && HeldByEnemy(state, country, SuezBlocker))
		{
			return false;
		}
		bool inBlack = InRegion(state, a, "black_sea_region");
		bool toBlack = InRegion(state, b, "black_sea_region");
		return inBlack == toBlack || !HeldByEnemy(state, country, BlackSeaBlocker);
	}

	private static int Sea(CGameState state, int province)
	{
		CProvince p = state.Province(province);
		return p.IsSea || p.PortSea <= 0 ? province : p.PortSea;
	}

	private static bool InRegion(CGameState state, int province, string name)
	{
		foreach (CRegion region in state.Regions)
		{
			if (region.Name == name)
			{
				return region.Provinces.Contains(province);
			}
		}
		return false;
	}

	private static bool HeldByEnemy(CGameState state, CCountry country, string blocker)
	{
		if (!state.Defines.Has(CMovementRules.MapSection, blocker))
		{
			return false;
		}
		int id = (int)(state.Defines[CMovementRules.MapSection, blocker].Raw / Whole);
		return id > 0 && id < state.Provinces.Count && state.Province(id).Controller is { } holder
			&& CCountryLists.IsEnemy(state, country, holder);
	}
}
