using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Two figures the AI ranks its divisions by: how much of a garrison one is, 1 to 100 in thousandths; and how it fights
/// on a province's ground, its own terrain figures. The original's slots 88 and 89,
/// <c>CAIUnit::RateUnitAsGarrison</c> and <c>CAIUnit::GetUnitTerrainModifier</c>. See <c>docs/mechanics/moving.md</c>,
/// section 11.3.
/// </summary>
public static class CUnitRatings
{
	private const long Whole = 1000;

	public static long Garrison(CUnit unit)
	{
		int n = unit.SubUnits.Count;
		if (n < 1)
		{
			return 0;
		}
		long fill = 0;
		long score = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			fill += CFleetRegroup.StrengthShare(brigade);
			score += brigade.Type.Key switch
			{
				"police_brigade" or "garrison_brigade" => 100,
				"militia_brigade" => 90,
				"paratrooper_brigade" or "marine_brigade" => 75,
				"infantry_brigade" => 20,
				"bergsjaeger_brigade" => 10,
				_ => 1,
			};
		}
		long rating = (score * Whole / n + (2 - n) * 20 * Whole) * (2 * Whole - fill / n) / Whole;
		return System.Math.Clamp(rating, Whole, 100 * Whole);
	}

	/// <summary>
	/// How fit a division is to land on an enemy's coast, 0 to 100 in thousandths: ours, its mean strength share by a
	/// hundred - the original's rating weighs it by type as well, which was not read (<c>moving.md</c>, section 18).
	/// </summary>
	public static long Fitness(CGameState state, CUnit unit)
	{
		CUnitShares.Of(state, unit, out long strength, out _);
		return System.Math.Min(strength * 100, 100 * Whole);
	}

	/// <summary>
	/// Attacking, its attack and movement on the province's ground; otherwise its defence there, and the same attack and
	/// movement again where a land neighbour is held by a war candidate - the neighbour's own ground never read.
	/// </summary>
	public static long Terrain(CGameState state, CCountry country, CProvince province, CUnit unit, bool attacking)
	{
		if (province.Terrain is not { } terrain)
		{
			return 0;
		}
		long attack = Mean(unit, terrain.Name, "attack");
		long movement = Mean(unit, terrain.Name, "movement");
		if (attacking)
		{
			return attack + movement;
		}
		long figure = Mean(unit, terrain.Name, "defence");
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (!beside.IsSea && beside.Controller is { } holder && CFrontWorthy.IsWarCandidate(state, country, holder))
			{
				return figure + attack + movement;
			}
		}
		return figure;
	}

	private static long Mean(CUnit unit, string adjuster, string key)
	{
		if (unit.SubUnits.Count == 0)
		{
			return 0;
		}
		long sum = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			if (brigade.Stats.Adjusters.TryGetValue(adjuster, out CModifier? figures))
			{
				sum += figures[key].Raw;
			}
		}
		return sum / unit.SubUnits.Count;
	}
}
