using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The bombers' hour: every unit makes its list of targets afresh by what its mission
/// goes for, and each of its wings fires at one target drawn from it with the attack the
/// mission names, and then bombs the province. Nothing a target has defends it. Written
/// to roll in the original's order, the roll nothing uses with it. See
/// <c>docs/mechanics/bombing.md</c>, section 6.
/// </summary>
public static class CBomberFire
{
	public const string RegimentDamage = "BOMB_REGIMENT_DAMAGE_MODIFIER";
	public const string ShipDamage = "BOMB_SHIP_DAMAGE_MODIFIER";
	public const string WingDamage = "BOMB_WING_DAMAGE_MODIFIER";
	public const string CagStrengthBonus = "CAG_SHIP_ATTACK_STR_BONUS";
	public const string CagOrganisationBonus = "CAG_SHIP_ATTACK_ORG_BONUS";

	public const string FrontlineFocus = "frontline_focus";
	public const string ReserveFocus = "reserve_focus";
	public const string NavalTargetChance = "naval_air_target_chance";

	/// <summary>The most a shot is ever avoided by, in thousandths of a hundred.</summary>
	private const long MostAvoided = 99000;

	/// <summary>How many targets a unit's list holds at most.</summary>
	private const int MostTargets = 1024;

	/// <summary>A brigade and a wing take a hundredth of what is rolled against them, when they are settled.</summary>
	private const long SettledShare = 10;

	// What a hit keeps of its two dice by what it hits. They are the original's own
	// figures and no file sets them.
	private const long RegimentStrengthShare = 400;
	private const long RegimentOrganisationShare = 800;
	private const long ShipStrengthShare = 300;
	private const long ShipOrganisationShare = 500;
	private const long WingStrengthShare = 500;
	private const long WingOrganisationShare = 700;

	/// <summary>A hit on a brigade is this much harder for every brigade on the side bombed.</summary>
	private const long PerDefendingBrigade = 10;

	public static void Attack(CGameState state, CCombat bombing)
	{
		CCombatSide side = bombing.Attacker;
		CCombatSide other = bombing.Defender;
		var need = new CProvinceBombing.CNeed(state, bombing.Province);
		foreach (CUnit unit in new List<CUnit>(side.Units))
		{
			CCountry? owner = state.Country(unit.Owner);
			if (CBombingOrders.PrefersFighting(unit.Order))
			{
				CollectFighting(state, other, side, owner);
			}
			if (!CBombingOrders.PrefersFighting(unit.Order) || side.Targets.Count == 0)
			{
				CollectTargets(state, other, side, unit, owner);
			}
			foreach (CSubUnit wing in new List<CSubUnit>(unit.SubUnits))
			{
				FireWing(state, bombing, side, unit, wing);
				CProvinceBombing.Bomb(state, bombing, side, unit, wing, need);
			}
		}
	}

	/// <summary>
	/// A sub unit's strength less what this hour has rolled against it so far, as it
	/// will be settled: all of it for a ship, a hundredth for a brigade or a wing.
	/// </summary>
	public static long StrengthLeft(CSubUnit subUnit)
	{
		long pending = subUnit.Unit?.Kind == EUnitKind.Navy
			? subUnit.PendingStrengthDamage
			: subUnit.PendingStrengthDamage * SettledShare / CFixedPoint.Scale;
		return subUnit.Strength.Raw - pending;
	}

