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

	public static long Garrison(CGameState state, CUnit unit)
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
			fill += CFleetRegroup.StrengthShare(state, brigade);
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
	/// The same rating in the original's own precision, as its garrison pick compares it: each brigade's strength over its
	/// most added as a single, the mean score, the mean fill and the product each rounded to a single, held between 1 and
	/// 100. Two divisions a thousandth of a share apart rate apart here, where in thousandths they tie
	/// (<c>reversing/findings/FINDINGS-liveread.md</c>, section 35).
	/// </summary>
	public static float GarrisonExact(CGameState state, CUnit unit)
	{
		int n = unit.SubUnits.Count;
		if (n < 1)
		{
			return 0;
		}
		float fill = 0;
		int score = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			// Its most as GetMaxStrength gives it, a reserve's less what its law keeps back (FINDINGS-liveread.md, section 38).
			long most = state.Country(unit.Owner) is { } owner ? CUnits.MaxStrength(state, owner, brigade).Raw : brigade.Stats[CSubUnitType.MaxStrength].Raw;
			float share = most <= 0 ? 1f : (float)(brigade.Strength.Raw / 1000.0 / (most / 1000.0));
			fill = (float)((double)fill + share);
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
		float mean = (float)((double)score / n);
		float meanFill = (float)((double)fill / n);
		float rating = (float)(((double)mean + (2 - n) * 20) * (2.0 - meanFill));
		return System.Math.Clamp(rating, 1f, 100f);
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
	///
	/// Under an enhanced game's fix <c>ai_terrain_neighbour</c> that second term is read on the neighbour's ground, which
	/// is the ground it would attack into: the best of such neighbours', and nothing for one with no terrain.
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
		bool theirGround = state.Rules.Has(Rules.EFix.AiTerrainNeighbour);
		long? best = null;
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (!beside.IsSea && beside.Controller is { } holder && CFrontWorthy.IsWarCandidate(state, country, holder))
			{
				if (!theirGround)
				{
					return figure + attack + movement;
				}
				long into = beside.Terrain is { } ground ? Mean(unit, ground.Name, "attack") + Mean(unit, ground.Name, "movement") : 0;
				best = best is { } so ? System.Math.Max(so, into) : into;
			}
		}
		return figure + (best ?? 0);
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
