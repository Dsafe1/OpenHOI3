using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The AI's odds of an attack: the damage its units would do over the damage the defenders would do, each side's soft
/// and hard attack weighed by the other's softness and cut by its toughness - ours less what our units are busy
/// defending elsewhere, theirs less how their own attacks elsewhere are going - in thousandths. The original's
/// <c>CAIUnit::EstimateAttackOdds</c>. See <c>docs/mechanics/moving.md</c>, section 11.4.
/// </summary>
public static class CAttackOdds
{
	private const long Scale = CFixedPoint.Scale;

	private const long NoDefenders = 100 * Scale;

	private const long NoDamage = 50 * Scale;

	private const long MostElsewhere = 4 * Scale;

	private const long NoOdds = 10;

	public static long Of(CGameState state, IReadOnlyList<CUnit> ours, IReadOnlyList<CUnit> theirs, CProvince target,
		bool followCombats = true)
	{
		if (ours.Count < 1)
		{
			return 0;
		}
		if (theirs.Count < 1)
		{
			return NoDefenders;
		}
		var provinces = new HashSet<int>();
		long ourBrigades = 0;
		foreach (CUnit unit in ours)
		{
			provinces.Add(unit.Location);
			ourBrigades += unit.SubUnits.Count;
		}
		long theirBrigades = 0;
		var theirProvinces = new HashSet<int>();
		foreach (CUnit unit in theirs)
		{
			theirBrigades += unit.SubUnits.Count;
			theirProvinces.Add(unit.Location);
		}
		Side(state, ours, attacking: true, target, followCombats, ourBrigades, provinces.Count,
			out long softOurs, out long hardOurs, out long softnessOurs, out long toughOurs, out _);
		Side(state, theirs, attacking: false, target, followCombats, theirBrigades - 3 * provinces.Count, provinces.Count,
			out long softTheirs, out long hardTheirs, out long softnessTheirs, out long toughTheirs, out bool theirsNotANumber);
		long theirDamage = (Scale - toughOurs / 100) * (softTheirs * softnessOurs + hardTheirs * (Scale - softnessOurs)) / Scale / Scale;
		long ourDamage = (Scale - toughTheirs / 100) * (softOurs * softnessTheirs + hardOurs * (Scale - softnessTheirs)) / Scale / Scale;
		// A defender's half that is not a number makes their damage none either, and the original's test of it against nought
		// (comiss, jbe) takes an unordered answer for "not above": the odds of no damage (moving.md, section 11.4).
		if (theirDamage <= 0 || theirsNotANumber)
		{
			return NoDamage;
		}
		return ourDamage * Scale / theirDamage;
	}

	/// <summary>One side's soft and hard attack, mean softness and mean toughness (defensiveness for defenders), in thousandths.</summary>
	private static void Side(CGameState state, IReadOnlyList<CUnit> units, bool attacking, CProvince target, bool followCombats,
		long crowd, int enemyProvinces, out long soft, out long hard, out long softness, out long tough, out bool notANumber)
	{
		soft = 0;
		hard = 0;
		softness = 0;
		tough = 0;
		notANumber = false;
		foreach (CUnit unit in units)
		{
			long engagement = attacking ? Pinned(unit, followCombats) : Elsewhere(state, unit, target, followCombats);
			long model = attacking
				? CAiCombatModel.Attack(state, unit, target, crowd, enemyProvinces)
				: CAiCombatModel.Defence(state, unit, crowd, enemyProvinces);
			if (model == CAiCombatModel.NotANumber)
			{
				notANumber = true;
				model = 0;
			}
			long weight = MeanMillionths(unit, strength: true) * MeanMillionths(unit, strength: false) / Million * (engagement + model) / Scale;
			CCountry? owner = state.Country(unit.Owner);
			long softBonus = owner?.Modifier[CUnitTotals.SoftAttack].Raw ?? 0;
			long hardBonus = owner?.Modifier[CUnitTotals.HardAttack].Raw ?? 0;
			soft += (Scale + softBonus) * CUnitTotals.Sum(unit, CUnitTotals.SoftAttack).Raw / Scale * weight / Million;
			hard += (Scale + hardBonus) * CUnitTotals.Sum(unit, CUnitTotals.HardAttack).Raw / Scale * weight / Million;
			softness += CUnitTotals.AverageSoftness(unit).Raw;
			// A unit no battle has worked out yet has defence products of nought, and so counts no toughness at all;
			// one whose battle is over keeps its last hour's.
			if (unit.HasCombatProducts)
			{
				string kind = attacking ? CSubUnitStats.Toughness : CUnitTotals.Defensiveness;
				tough += CUnitTotals.Sum(unit, kind).Raw * unit.CombatDefence.Raw / Scale * unit.LandingDefence.Raw / Scale;
			}
		}
		softness /= units.Count;
		tough /= units.Count;
	}

	/// <summary>A unit of ours defending elsewhere: by the defender's share of each such battle, squared where it is losing.</summary>
	private static long Pinned(CUnit unit, bool followCombats)
	{
		long engagement = Scale;
		if (!followCombats)
		{
			return engagement;
		}
		foreach (CCombat combat in unit.Combats)
		{
			if (!combat.Defender.Units.Contains(unit))
			{
				continue;
			}
			long left = Scale - CCombatShares.AttackerShare(combat);
			engagement = engagement * left / Scale;
			if (left < Scale / 2)
			{
				engagement = engagement * left / Scale;
			}
		}
		return engagement;
	}

	/// <summary>A defender attacking elsewhere: by the odds of its first such battle, at most 4, over 4.</summary>
	private static long Elsewhere(CGameState state, CUnit unit, CProvince target, bool followCombats)
	{
		if (!followCombats)
		{
			return Scale;
		}
		foreach (CCombat combat in unit.Combats)
		{
			if (!combat.Attacker.Units.Contains(unit) || combat.Province == target.Id)
			{
				continue;
			}
			long odds = Of(state, combat.Attacker.Units, combat.Defender.Units, state.Province(combat.Province), false);
			long factor = odds == 0 ? NoOdds : System.Math.Min(odds, MostElsewhere);
			return factor / 4;
		}
		return Scale;
	}

	private const long Million = 1_000_000;

	/// <summary>The mean of its brigades' strength or organisation over a hundred, in millionths: the original's slots 20 and 21 over 100000.</summary>
	private static long MeanMillionths(CUnit unit, bool strength)
	{
		if (unit.SubUnits.Count == 0)
		{
			return 0;
		}
		long sum = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			sum += strength ? brigade.Strength.Raw : brigade.Organisation.Raw;
		}
		return sum / unit.SubUnits.Count * 10;
	}
}
