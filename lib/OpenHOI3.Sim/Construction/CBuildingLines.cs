using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// What is a building line's own in the build queue: whether a country may build a building and where, its price, the
/// day's test of whether it may still be built, finishing it and placing what waits in the deploy menu. See
/// <c>docs/mechanics/buildinglines.md</c>.
/// </summary>
public static class CBuildingLines
{
	private const long Whole = CFixedPoint.Scale;

	/// <summary>The rebels' tag, which may keep a building line while owning nothing.</summary>
	private const string Rebels = "REB";

	/// <summary>
	/// Whether a building is available to a country: a technology it has at a level of at least one names it in
	/// <c>activate_building</c> (*inferred*, as a unit's is).
	/// </summary>
	public static bool IsAvailable(CGameState state, CCountry country, CBuilding building)
	{
		foreach (CTechnology technology in state.Technologies.Values)
		{
			if (technology.ActivatesBuilding.Equals(building.Key, StringComparison.OrdinalIgnoreCase)
				&& country.TechnologyLevels.GetValueOrDefault(technology.Name) >= 1)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether a province has a port, as a <c>port</c> building asks.</summary>
	public static bool HasPort(CProvince province) => province.PortSea > 0;

	/// <summary>How many of a building a country's queue holds for a province, its lines' sizes together.</summary>
	public static int Queued(CCountry country, CBuilding building, int province)
	{
		int queued = 0;
		foreach (CConstruction line in country.Constructions)
		{
			if (line.Building == building && line.Location == province)
			{
				queued += line.Size;
			}
		}
		return queued;
	}

	/// <summary>
	/// The original's <c>CCountry::IsBuildingAllowed</c>: available to the country; the country holds the province - not
	/// asked for the underground; built and queued below <c>max_level</c>; a port where it needs one; none of what
	/// excludes it and all of what it needs.
	/// </summary>
	public static bool IsAllowed(CGameState state, CCountry country, CBuilding building, CProvince province)
	{
		if (!IsAvailable(state, country, building))
		{
			return false;
		}
		bool underground = CUndergrounds.Building(state) == building;
		if (!underground && province.Controller != country.Tag)
		{
			return false;
		}
		long built = province.BuildingMaxLevel(building).Raw / Whole;
		if (built + Queued(country, building, province.Id) >= building.MaxLevel)
		{
			return false;
		}
		if (building.IsPort && !HasPort(province))
		{
			return false;
		}
		foreach (CBuilding excluding in building.Excluding)
		{
			if (province.BuildingMaxLevel(excluding) > CFixedPoint.Zero)
			{
				return false;
			}
		}
		foreach (CBuilding needed in building.Prerequisites)
		{
			if (province.BuildingMaxLevel(needed) <= CFixedPoint.Zero)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// The original's <c>CBuildingDeployment::CanDeployIn</c>: a port where one is needed, and allowed; the underground by
	/// its own rules; anything else only in a province the country holds, a naval base only where there is none and an
	/// air base only where there is less than one level.
	/// </summary>
	public static bool CanDeployIn(CGameState state, CCountry country, CBuilding building, CProvince province)
	{
		if (building.IsPort && !HasPort(province))
		{
			return false;
		}
		if (!IsAllowed(state, country, building, province))
		{
			return false;
		}
		if (CUndergrounds.Building(state) == building)
		{
			return CUndergrounds.CanPlaceIn(state, province, country);
		}
		if (province.Controller != country.Tag)
		{
			return false;
		}
		if (building.Key.Equals(CBaseCrowding.NavalBase, StringComparison.OrdinalIgnoreCase))
		{
			return province.BuildingMaxLevel(building) <= CFixedPoint.Zero;
		}
		if (building.Key.Equals(CBaseCrowding.AirBase, StringComparison.OrdinalIgnoreCase))
		{
			return province.BuildingMaxLevel(building) < CFixedPoint.One;
		}
		return true;
	}

	/// <summary>
	/// The original's <c>CCountry_CanTrainUnderground</c>, which the production screen's underground button and an
	/// unplaced underground line ask: at war, not in exile, and one province with no underground held by an enemy that
	/// is not the rebels and owned by a friend - an ally's, for a major or a country with industry, and then its own.
	/// </summary>
	public static bool MayTrainUnderground(CGameState state, CCountry country)
	{
		if (!state.IsAtWar(country.Tag) || country.InExile)
		{
			return false;
		}
		bool large = country.IsMajor || country.MaxIc * 1000L > 100;
		var allies = CCountryLists.Allies(state, country);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is not { } owner)
			{
				continue;
			}
			bool ours = owner == country.Tag;
			if (!ours && !(large && allies.Exists(ally => ally.Tag == owner)))
			{
				continue;
			}
			if (CUndergrounds.Has(state, province) || province.Controller is not { } holder || holder == country.Tag
				|| holder.Value == Rebels || !CCountryLists.AtWar(state, holder, country.Tag)
				|| !CCountryLists.IsFriend(state, country, owner, withAccess: false))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// The original's <c>CBuildingConstruction::RecalculateCost</c>: the building's cost and time, each times one less
	/// the country's discount in its <c>on_completion</c> category, the cost truncated and the days whole.
	/// </summary>
	public static void Price(CCountry country, CConstruction line)
	{
		if (line.Building is not { } building)
		{
			return;
		}
		long discount = CBuildCosts.CategoryDiscount(country, building.OnCompletion);
		line.Cost = new CFixedPoint(building.Cost.Raw * (Whole - discount) / Whole);
		long days = building.Time.Raw * (Whole - discount) / Whole / Whole;
		line.Duration = CFixedPoint.FromInt((int)days);
	}

	/// <summary>
	/// The building line's own test, asked first each day: a line in a province - not the underground - the country no
	/// longer holds; an unplaced underground the country may no longer train; an underground whose province has one or may
	/// no longer take one. Such a line is taken out with nothing said.
	/// </summary>
	public static bool MayStillBeBuilt(CGameState state, CCountry country, CConstruction line)
	{
		if (line.Building is not { } building)
		{
			return true;
		}
		bool underground = CUndergrounds.Building(state) == building;
		if (!underground)
		{
			return line.Location == 0 || state.Province(line.Location).Controller == country.Tag;
		}
		if (line.Location == 0)
		{
			return MayTrainUnderground(state, country);
		}
		CProvince province = state.Province(line.Location);
		return !CUndergrounds.Has(state, province) && CUndergrounds.CanPlaceIn(state, province, country);
	}

	/// <summary>
	/// The original's <c>CBuildingConstruction::Deliver</c>, for a country that owns land: the practical in full; with no
	/// province a building waiting in the deploy menu, telling nobody; with one, a level of it built; then the progress
	/// less the days, one fewer to build, and priced again for the next. Answers whether the line is done with.
	/// </summary>
	public static bool Finish(CGameState state, CCountry country, CConstruction line)
	{
		if (line.Building is not { } building)
		{
			return true;
		}
		if (building.OnCompletion.Length > 0)
		{
			CResearch.GainAbility(state, country, building.OnCompletion, building.CompletionSize);
		}
		if (building.IsCapital && line.Location == 0)
		{
			line.Placed++;
			country.BuildingDeployments.Add(new CBuildingDeployment(line.Id + line.Placed, country.Tag, building));
		}
		else if (line.Location != 0)
		{
			CProvince province = state.Province(line.Location);
			Build(state, province, building, country, province.Owner, province.Owner);
		}
		CFixedPoint spent = line.Progress < line.Duration ? line.Progress : line.Duration;
		line.Progress -= spent;
		line.Size--;
		if (line.Size > 0)
		{
			Price(country, line);
		}
		return line.Size <= 0;
	}

	/// <summary>
	/// One more level of a building built in a province and none of it working - the day's repair fills it - at most its
	/// <c>max_level</c>; the underground starting with its strength and its owner; and <c>BUILDING_DONE</c> to the
	/// province's holder. The two countries the message is shown with are the caller's.
	/// </summary>
	public static void Build(CGameState state, CProvince province, CBuilding building, CCountry builder, CCountryTag? first,
		CCountryTag? second)
	{
		bool underground = CUndergrounds.Building(state) == building;
		bool hadUnderground = underground && CUndergrounds.Has(state, province);
		CFixedPoint most = province.BuildingMaxLevel(building) + CFixedPoint.One;
		CFixedPoint cap = CFixedPoint.FromInt(building.MaxLevel);
		province.SetBuildingMaxLevel(building, most > cap ? cap : most);
		if (underground && !hadUnderground)
		{
			CUndergrounds.Begin(state, province, builder);
		}
		if (province.Controller is { } holder)
		{
			state.RaisePactMessage(new CPactMessage("BUILDING_DONE", "", "", first ?? holder, second ?? holder)
			{
				Told = holder,
				Province = province.Id,
				Values = new[] { ("NAME", building.Key), ("PROV", province.Name) },
			});
		}
	}

	/// <summary>
	/// The original's <c>CBuildingDeployment::DeployAt</c>: a level built as a finished line builds one, the message's two
	/// countries the holder and the owner; and the deployment gone from the menu. It asks nothing of the province.
	/// </summary>
	public static void Place(CGameState state, CCountry country, CBuildingDeployment deployment, CProvince province)
	{
		country.BuildingDeployments.Remove(deployment);
		Build(state, province, deployment.Building, country, province.Controller, province.Owner);
	}
}