	/// <summary>
	/// The list of a mission that looks first for units that are fighting: each land
	/// unit of the other side that is in a battle is taken by a roll - by the bombers'
	/// country's <c>frontline_focus</c> where it is on a front, its <c>reserve_focus</c>
	/// otherwise, a tenth of each a chance in a hundred and one chance always.
	/// </summary>
	public static void CollectFighting(CGameState state, CCombatSide other, CCombatSide side, CCountry? owner)
	{
		List<CSubUnit> targets = side.Targets;
		targets.Clear();
		foreach (CUnit unit in other.Units)
		{
			if (!CMovementRules.IsLandUnit(unit) || !unit.IsFighting)
			{
				continue;
			}
			bool isOnFront = false;
			foreach (CCombat combat in unit.Combats)
			{
				if (combat.Kind == ECombatKind.Land
					&& (combat.Attacker.Front.Contains(unit) || combat.Defender.Front.Contains(unit)))
				{
					isOnFront = true;
					break;
				}
			}
			long focus = owner is null ? 0 : owner.Technology[isOnFront ? FrontlineFocus : ReserveFocus].Raw;
			if (state.Random.Next(100) <= focus * 100 / CFixedPoint.Scale)
			{
				Add(targets, unit);
			}
		}
	}

	/// <summary>
	/// The list of any mission: the other side's aircraft at their base, its land units
	/// or its fleets by what the mission goes for. A ship goes on the list only by a
	/// roll of its own: ten in a hundred for each point of the bombers' leader's skill,
	/// more by the country's <c>naval_air_target_chance</c> - and with no leader, one in
	/// a hundred.
	/// </summary>
	public static void CollectTargets(CGameState state, CCombatSide other, CCombatSide side, CUnit bomber,
		CCountry? owner)
	{
		List<CSubUnit> targets = side.Targets;
		targets.Clear();
		EUnitOrder order = bomber.Order;
		foreach (CUnit unit in other.Units)
		{
			bool isAir = unit.Kind == EUnitKind.Air;
			bool isFleet = unit.Kind == EUnitKind.Navy;
			if (isAir && (!CBombingOrders.GoesForBasedAircraft(order)
				|| !CBombingOrders.ShouldBomb(state, bomber, state.Province(bomber.Location))))
			{
				continue;
			}
			if (CMovementRules.IsLandUnit(unit) && !CBombingOrders.GoesForLandUnits(order))
			{
				continue;
			}
			if (isFleet && !CBombingOrders.GoesForShips(order))
			{
				continue;
			}
			if (isAir && !CAirMissions.IsAtOwnBase(unit))
			{
				continue;
			}
			if (!isFleet)
			{
				Add(targets, unit);
				continue;
			}
			long chance = LeaderSkill(bomber) * CFixedPoint.Scale * 10000 / CFixedPoint.Scale;
			chance = chance * ((owner is null ? 0 : owner.Technology[NavalTargetChance].Raw) + CFixedPoint.Scale) / CFixedPoint.Scale;
			foreach (CSubUnit ship in unit.SubUnits)
			{
				if (state.Random.Next(100) <= chance / CFixedPoint.Scale
					&& StrengthLeft(ship) >= 0 && targets.Count < MostTargets)
				{
					targets.Add(ship);
				}
			}
		}
	}

	/// <summary>
	/// The skill of a unit's leader. What a leader does in a battle is not built yet, so
	/// every unit counts as the original's unit with none: one of no skill.
	/// </summary>
	public static long LeaderSkill(CUnit unit)
	{
		return 0;
	}

