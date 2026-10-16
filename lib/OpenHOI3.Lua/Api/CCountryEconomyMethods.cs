using System;
using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The methods of <c>CCountry</c> the tech and production scripts reach: research, leadership and its shares, the IC
/// and the queue, manpower, convoys, buildings, the underground target and desperation. See
/// <c>docs/mechanics/ministers.md</c>, section 7, and <c>reversing/findings/FINDINGS-ministers.md</c> A.6, C.5 and
/// D.1.
/// </summary>
public static class CCountryEconomyMethods
{
	private const string Name = "CCountry";

	private const string NavalBase = "naval_base";
	private const string AirBase = "air_base";
	private const string Underground = "underground";

	/// <summary>What an own province weighs as an underground target against an ally's.</summary>
	private const long OwnWeight = 3;

	/// <summary>An underground target's weight is this much of its infrastructure.</summary>
	private const long PerInfrastructure = 50;

	/// <summary>The underground target is picked at this place along the weights - the original's own figure, no draw.</summary>
	private const long UndergroundPick = 757147;

	public static CLuaClass Add(CLuaClass luaClass, CGameApi api)
	{
		return luaClass
			// Research and leadership (A.6).
			.Method("GetAllowedResearchSlots", call => call.Return(AllowedResearchSlots(Of(call))))
			.Method("GetNumberOfCurrentResearch", call => call.Return(Of(call).ResearchQueue.Count))
			.Method("GetCurrentResearch", call =>
			{
				var technologies = new List<object?>();
				foreach (string name in Of(call).ResearchQueue)
				{
					if (api.State.Technologies.TryGetValue(name, out CTechnology? technology))
					{
						technologies.Add(new CLuaObject(api.Technology, technology));
					}
				}
				return call.ReturnIterator(technologies);
			})
			.Method("GetTotalLeadership", call => call.Return(api.Fixed(Of(call).TotalLeadership)))
			.Method("GetOfficerRatio", call => call.Return(api.Fixed(Of(call).OfficerRatio)))
			.Method("GetDiplomaticInfluence", call => call.Return(api.Fixed(Of(call).DiplomaticInfluence)))
			.Method("CalculateNumberOfActiveInfluences", call =>
			{
				CCountry country = Of(call);
				return call.Return(api.State.Influences.FindAll(influence => influence.Influencer == country.Tag).Count);
			})
			.Method("GetNumOfPorts", call => call.Return(Ports(api, Of(call))))
			.Method("GetNumOfAirfields", call => call.Return(Airfields(api, Of(call))))
			// The IC and the queue (C.4, C.5).
			.Method("GetICPart", call =>
			{
				int category = call.Integer(2);
				if (category < 0 || category >= CProduction.CategoryCount)
				{
					throw new CLuaError($"bad argument #2 to 'GetICPart' (no category {category})");
				}
				return call.Return(api.Fixed(CProduction.Given(Of(call), (EProductionCategory)category)));
			})
			.Method("GetUsedIC", call =>
			{
				long used = 0;
				foreach (CConstruction line in Of(call).Constructions)
				{
					used += line.Cost.Raw;
				}
				return call.Return(api.Fixed(new CFixedPoint(used)));
			})
			.Method("GetConstructions", call =>
			{
				var lines = new List<object?>();
				foreach (CConstruction line in Of(call).Constructions)
				{
					lines.Add(new CLuaObject(api.Construction, line));
				}
				return call.ReturnIterator(lines);
			})
			.Method("GetBuildCostIC", call =>
			{
				CCountry country = Of(call);
				var order = new CBrigadeOrder(call.Object<CSubUnitType>(2, CLicenceActionClass.SubUnitName));
				CBuildQueue.SetModelToCountrys(country, order);
				bool reserve = call.Boolean(4);
				CSubUnit brigade = CBuildQueue.AsBuilt(order, reserve, out _);
				return call.Return(api.Fixed(new CFixedPoint(CBuildCosts.BuildCost(country, brigade, call.Integer(3)))));
			})
			.Method("GetBuildCost", call =>
			{
				CCountry country = Of(call);
				CBuilding building = call.Object<CBuilding>(2, CBuildingClass.Name);
				var line = new CConstruction(country.Tag) { Building = building };
				CBuildingLines.Price(country, line);
				return call.Return(api.Fixed(line.Cost));
			})
			.Method("IsBuildingAllowed", call => call.Return(CBuildingLines.IsAllowed(api.State, Of(call),
				call.Object<CBuilding>(2, CBuildingClass.Name), call.Object<CProvince>(3, "CProvince"))))
			// Manpower and units (C.5).
			.Method("HasExtraManpowerLeft", call => call.Return(Of(call).SpareManpower.Raw > 0))
			.Method("CountMaxUnitsStillBuildable", call => call.Return((int)CUnitAvailability.StillBuildable(api.State, Of(call),
				call.Object<CSubUnitType>(2, CLicenceActionClass.SubUnitName))))
			.Method("GetDissent", call => call.Return(api.Fixed(Of(call).Dissent)))
			.Method("CalcDesperation", call => call.Return(api.Fixed(new CFixedPoint(CDesperation.Of(api.State, Of(call))))))
			// Lend-lease (B.2).
			.Method("HasActiveLendLeaseToAnyone", call => call.Return(Of(call).LendLeaseGiving.Count > 0))
			.Method("GetMaxLendLeaseFraction", call => call.Return(api.Fixed(MaxLendLeaseFraction(api, Of(call)))))
			// Convoys (C.5).
			.Method("GetEscorts", call => call.Return(Of(call).Escorts))
			.Method("GetTotalConvoyTransports", call => call.Return(Of(call).TransportsAtSharing))
			.Method("GetTotalNeededConvoyTransports", call =>
			{
				CCountry country = Of(call);
				return call.Return(country.TransportsOnConvoysAtSharing + country.TransportsNeeded);
			})
			.Method("GetConvoyBuildCost", call => call.Return(api.Fixed(CConvoyLines.Cost(api.State, Of(call), escorts: false))))
			.Method("GetEscortBuildCost", call => call.Return(api.Fixed(CConvoyLines.Cost(api.State, Of(call), escorts: true))))
			// Provinces and buildings (D.1).
			.Method("GetControlledProvinces", call =>
			{
				CCountry country = Of(call);
				var provinces = new List<object?>();
				foreach (CProvince province in api.State.Provinces)
				{
					if (province.Controller == country.Tag)
					{
						provinces.Add(province.Id);
					}
				}
				return call.ReturnIterator(provinces);
			})
			.Method("GetTotalCoreBuildingLevels", call =>
			{
				List<CFixedPoint> levels = Of(call).CoreBuildingLevels;
				int index = call.Integer(2) - 1;
				return call.Return(api.Fixed(index >= 0 && index < levels.Count ? levels[index] : CFixedPoint.Zero));
			})
			.Method("GetRandomUnderGroundTarget", call => call.Return(UndergroundTarget(api, Of(call))));
	}

