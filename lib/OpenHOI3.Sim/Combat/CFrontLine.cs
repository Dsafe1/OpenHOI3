using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// Who stands on the front of a battle. A battle has a width, every unit takes some of
/// it, and a side's units fight only while they are on its front; the rest wait in
/// reserve for room and for luck. See <c>docs/mechanics/landcombat.md</c>, section 3.
/// </summary>
public static class CFrontLine
{
	public const string BaseWidth = "BASE_COMBAT_WIDTH";
	public const string AdditionalWidth = "ADDITIONAL_COMBAT_WIDTH";

	/// <summary>The technology figure and the two country modifiers a reserve's chance takes.</summary>
	public const string TechnologyReinforceChance = "reinforce_chance";
	public const string AttackReinforceChance = "attack_reinforce_chance";
	public const string DefendReinforceChance = "defend_reinforce_chance";

	/// <summary>What every reserve's chance starts from, in thousandths.</summary>
	public const long BaseReinforceChance = 10;

	/// <summary>What the best score has to beat, so that a score of nothing is still taken.</summary>
	private const long NoScore = -1000;

	/// <summary>
	/// The province a unit counts as attacking from, for the width: the one it is in,
	/// or the one it was in before where it already stands in the battle's own.
	/// </summary>
	public static int DirectionOf(CCombat combat, CUnit unit)
	{
		return unit.Location == combat.Province && unit.Previous != 0 ? unit.Previous : unit.Location;
	}

	/// <summary>
	/// The province a unit counts as coming from when its side's reserves are weighed.
	/// The original takes this the other way about from the width: the province it was
	/// in before for a unit outside the battle's own, where it has one.
	/// </summary>
	public static int OriginOf(CCombat combat, CUnit unit)
	{
		return unit.Location != combat.Province && unit.Previous != 0 ? unit.Previous : unit.Location;
	}

	/// <summary>
	/// How wide the battle is: what the tactics add, the base width for the first
	/// province the attackers come from, and the additional width for each further one -
	/// the dropped attackers all counted by the first of them. It is one figure for both sides.
	/// </summary>
	public static CFixedPoint Width(CGameState state, CCombat combat)
	{
		long width = combat.TacticWidth.Raw;
		var seen = new List<int>();
		bool paratrooper = false;
		foreach (CUnit unit in combat.Attacker.Units)
		{
			// Every dropped unit after the first counts for nothing (transport.md, section 13.4).
			if (unit.WasParadropped)
			{
				if (paratrooper)
				{
					continue;
				}
				paratrooper = true;
			}
			int from = DirectionOf(combat, unit);
			if (seen.Contains(from))
			{
				continue;
			}
			width += state.Defines[CManpower.MilitarySection, seen.Count == 0 ? BaseWidth : AdditionalWidth].Raw;
			seen.Add(from);
		}
		return new CFixedPoint(width);
	}

	/// <summary>What is left of the width with the front as it stands; negative when it is over.</summary>
	public static long Room(CGameState state, CCombat combat, CCombatSide side)
	{
		long room = Width(state, combat).Raw;
		foreach (CUnit unit in side.Front)
		{
			room -= UnitWidth(state, unit).Raw;
		}
		return room;
	}

	public static CFixedPoint UnitWidth(CGameState state, CUnit unit)
	{
		return state.Country(unit.Owner) is { } owner ? CUnitTotals.Width(owner, unit) : CUnitTotals.LeastWidth;
	}

	/// <summary>
	/// What a side's units are ordered by for the front, the largest first. For an
	/// attacker: its soft attack, hard attack and toughness and one more, by its average
	/// organisation and by its strength. A defender's has its defensiveness for the
	/// toughness and its leader's skill, whole, added - whatever his rank.
	/// </summary>
	public static long FrontOrder(CUnit unit, bool isAttacker)
	{
		long figure = CUnitTotals.Sum(unit, CUnitTotals.SoftAttack).Raw
			+ CUnitTotals.Sum(unit, CUnitTotals.HardAttack).Raw
			+ CUnitTotals.Sum(unit, isAttacker ? CSubUnitStats.Toughness : CUnitTotals.Defensiveness).Raw
			+ (isAttacker ? 0 : (unit.Leader?.Skill ?? 0) * (long)CFixedPoint.Scale)
			+ CFixedPoint.Scale;
		figure = figure * CUnitTotals.AverageOrganisation(unit).Raw / CFixedPoint.Scale;
		return CUnitTotals.Strength(unit).Raw * figure / CFixedPoint.Scale;
	}

	/// <summary>
	/// Puts a side's units on the front for the first time: the ones that can fight,
	/// best first, for as long as there is room - asked before each is added, so the
	/// front may end one unit over. Everyone else is a reserve.
	/// </summary>
	public static void SetUp(CGameState state, CCombat combat, CCombatSide side)
	{
		side.IsFrontSetUp = true;
		var able = new List<CUnit>();
		foreach (CUnit unit in side.Units)
		{
			if (side.Front.Contains(unit) || side.Reserves.Contains(unit))
			{
				continue;
			}
			if (!CUnitTotals.CanFight(unit))
			{
				side.Reserves.Add(unit);
				continue;
			}
			// A unit goes in before the first one that is worse than it, so after its equals.
			long order = FrontOrder(unit, side.IsAttacker);
			int at = 0;
			while (at < able.Count && FrontOrder(able[at], side.IsAttacker) >= order)
			{
				at++;
			}
			able.Insert(at, unit);
		}

		int placed = 0;
		while (placed < able.Count && Room(state, combat, side) >= 0)
		{
			side.Front.Add(able[placed]);
			placed++;
		}
		for (; placed < able.Count; placed++)
		{
			side.Reserves.Add(able[placed]);
		}
	}

