using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// Where a finished unit may be put, and putting it there. See
/// <c>docs/mechanics/buildqueue.md</c>, section 7.
/// </summary>
public static class CDeployment
{
	/// <summary>
	/// The land a country may place units in: the area its capital is in, which is the
	/// capital and every province the country holds that is joined to it through land
	/// it holds, each with more than one level of infrastructure. Nothing for a country
	/// that does not hold its capital. See <c>docs/mechanics/supply.md</c>, section 2.
	/// </summary>
	public static HashSet<int> HomeArea(CGameState state, CCountry country)
	{
		if (country.Capital <= 0 || state.Province(country.Capital).Controller != country.Tag)
		{
			return new HashSet<int>();
		}
		return new HashSet<int>(CSupplyNetworks.AreaAround(state, country.Capital));
	}

	/// <summary>
	/// Whether one of a country's waiting units may be put in a province: a fleet needs
	/// a naval base there, an air unit an air base, and all of them need the province to
	/// be in the country's home area.
	/// </summary>
	public static bool MayPlace(CGameState state, CCountry country, CUnit unit, int provinceId)
	{
		return MayPlace(state, unit, provinceId, HomeArea(state, country));
	}

	/// <summary>Every province one of a country's waiting units may be put in.</summary>
	public static HashSet<int> Places(CGameState state, CCountry country, CUnit unit)
	{
		HashSet<int> home = HomeArea(state, country);
		var places = new HashSet<int>();
		foreach (int provinceId in home)
		{
			if (MayPlace(state, unit, provinceId, home))
			{
				places.Add(provinceId);
			}
		}
		return places;
	}

	/// <summary>Takes a unit off the waiting list and puts it in a province, as a unit nothing commands.</summary>
	public static void Place(CCountry country, CUnit unit, int provinceId)
	{
		country.Deployments.Remove(unit);
		unit.Location = provinceId;
		if (unit.Kind == EUnitKind.Navy || unit.Kind == EUnitKind.Air)
		{
			unit.Base = provinceId;
		}
		country.Units.Add(unit);
	}

	/// <summary>The most land units an army group, an army or a corps may have under it.</summary>
	public const int MostLandSubordinates = 5;

	private const string IsCarrierAirGroup = "is_cag";

	/// <summary>
	/// The units in the field one of a country's waiting units may join, in the order
	/// the original lists them: by rank from the theatres down, then the fleets, then
	/// the air units, and within one kind in the order they were made.
	/// </summary>
	public static List<CUnit> Targets(CGameState state, CCountry country, CUnit unit)
	{
		var targets = new List<CUnit>();
		List<CUnit> inOrder = country.UnitsInOrder();
		HashSet<int> home = HomeArea(state, country);
		foreach (EUnitKind kind in Enum.GetValues<EUnitKind>())
		{
			foreach (CUnit target in inOrder)
			{
				if (target.Kind == kind && MayJoin(state, country, unit, target, home))
				{
					targets.Add(target);
				}
			}
		}
		return targets;
	}

	/// <summary>
	/// Whether a waiting unit may be put with one in the field. The unit in the field
	/// has to stand where the new one could be placed, and then: a lone brigade joins a
	/// land unit that has room for another; any other land unit goes under a
	/// headquarters that has room for it; a ship joins a fleet and a wing an air unit.
	/// </summary>
	public static bool MayJoin(CGameState state, CCountry country, CUnit unit, CUnit target, HashSet<int>? home = null)
	{
		if (ReferenceEquals(unit, target) || target.Location <= 0 || !target.Owner.Equals(country.Tag))
		{
			return false;
		}
		home ??= HomeArea(state, country);
		if (!MayPlace(state, unit, target.Location, home))
		{
			return false;
		}

		if (IsLand(unit))
		{
			if (unit.SubUnits.Count == 1)
			{
				return IsLand(target) && target.SubUnits.Count < CUnitAvailability.MostBrigades(state, country);
			}
			return MayTakeSubordinate(target, unit);
		}
		if (unit.Kind == EUnitKind.Air)
		{
			// A wing of ordinary aircraft does not join a carrier's air group.
			return target.Kind == EUnitKind.Air
				&& (target.SubUnits.Count == 0 || !IsCag(target.SubUnits[0]) || (unit.SubUnits.Count > 0 && IsCag(unit.SubUnits[0])));
		}
		return target.Kind == EUnitKind.Navy;
	}

	/// <summary>
	/// Whether one unit may be put under another: the commander has to be of a higher
	/// rank, and anything but a theatre takes no more than five land units.
	/// </summary>
	public static bool MayTakeSubordinate(CUnit commander, CUnit subordinate)
	{
		if ((int)commander.Kind >= (int)subordinate.Kind)
		{
			return false;
		}
		if (!IsLand(subordinate))
		{
			return (int)commander.Kind <= (int)EUnitKind.Corps;
		}
		if (commander.Kind == EUnitKind.Theatre)
		{
			return true;
		}
		int land = 0;
		foreach (CUnit child in commander.Children)
		{
			if (IsLand(child))
			{
				land++;
			}
		}
		return land < MostLandSubordinates;
	}

	/// <summary>
	/// Puts a waiting unit with one in the field. It is placed where that one stands;
	/// then a unit of a single brigade, ship or wing of the same service gives it to the
	/// other and is gone, and any other unit is put under it.
	/// </summary>
	public static void Join(CCountry country, CUnit unit, CUnit target)
	{
		Place(country, unit, target.Location);
		country.Units.Remove(unit);
		if (unit.SubUnits.Count == 1 && Service(unit) == Service(target))
		{
			CSubUnit subUnit = unit.SubUnits[0];
			unit.SubUnits.Clear();
			subUnit.Unit = target;
			target.SubUnits.Add(subUnit);
			return;
		}
		unit.Parent = target;
		target.Children.Add(unit);
	}

	private static bool MayPlace(CGameState state, CUnit unit, int provinceId, HashSet<int> home)
	{
		CProvince province = state.Province(provinceId);
		if (province.Id == 0)
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Navy && !HasBase(state, province, CBaseCrowding.NavalBase))
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Air && !HasBase(state, province, CBaseCrowding.AirBase))
		{
			return false;
		}
		return home.Contains(provinceId);
	}

	private static bool IsLand(CUnit unit)
	{
		return unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air;
	}

	private static EUnitBranch Service(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Navy => EUnitBranch.Naval,
			EUnitKind.Air => EUnitBranch.Air,
			_ => EUnitBranch.Land,
		};
	}

	private static bool IsCag(CSubUnit subUnit)
	{
		return subUnit.Type.Flag(IsCarrierAirGroup);
	}

	private static bool HasBase(CGameState state, CProvince province, string key)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return province.BuildingLevel(building) > CFixedPoint.Zero;
			}
		}
		return false;
	}
}