	private static void Add(List<CSubUnit> targets, CUnit unit)
	{
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (StrengthLeft(subUnit) >= 0 && targets.Count < MostTargets)
			{
				targets.Add(subUnit);
			}
		}
	}

	/// <summary>The attack a wing fires at a target with, by its unit's mission.</summary>
	public static long AttackOf(EUnitOrder order, CSubUnit wing, CSubUnit target)
	{
		if (CBombingOrders.GoesForLandUnits(order))
		{
			long softness = Math.Min(target.Stats[CUnitTotals.Softness].Raw, CFixedPoint.Scale);
			long hard = wing.Stats[CUnitTotals.HardAttack].Raw * (CFixedPoint.Scale - softness) / CFixedPoint.Scale;
			return wing.Stats[CUnitTotals.SoftAttack].Raw * softness / CFixedPoint.Scale + hard;
		}
		if (CBombingOrders.GoesForShips(order))
		{
			return wing.Stats[CNavalFire.SeaAttack].Raw;
		}
		return CBombingOrders.GoesForBasedAircraft(order) ? wing.Stats[CUnitTotals.HardAttack].Raw : 0;
	}

	private static void FireWing(CGameState state, CCombat bombing, CCombatSide side, CUnit unit, CSubUnit wing)
	{
		List<CSubUnit> targets = side.Targets;
		if (targets.Count < 1)
		{
			return;
		}
		CDefines defines = state.Defines;
		string military = CManpower.MilitarySection;
		CSubUnit target = targets[state.Random.Next(targets.Count)];
		long attack = AttackOf(unit.Order, wing, target);

		long brigades = 0;
		foreach (CUnit defender in bombing.Defender.Units)
		{
			brigades += defender.SubUnits.Count;
		}
		long perBrigade = brigades * CFixedPoint.Scale * PerDefendingBrigade / CFixedPoint.Scale + CFixedPoint.Scale;

		long worth = wing.CombatAttack.Raw * attack / CFixedPoint.Scale;
		long shots = worth / CFixedPoint.Scale;
		if (state.Random.Next(100) < worth % CFixedPoint.Scale * 100 / CFixedPoint.Scale)
		{
			shots++;
		}
		long avoid = Math.Min(defines[military, CAirFire.AvoidChance].Raw, MostAvoided) / CFixedPoint.Scale;
		EUnitKind? kind = target.Unit?.Kind;
		for (; shots > 0; shots--)
		{
			if (StrengthLeft(target) < 0)
			{
				return;
			}
			state.Random.Next();                               // a roll nothing uses
			if (state.Random.Next(100) < avoid)
			{
				continue;
			}
			long organisation = (state.Random.Next(7) + 3) * (long)CFixedPoint.Scale;
			long strength = (state.Random.Next(3) + 1) * (long)CFixedPoint.Scale;
			if (state.Random.Next(100) < 10)
			{
				strength *= 10;
			}

			if (kind == EUnitKind.Navy)
			{
				long hull = target.Stats[CNavalBattle.Hull].Raw;
				if (hull > 0)
				{
					strength = strength * CFixedPoint.Scale / hull;
					organisation = organisation * CFixedPoint.Scale / hull;
				}
				if (wing.Type.Flag(CAirFire.IsCag) && target.Unit is { IsFighting: true })
				{
					strength = strength * defines[military, CagStrengthBonus].Raw / CFixedPoint.Scale;
					organisation = organisation * defines[military, CagOrganisationBonus].Raw / CFixedPoint.Scale;
				}
				long modifier = defines[military, ShipDamage].Raw;
				strength = strength * ShipStrengthShare / CFixedPoint.Scale * modifier / CFixedPoint.Scale;
				organisation = organisation * ShipOrganisationShare / CFixedPoint.Scale * modifier / CFixedPoint.Scale;
			}
			else if (kind == EUnitKind.Air)
			{
				long modifier = defines[military, WingDamage].Raw;
				strength = strength * WingStrengthShare / CFixedPoint.Scale * modifier / CFixedPoint.Scale;
				organisation = organisation * WingOrganisationShare / CFixedPoint.Scale * modifier / CFixedPoint.Scale;
			}
			else if (kind is not null)
			{
				long modifier = defines[military, RegimentDamage].Raw;
				strength = strength * perBrigade / CFixedPoint.Scale * RegimentStrengthShare / CFixedPoint.Scale
					* modifier / CFixedPoint.Scale;
				organisation = RegimentOrganisationShare * organisation / CFixedPoint.Scale * modifier / CFixedPoint.Scale;
			}
			target.PendingOrganisationDamage += organisation;
			target.PendingStrengthDamage += strength;
			if (kind == EUnitKind.Navy && StrengthLeft(target) < 0
				&& (wing.Type.Flag(CAirModifiers.IsBomber) || wing.Type.Flag(CAirFire.IsCag)))
			{
				target.SunkById = wing.Id;
			}
		}
	}
}