	/// <summary>
	/// The research share's part of the total leadership, rounded to the nearest whole, half to even - the share first
	/// turned into thousandths, rounded down.
	/// </summary>
	public static int AllowedResearchSlots(CCountry country)
	{
		long share = country.LeadershipShares[(int)ELeadershipCategory.Research].ToThousandths().Raw;
		long leadership = share * country.TotalLeadership.Raw / CFixedPoint.Scale;
		return (int)Math.Round(leadership / 1000.0, MidpointRounding.ToEven);
	}

	/// <summary>Owned provinces with a port and a naval base built, in the acting capital's area only.</summary>
	private static int Ports(CGameApi api, CCountry country)
	{
		CBuilding? naval = Find(api, NavalBase);
		if (naval is null || country.ActingCapital <= 0)
		{
			return 0;
		}
		int ports = 0;
		foreach (int id in CSupplyNetworks.AreaAround(api.State, country.ActingCapital))
		{
			CProvince province = api.State.Province(id);
			if (province.Owner == country.Tag && CBuildingLines.HasPort(province) && province.BuildingMaxLevel(naval).Raw > 0)
			{
				ports++;
			}
		}
		return ports;
	}

	/// <summary>Controlled provinces with an air base built.</summary>
	private static int Airfields(CGameApi api, CCountry country)
	{
		CBuilding? air = Find(api, AirBase);
		int airfields = 0;
		foreach (CProvince province in api.State.Provinces)
		{
			if (air is not null && province.Controller == country.Tag && province.BuildingMaxLevel(air).Raw > 0)
			{
				airfields++;
			}
		}
		return airfields;
	}

