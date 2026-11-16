using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
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
		if (country.ActingCapital <= 0)
		{
			return new HashSet<int>();
		}
		return new HashSet<int>(CSupplyNetworks.AreaAround(state, country.ActingCapital));
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

	/// <summary>
	/// Takes a unit off the waiting list and puts it in a province, as a unit nothing
	/// commands; a country that chooses its own leaders gives it the best it has free
	/// (<c>docs/mechanics/leaders.md</c>, section 5).
	/// </summary>
	public static void Place(CGameState state, CCountry country, CUnit unit, int provinceId)
	{
		country.Deployments.Remove(unit);
		unit.DeploymentId = 0;
		unit.Location = provinceId;
		// Put on the map, every brigade starts at no organisation - the original's, where its custom-game setting for
		// placing at full organisation is off, which it always is here (buildqueue.md, section 7).
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			brigade.Organisation = CFixedPoint.Zero;
		}
		if (unit.Kind == EUnitKind.Navy || unit.Kind == EUnitKind.Air)
		{
			unit.Base = provinceId;
		}
		country.Units.Add(unit);
		if (country.AutoAssignLeaders)
		{
			CLeaders.AssignBest(state, country, unit);
		}
		// Every unit put on the map, joining one or not, spreads its owner's threat (diplomacy.md, 5.3).
		Diplomacy.CThreat.UnitDeployed(state, country, unit);
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
			return MayTakeSubordinate(state, target, unit);
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
	/// rank, and anything but a theatre takes no more than five land units - or, where a mod's Lua has set the limits as
	/// BiceLib's <c>Units</c> do, the limit of <see cref="LandSubordinateLimit"/>.
	/// </summary>
	public static bool MayTakeSubordinate(CGameState state, CUnit commander, CUnit subordinate)
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
		if (!state.BiceLib.IsOn(Mods.CBiceLibSwitches.UnitLimits))
		{
			return land < MostLandSubordinates;
		}
		// A one-brigade division is always taken, and a commander with nothing under it takes anything.
		if (IsLoneBrigade(subordinate) || commander.Children.Count == 0)
		{
			return true;
		}
		return land < LandSubordinateLimit(state, commander, subordinate.Owner);
	}

	/// <summary>
	/// The most land units a headquarters takes under the limits a mod's Lua set (BiceLib's <c>Units</c>,
	/// <c>docs/mechanics/bicelib.md</c>, section 3): an army group's, an army's or a corps' limit for the country whose unit is
	/// put under it - its own, else the default, else five - plus what each trait of its leader adds, plus the one-brigade
	/// divisions it already has, which are not to count.
	/// </summary>
	public static int LandSubordinateLimit(CGameState state, CUnit commander, CCountryTag owner)
	{
		Mods.CBiceLibSwitches switches = state.BiceLib;
		int limit = commander.Kind is EUnitKind.ArmyGroup or EUnitKind.Army or EUnitKind.Corps
			? switches.UnitLimit(commander.Kind, owner)
			: MostLandSubordinates;
		if (commander.Leader is { } leader && switches.HasCommandLimitTraits)
		{
			foreach (CTrait trait in leader.Traits)
			{
				limit += switches.CommandLimitTrait(trait.Key);
			}
		}
		foreach (CUnit child in commander.Children)
		{
			if (IsLoneBrigade(child))
			{
				limit++;
			}
		}
		return limit;
	}

	/// <summary>A brigade on its own, held as a division of one.</summary>
	private static bool IsLoneBrigade(CUnit unit) => unit.Kind == EUnitKind.Division && unit.SubUnits.Count == 1;

	/// <summary>
	/// Puts a waiting unit with one in the field. It is placed where that one stands,
	/// which may give it a leader; then a unit of a single brigade, ship or wing of the
	/// same service gives it to the other and is gone, and any other unit is put under
	/// it. The one that took a brigade is given a leader if it has none and its country
	/// chooses its own - while the one that gave it still holds whoever it was given, so
	/// he is not to be had.
	/// </summary>
	public static void Join(CGameState state, CCountry country, CUnit unit, CUnit target, int province = 0)
	{
		Place(state, country, unit, province != 0 ? province : target.Location);
		country.Units.Remove(unit);
		if (unit.SubUnits.Count == 1 && Service(unit) == Service(target))
		{
			CSubUnit subUnit = unit.SubUnits[0];
			unit.SubUnits.Clear();
			subUnit.Unit = target;
			target.SubUnits.Add(subUnit);
			if (state.Played.Contains(country.Tag))
			{
				state.RaisePactMessage(new Agreements.CPactMessage("TRANSFEREDSUBUNIT", "", "", country.Tag, country.Tag)
				{
					Told = country.Tag,
					Province = target.Location,
					Values = new[] { ("SUB", subUnit.Name), ("OLD", unit.Name), ("NEW", target.Name) },
				});
			}
			if (target.Leader is null && country.AutoAssignLeaders)
			{
				CLeaders.AssignBest(state, country, target);
			}
			CLeaders.SetLeader(state, unit, null, false, false);
			return;
		}
		unit.Parent = target;
		target.Children.Add(unit);
	}

	/// <summary>
	/// A finished unit joining the unit its line was ordered for, where that may take its brigade or is a theatre that may take
	/// it: at that unit's province, or else the nearest place in the home area; with none, or a unit that may not, it stays
	/// waiting. See <c>docs/mechanics/upgrades.md</c>, section 4.
	/// </summary>
	public static void JoinOrdered(CGameState state, CCountry country, CUnit unit, CUnit target)
	{
		bool mayTake = unit.SubUnits.Count != 0 && CSubUnitTransfer.MayTake(state, country, target, unit.SubUnits[0], sameProvince: false);
		if (!mayTake && !(target.Kind == EUnitKind.Theatre && MayTakeSubordinate(state, target, unit)))
		{
			return;
		}
		int province = PlaceToJoin(state, country, unit, target);
		if (province == 0)
		{
			return;
		}
		Join(state, country, unit, target, province);
	}

	/// <summary>
	/// Where a waiting unit joining one in the field is put, as the original's join puts it: the other's province where it
	/// may be placed there, else the nearest province of the home area where it may; nought where there is none.
	/// </summary>
	public static int PlaceToJoin(CGameState state, CCountry country, CUnit unit, CUnit target)
	{
		int province = target.Location;
		if (province > 0 && MayPlace(state, country, unit, province))
		{
			return province;
		}
		province = 0;
		long nearest = long.MaxValue;
		foreach (int place in Places(state, country, unit))
		{
			long distance = target.Location <= 0 ? 0
				: CMapDistance.Between(state.Province(target.Location), state.Province(place), state.MapWidth);
			if (distance < nearest || (distance == nearest && place < province))
			{
				nearest = distance;
				province = place;
			}
		}
		return province;
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