	/// <summary>
	/// A side's front, each hour, before it shoots. A front unit that can no longer
	/// fight falls back out of the battle, and where it is a defender the province's
	/// buildings may be damaged. Then, with the front over the width even
	/// without its last unit, that unit goes back to the reserves - one an hour;
	/// otherwise reserves come up while there is room.
	/// </summary>
	public static void Update(CGameState state, CCombat combat, CCombatSide side)
	{
		foreach (CUnit unit in new List<CUnit>(side.Front))
		{
			if (!CUnitTotals.CanFight(unit))
			{
				side.Front.Remove(unit);
				CCombatRetreat.Retreat(state, unit, side);
				if (side == combat.Defender)
				{
					CProvinceHands.DamageBuildings(state, state.Province(combat.Province));
				}
			}
		}

		if (Room(state, combat, side) < 0)
		{
			if (side.Front.Count > 0)
			{
				CUnit last = side.Front[^1];
				long used = 0;
				foreach (CUnit unit in side.Front)
				{
					used += UnitWidth(state, unit).Raw;
				}
				if (Width(state, combat).Raw < used - UnitWidth(state, last).Raw)
				{
					side.Front.RemoveAt(side.Front.Count - 1);
					side.Reserves.Add(last);
				}
			}
			return;
		}

		while (Room(state, combat, side) >= 0 && side.Reserves.Count > 0
			&& PickReinforcement(state, combat, side) is { } pick)
		{
			side.Reserves.Remove(pick);
			side.Front.Add(pick);
		}
	}

	/// <summary>
	/// Which reserve comes up, or none. Every reserve that can fight rolls once, in a
	/// battle past its first hour; of those whose roll lets them, the best scored comes -
	/// one from a province its side already fills the base width from counting for a
	/// thousandth of its score.
	/// </summary>
	public static CUnit? PickReinforcement(CGameState state, CCombat combat, CCombatSide side)
	{
		var used = new Dictionary<int, long>();
		foreach (CUnit unit in side.Units)
		{
			int origin = OriginOf(combat, unit);
			used[origin] = used.GetValueOrDefault(origin) + UnitWidth(state, unit).Raw / CFixedPoint.Scale;
		}
		long full = state.Defines[CManpower.MilitarySection, BaseWidth].Raw / CFixedPoint.Scale;

		CUnit? best = null;
		long bestScore = NoScore;
		foreach (CUnit unit in side.Reserves)
		{
			if (!CUnitTotals.CanFight(unit) || combat.Hour <= 0)
			{
				continue;
			}
			long percent = ReinforceChance(state, unit, side.IsAttacker).Raw * 100 / CFixedPoint.Scale;
			if (state.Random.Next(100) > percent)
			{
				continue;
			}
			long score = ReinforceScore(unit);
			if (used.GetValueOrDefault(OriginOf(combat, unit)) >= full)
			{
				score /= CFixedPoint.Scale;
			}
			if (score > bestScore)
			{
				best = unit;
				bestScore = score;
			}
		}
		return best;
	}

	/// <summary>
	/// A reserve's chance of coming up this hour, in thousandths of a hundred per cent:
	/// the base, its country's technology and its modifier for the side, by its average
	/// organisation over a hundred. A corps, or a division under one, whose corps stands
	/// in its province or the next adds the corps commander's skill, ten a point, by how
	/// much of the corps' command reaches it.
	/// </summary>
	public static CFixedPoint ReinforceChance(CGameState state, CUnit unit, bool isAttacker)
	{
		if (!CUnitTotals.CanFight(unit) || state.Country(unit.Owner) is not { } owner)
		{
			return CFixedPoint.Zero;
		}
		long chance = BaseReinforceChance
			+ owner.Technology[TechnologyReinforceChance].Raw
			+ owner.Modifier[isAttacker ? AttackReinforceChance : DefendReinforceChance].Raw;

		CUnit? corps = unit.Kind switch
		{
			EUnitKind.Division => unit.Parent is { Kind: EUnitKind.Corps } parent ? parent : null,
			EUnitKind.Corps => unit,
			_ => null,
		};
		if (corps is not null
			&& (corps.Location == unit.Location || state.Province(corps.Location).EdgeTo(unit.Location) is not null))
		{
			long skill = (corps.Leader?.Skill ?? 0) * CFixedPoint.Scale * BaseReinforceChance / CFixedPoint.Scale;
			chance += Leaders.CCommandReach.Through(state, unit, Leaders.CCommandReach.Level(corps)) * skill / CFixedPoint.Scale;
		}
		long share = CUnitTotals.AverageOrganisation(unit).Raw * CFixedPoint.Scale / (100 * CFixedPoint.Scale);
		return new CFixedPoint(share * chance / CFixedPoint.Scale);
	}

	/// <summary>
	/// What ranks the reserves whose roll let them stand: the unit's two attacks, its
	/// defensiveness and its toughness, by its organisation and by its strength, each
	/// over a hundred.
	/// </summary>
	public static long ReinforceScore(CUnit unit)
	{
		long score = CUnitTotals.Sum(unit, CUnitTotals.SoftAttack).Raw
			+ CUnitTotals.Sum(unit, CUnitTotals.HardAttack).Raw
			+ CUnitTotals.Sum(unit, CUnitTotals.Defensiveness).Raw
			+ CUnitTotals.Sum(unit, CSubUnitStats.Toughness).Raw;
		long organisation = CUnitTotals.AverageOrganisation(unit).Raw * 10 / CFixedPoint.Scale;
		score = score * organisation / CFixedPoint.Scale;
		long strength = CUnitTotals.Strength(unit).Raw * 10 / CFixedPoint.Scale;
		return score * strength / CFixedPoint.Scale;
	}
}