	/// <summary>
	/// The lend-lease a country may give as a share of its IC: nought where it gives none; else the low define and its
	/// effective neutrality's part of the way to the high one, a neutrality at the limit or more giving the low.
	/// </summary>
	private static CFixedPoint MaxLendLeaseFraction(CGameApi api, CCountry country)
	{
		if (country.LendLeaseGiving.Count == 0)
		{
			return CFixedPoint.Zero;
		}
		const string section = Sim.Agreements.COfferRules.Section;
		long limit = api.State.Defines[section, Sim.Aid.CLendLease.NeutralityLimitDefine].Raw;
		if (limit == 0)
		{
			limit = 10;
		}
		long n = Math.Clamp(country.EffectiveNeutrality.Raw * CFixedPoint.Scale / limit, 0, CFixedPoint.Scale);
		long low = api.State.Defines[section, "LEND_LEASE_MAX_IC_LOW"].Raw;
		long high = api.State.Defines[section, "LEND_LEASE_MAX_IC_HIGH"].Raw;
		return new CFixedPoint(low + (high - low) * (CFixedPoint.Scale - n) / CFixedPoint.Scale);
	}

	/// <summary>
	/// <c>GetRandomUnderGroundTarget</c>, which draws nothing: the provinces an underground may be placed in - an ally's
	/// owned land first, for a major or a country of over 100 IC, then its own - each weighing a twentieth of its
	/// infrastructure, three times over for its own; the one at <c>757147 mod</c> the weights' sum along them. Nought
	/// where none weighs anything. A province with an underground line queued stays a candidate.
	/// </summary>
	private static int UndergroundTarget(CGameApi api, CCountry country)
	{
		CBuilding? underground = Find(api, Underground);
		var candidates = new List<(int Province, long Weight)>();
		void Look(CCountry owner, long times)
		{
			foreach (CProvince province in api.State.Provinces)
			{
				if (province.Owner != owner.Tag || underground is null || province.BuildingMaxLevel(underground).Raw > 0
					|| province.Controller == country.Tag || !CUndergrounds.CanPlaceIn(api.State, province, country, requireEmpty: false))
				{
					continue;
				}
				long weight = CSupplyFigures.EffectiveInfrastructure(province) * PerInfrastructure / CFixedPoint.Scale;
				candidates.Add((province.Id, weight * times));
			}
		}
		if (country.IsMajor || country.MaxIc * 1000L > 100)
		{
			foreach (CCountry ally in CCountryLists.Allies(api.State, country))
			{
				Look(ally, 1);
			}
		}
		Look(country, OwnWeight);
		long total = 0;
		foreach ((int _, long weight) in candidates)
		{
			total += weight;
		}
		if (total <= 0)
		{
			return 0;
		}
		long pick = UndergroundPick % total;
		long running = 0;
		foreach ((int province, long weight) in candidates)
		{
			if (pick >= running && pick < running + weight)
			{
				return province;
			}
			running += weight;
		}
		return candidates[^1].Province;
	}

	private static CBuilding? Find(CGameApi api, string key)
	{
		foreach (CBuilding building in api.State.Buildings)
		{
			if (string.Equals(building.Key, key, StringComparison.Ordinal))
			{
				return building;
			}
		}
		return null;
	}

	private static CCountry Of(CLuaCall call) => call.Object<CCountry>(1, Name);
}
