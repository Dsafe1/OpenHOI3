using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The nuclear bomb: an air unit takes one from its country's stock, flies it to an enemy's province and drops it, which
/// hurts the controller's national unity, lays every building there flat, empties the province's goods and damages what
/// stands in it. A bomb not dropped goes back to the stock, whatever ends the order. See <c>docs/mechanics/nukes.md</c>.
/// </summary>
public static class CNukeMission
{
	public const string WeNuke = "WE_NUKE";
	public const string NukeOnUs = "NUKE_ON_US";
	public const string NukeOnOther = "NUKE_ON_OTHER";

	/// <summary>What the country's technologies make of bombs in a month, in thousandths.</summary>
	public const string Production = "nuclear_production";

	private const string UnityImpact = "NUKE_UNITY_IMPACT";
	private const string OnDropped = "on_nuke_dropped";
	private const string StrategicAttack = "strategic_attack";

	/// <summary>One bomb in the stock, in thousandths [exe].</summary>
	private const long OneBomb = 1000;

	/// <summary>The least share of its full strength a bomb costs a unit, in thousandths [exe].</summary>
	private const long LeastShare = 670;

	/// <summary>What a roll in thousandths is divided by before it is added to that [exe].</summary>
	private const long RollDivisor = 3000;

	/// <summary>The organisation damage of a bomb, and what a building takes: more than any has [exe].</summary>
	private const long Overwhelming = 10000000;

	/// <summary>Every country's month: what its technology makes, with no cap and no regard to anything else [exe].</summary>
	public static void RunMonthly(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.Nukes += country.Technology[Production];
		}
	}

	/// <summary>Whether any wing of the unit has a strategic attack, which is all that makes it a carrier [exe].</summary>
	public static bool MayCarry(CUnit unit)
	{
		return unit.Kind == EUnitKind.Air && unit.SubUnits.Any(wing => wing.Stats[StrategicAttack] > CFixedPoint.Zero);
	}

	/// <summary>The order's own test: a carrier and a province an enemy holds. Having a bomb is the window's to ask [exe].</summary>
	public static bool MayGive(CGameState state, CUnit unit, CProvince province)
	{
		return MayCarry(unit) && CMovementRules.IsEnemy(state, province.Controller, unit.Owner);
	}

	/// <summary>The order installed: a bomb out of the stock where there is one, and on its way [exe].</summary>
	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		if (country.Nukes.Raw < OneBomb)
		{
			return;
		}
		unit.Mission.Nukes = 1;
		unit.Mission.BombsFrom = country;
		country.Nukes = new CFixedPoint(country.Nukes.Raw - OneBomb);
		Fly(state, country, unit);
	}

	/// <summary>What the order still carries back to the stock: an order replaced or ended, or the bomber lost [exe].</summary>
	public static void GiveBack(CMission mission)
	{
		if (mission.Nukes > 0 && mission.BombsFrom is { } country)
		{
			country.Nukes = new CFixedPoint(country.Nukes.Raw + mission.Nukes * OneBomb);
			mission.Nukes = 0;
		}
	}

	/// <summary>
	/// The hour: no base, no bomb or a target no longer an enemy's, and the order is over; in a battle, a bombing or under
	/// way, going on; over the target, the drop; elsewhere, its way again. It asks no end date and no range [exe].
	/// </summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		CProvince target = state.Province(unit.OrderProvince);
		if (unit.Base == 0 || unit.Mission.Nukes == 0 || !CMovementRules.IsEnemy(state, target.Controller, unit.Owner))
		{
			return false;
		}
		if (unit.IsFighting || unit.Bombing is not null || unit.IsMoving)
		{
			return true;
		}
		if (unit.Location == target.Id)
		{
			Drop(state, country, unit, target);
			return false;
		}
		Fly(state, country, unit);
		return true;
	}

	/// <summary>Home with no fuel; otherwise the way to the target, by day or by night as the order says [exe].</summary>
	private static void Fly(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.FuelReceived == CFixedPoint.Zero)
		{
			CAirMissions.ReturnToBase(state, country, unit);
			return;
		}
		CAirMissions.FlyTo(state, country, unit, state.Province(unit.OrderProvince), judgeWithoutWay: false);
	}

	/// <summary>The bomb falls, in the original's order: unity, buildings, goods, units, the messages and the on_action [exe].</summary>
	private static void Drop(CGameState state, CCountry country, CUnit unit, CProvince target)
	{
		CCountryTag held = target.Controller!.Value;
		CCountry? controller = state.Country(held);
		long amount = 0;
		if (controller is not null)
		{
			amount = UnityLost(state, controller, target);
			CNationalUnity.Change(controller, -amount);
		}
		foreach (CBuilding building in state.Buildings)
		{
			long left = System.Math.Max(0, target.BuildingLevel(building).Raw - Overwhelming);
			target.SetBuildingCurrentLevel(building, new CFixedPoint(left));
		}
		target.Pool.Clear();
		target.CurrentProducing.Clear();
		foreach (CUnit standing in state.Positions.Of(target.Id).ToList())
		{
			if (standing != unit && (standing.Kind != EUnitKind.Air || CAirMissions.IsAtOwnBase(standing)))
			{
				Hit(state, standing);
			}
		}
		state.RaisePactMessage(new CPactMessage(WeNuke, NukeOnUs, NukeOnOther, unit.Owner, held)
		{
			Province = target.Id,
			Values = new[]
			{
				("PROV", target.Name),
				("IMPACT", amount.ToString(CultureInfo.InvariantCulture)),
				("EVIL", country.Name),
				("TARGET", controller?.Name ?? held.Value),
			},
		});
		COnActions.Fire(state, OnDropped, CEventScope.Start(unit.Owner, target.Id, fromCountry: held));
		unit.Mission.Nukes = 0;
	}

	/// <summary>
	/// What the controller loses, before its own modifier scales it: the plain figure, and where the province is one of its
	/// cores a part more for each of the province's IC, the whole twice over at its acting capital [exe].
	/// </summary>
	public static long UnityLost(CGameState state, CCountry controller, CProvince target)
	{
		long factor = 1000;
		if (target.Cores.Contains(controller.Tag))
		{
			factor += target.Modifier[Economy.CIndustrialCapacity.ProvinceIc].Raw;
			if (target.Id == controller.ActingCapital)
			{
				factor *= 2;
			}
		}
		return state.Defines[CResearch.CountrySection, UnityImpact].Raw * factor / 1000;
	}

	private static long Share(CGameState state)
	{
		return LeastShare + state.Random.Next(1000) * 1000 / RollDivisor;
	}

	private static void Hit(CGameState state, CUnit unit)
	{
		if (state.Country(unit.FiguresOwner) is not { } owner)
		{
			return;
		}
		if (CMovementRules.IsLandUnit(unit))
		{
			CCombatDamage.Take(state, unit, Share(state) * CUnitTotals.FullStrength(unit).Raw / 1000, Overwhelming);
			CCombatDamage.SettleUnit(state, unit);
			return;
		}
		foreach (CSubUnit part in unit.SubUnits)
		{
			part.PendingStrengthDamage += Share(state) * CUnits.MaxStrength(state, owner, part).Raw / 1000;
			part.PendingOrganisationDamage += Overwhelming;
		}
		if (unit.Kind == EUnitKind.Air)
		{
			CCombatDamage.SettleUnit(state, unit);
			return;
		}
		CNavalLosses.SettleOutOfBattle(state, unit);
	}
}
