using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a unit uses in a day, in supplies and in fuel. See
/// <c>docs/mechanics/upkeep.md</c>, section 1. The taking of it is
/// <see cref="Supply.CSupplyDraw"/>: a unit draws on the province it stands in, which
/// the supply network keeps filled.
/// </summary>
public static class CUnitSupply
{
	/// <summary>The key of <c>Consumption</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey ConsumptionKey = CModifierKey.Of(Consumption);

	/// <summary>The key of <c>SupplyStatistic</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey SupplyStatisticKey = CModifierKey.Of(SupplyStatistic);

	/// <summary>The key of <c>FuelStatistic</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey FuelStatisticKey = CModifierKey.Of(FuelStatistic);

	/// <summary>The modifier a law or a minister sets: more or less of everything a unit uses.</summary>
	public const string Consumption = "supply_consumption";

	public const string SupplyStatistic = "supply_consumption";
	public const string FuelStatistic = "fuel_consumption";

	/// <summary>Every level of anything a brigade holds makes it use a hundredth more.</summary>
	private const long PerLevel = 10;

	/// <summary>What each point of an army group commander's skill takes off, from a float of 50.5.</summary>
	private const long CommanderDiscount = 50;

	/// <summary>What a land unit on a strategic redeployment uses of supplies, times its usual: 2 in Their Finest Hour.</summary>
	public const string RedeploymentSupply = "STRAT_REDEP_SUPPLY_MOD";

	/// <summary>
	/// A land unit on a strategic redeployment: it uses <see cref="RedeploymentSupply"/> times the supplies, and no fuel
	/// (<c>reversing/findings/FINDINGS-unitdaily.md</c>; <c>docs/mechanics/upkeep.md</c>, section 1).
	/// </summary>
	private static bool IsRedeploying(CUnit unit) =>
		unit.Order == EUnitOrder.StrategicRedeployment && Movement.CMovementRules.IsLandUnit(unit);

	/// <summary>The supplies scaled for a strategic redeployment, where the defines say how much.</summary>
	private static long Redeployed(CGameState state, CUnit unit, long used)
	{
		if (!IsRedeploying(unit) || !state.Defines.Has(CManpower.MilitarySection, RedeploymentSupply))
		{
			return used;
		}
		return used * state.Defines[CManpower.MilitarySection, RedeploymentSupply].Raw / CFixedPoint.Scale;
	}

	/// <summary>
	/// What everything a unit uses is multiplied by, in thousandths: one and the
	/// country's modifier, less a twentieth for each point of the skill of the army
	/// group's commander, by how much of his command reaches the unit, and with what the
	/// leaders' <c>supply_consumption</c> traits come to for it. Never below nothing.
	/// </summary>
	public static long Potency(CGameState state, CCountry owner, CUnit unit)
	{
		long potency = CFixedPoint.Scale + owner.Modifier[ConsumptionKey].Raw;

		CUnit? group = unit;
		while (group is not null && CCommandReach.Level(group) != 1)
		{
			group = group.Parent;
		}
		if (group is not null && unit.Location != 0 && group.Leader is { Skill: > 0 } leader)
		{
			long discount = leader.Skill * CFixedPoint.Scale * CommanderDiscount / CFixedPoint.Scale;
			potency -= CCommandReach.Through(state, unit, 1) * discount / CFixedPoint.Scale;
		}

		potency += CCommandReach.Effect(state, unit, ETraitEffect.SupplyConsumption);
		return Math.Max(0, potency);
	}

	/// <summary>The supplies one brigade uses, as its card says: its unit's potency, and a part of it for a land brigade or a wing under strength.</summary>
	public static CFixedPoint SubUnitSupply(CGameState state, CCountry owner, CSubUnit subUnit)
	{
		if (subUnit.Unit is not { } unit)
		{
			return subUnit.Stats[SupplyStatisticKey];
		}
		long used = subUnit.Stats[SupplyStatisticKey].Raw * (Potency(state, owner, unit) + LevelsHeld(subUnit) * PerLevel) / CFixedPoint.Scale;
		if (unit.Kind != EUnitKind.Navy)
		{
			used = used * Share(subUnit) / CFixedPoint.Scale;
		}
		return new CFixedPoint(Redeployed(state, unit, used));
	}

	/// <summary>
	/// The fuel one brigade uses, as its card says. Unlike a unit's own total it goes with the brigade's strength, a ship
	/// apart, so what a brigade shows and what its unit is charged are not one sum - the original's.
	/// </summary>
	public static CFixedPoint SubUnitFuel(CGameState state, CCountry owner, CSubUnit subUnit)
	{
		if (subUnit.Unit is not { } unit)
		{
			return subUnit.Stats[FuelStatisticKey];
		}
		if (IsRedeploying(unit))
		{
			return CFixedPoint.Zero;
		}
		long used = subUnit.Stats[FuelStatisticKey].Raw * (Potency(state, owner, unit) + LevelsHeld(subUnit) * PerLevel) / CFixedPoint.Scale;
		if (subUnit.Type.Branch != EUnitBranch.Naval)
		{
			used = used * Share(subUnit) / CFixedPoint.Scale;
		}
		return new CFixedPoint(used);
	}

	/// <summary>A brigade's strength over its most, in thousandths; -1 where its type has no most.</summary>
	private static long Share(CSubUnit subUnit)
	{
		long full = subUnit.Stats.MaxStrength.Raw;
		return full == 0 ? -1 : subUnit.Strength.Raw * CFixedPoint.Scale / full;
	}

	/// <summary>
	/// The levels a brigade holds, of all its technologies added together (<see cref="CSubUnit.LevelsHeld"/>).
	/// </summary>
	public static long LevelsHeld(CSubUnit subUnit) => subUnit.LevelsHeld;

	/// <summary>
	/// The supplies a unit's own brigades use in a day. A brigade under strength uses
	/// that much less; a ship uses the same whatever state it is in.
	/// </summary>
	public static CFixedPoint SupplyConsumption(CGameState state, CCountry owner, CUnit unit)
	{
		owner = state.Country(unit.FiguresOwner) ?? owner;
		long potency = Potency(state, owner, unit);
		long total = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			long used = subUnit.Stats[SupplyStatisticKey].Raw * (potency + LevelsHeld(subUnit) * PerLevel) / CFixedPoint.Scale;
			if (unit.Kind != EUnitKind.Navy)
			{
				long full = subUnit.Stats.MaxStrength.Raw;
				long share = full == 0 ? -1 : subUnit.Strength.Raw * CFixedPoint.Scale / full;
				used = used * share / CFixedPoint.Scale;
			}
			total += used;
		}
		return new CFixedPoint(Redeployed(state, unit, total));
	}

	/// <summary>The fuel a unit's own brigades use in a day when they use any. Strength does not come into it.</summary>
	public static CFixedPoint FuelConsumption(CGameState state, CCountry owner, CUnit unit)
	{
		owner = state.Country(unit.FiguresOwner) ?? owner;
		if (IsRedeploying(unit))
		{
			return CFixedPoint.Zero;
		}
		long potency = Potency(state, owner, unit);
		long total = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			total += subUnit.Stats[FuelStatisticKey].Raw * (potency + LevelsHeld(subUnit) * PerLevel) / CFixedPoint.Scale;
		}
		return new CFixedPoint(total);
	}

	/// <summary>
	/// Whether a unit burns fuel today: one that is moving, and a fleet that is not in
	/// its own port. An army or an air unit standing still burns none.
	/// </summary>
	public static bool BurnsFuel(CUnit unit)
	{
		return unit.IsMoving || (unit.Kind == EUnitKind.Navy && unit.Location != unit.Base);
	}

	/// <summary>What all of a country's units use in a day, whether or not they burn fuel today.</summary>
	public static (CFixedPoint Supplies, CFixedPoint Fuel) DailyUse(CGameState state, CCountry country)
	{
		CFixedPoint supplies = CFixedPoint.Zero;
		CFixedPoint fuel = CFixedPoint.Zero;
		foreach (CUnit unit in country.AllUnits())
		{
			supplies += SupplyConsumption(state, country, unit);
			fuel += FuelConsumption(state, country, unit);
		}
		return (supplies, fuel);
	}
}
