using System;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// How much a province wants a garrison, as a plan counts it, truncated to a whole number: 25 or more counts. The original's
/// <c>CUnitPlan_ProvinceHoldScore</c>; a sibling of <see cref="CProvinceHoldValue"/> and not the same in its figures. See
/// <c>docs/mechanics/theatresscreen.md</c>, section 6.3.
/// </summary>
public static class CPlanHoldScore
{
	private const long Whole = 1000;
	private const long LeastInfrastructure = 200;
	private const int NearThreat = 100;
	private const long BelgianBorder = 4000;
	private const int SmallArea = 15;
	private const string InfrastructureBuilding = "infra";
	private const string Plains = "plains";

	public static int Of(CGameState state, CUnit unit, CProvince province)
	{
		if (state.Country(unit.Owner) is not { } country || CMovementRules.BuildingLevel(state, province, InfrastructureBuilding).Raw / 10 < LeastInfrastructure)
		{
			return 0;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		bool isDepot = network.DepotOf(province.Id) == province.Id;
		bool atPeace = CCountryLists.Enemies(state, country).Count == 0;
		long sum = 0;
		long count = 0;
		foreach (CProvinceEdge edge in province.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (edge.Kind == EProvinceEdgeKind.Impassable || beside.IsSea || beside.Controller is not { } holder)
			{
				continue;
			}
			if (province.Points == 0 && !isDepot && CFrontWorthy.IsWarCandidate(state, country, holder))
			{
				return 0;
			}
			if (IsBelgianBorder(state, country, holder, beside))
			{
				sum += BelgianBorder;
				count++;
			}
			else if (!CCountryLists.IsFriend(state, country, holder, true))
			{
				long threat = Threat(state, country, holder, atPeace);
				if (threat >= NearThreat)
				{
					sum += threat;
					count++;
				}
			}
		}
		long value = count > 0 ? (sum * Whole / count / 20) + count * Whole : 0;
		if (HasNearNeighbour(state, country, province, atPeace))
		{
			value += 3 * province.Modifier[CCombatModifiers.FortLevel].Raw;
		}
		long coastal = province.Modifier[CCombatModifiers.CoastalFortLevel].Raw;
		if (coastal > 0)
		{
			value += 5 * (coastal + Whole);
		}
		value += Bases(state, country, province, network);
		value += Points(state, country, province);
		if (province.Id == country.ActingCapital)
		{
			value += 100 * Whole;
		}
		if (province.StrategicResource.Length > 0)
		{
			value += 60 * Whole;
		}
		long risk = Math.Max(Math.Max(CRevoltRisk.Shown(state, province), province.Modifier["minimum_revolt_risk"].Raw), 0);
		value += province.Modifier["ic"].Raw * risk / Whole;
		if (isDepot)
		{
			value += 100 * Whole;
		}
		if (CTheatreFigures.NavalBase(state) is { } naval && province.BuildingMaxLevel(naval).Raw > 0 && province.PortSea > 0)
		{
			value += (30 + 2 * Ships(state, province)) * Whole;
		}
		foreach (CObjective objective in unit.Plan.Objectives)
		{
			if (objective.Province == province.Id)
			{
				value += objective.Priority * Whole / 4;
				break;
			}
		}
		if (country.UnitAi.RunsUnits && province.Controller is { } controller)
		{
			value += (state.Country(controller)?.Strategy.DefendProvinces.WeightOf(province.Id) ?? 0) * Whole;
		}
		return (int)(value / Whole);
	}

	private static long Threat(CGameState state, CCountry country, CCountryTag holder, bool atPeace)
	{
		long threat = state.Country(holder)?.Strategy.Threat.WeightOf(country.Tag) ?? 0;
		if (atPeace && country.UnitAi.RunsUnits)
		{
			threat += country.Strategy.Antagonize.WeightOf(holder);
		}
		return threat;
	}

	/// <summary>France facing Belgium or Luxembourg, or Belgium facing Luxembourg, while Germany holds land and is neither of its faction nor at war there.</summary>
	private static bool IsBelgianBorder(CGameState state, CCountry country, CCountryTag holder, CProvince beside)
	{
		bool pair = (country.Tag.Value == "FRA" && holder.Value is "BEL" or "LUX") || (country.Tag.Value == "BEL" && holder.Value == "LUX");
		if (!pair || state.Country(holder) is not { } them || them.Overlord == country.Tag || state.Country(new CCountryTag("GER")) is not { } germany
			|| CCountryLists.OwnedCount(state, germany.Tag) == 0)
		{
			return false;
		}
		return (country.Faction.Length == 0 || germany.Faction != country.Faction) && !CCountryLists.IsEnemy(state, germany, holder);
	}

	private static bool HasNearNeighbour(CGameState state, CCountry country, CProvince province, bool atPeace)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (edge.Kind == EProvinceEdgeKind.Impassable || beside.IsSea || beside.Controller is not { } holder
				|| CCountryLists.IsFriend(state, country, holder, true))
			{
				continue;
			}
			if (Threat(state, country, holder, atPeace) >= NearThreat || CFrontWorthy.Is(state, country, holder))
			{
				return true;
			}
		}
		return false;
	}

	private static long Bases(CGameState state, CCountry country, CProvince province, CSupplyNetwork network)
	{
		long bases = 0;
		if (CTheatreFigures.AirBase(state) is { } air)
		{
			bases += province.BuildingMaxLevel(air).Raw;
		}
		if (CTheatreFigures.NavalBase(state) is { } naval && province.BuildingMaxLevel(naval).Raw > 0)
		{
			long level = province.BuildingMaxLevel(naval).Raw;
			if (country.Tag.Value is "GER" or "ITA")
			{
				long x = 2 * (5 * level + 15 * Whole);
				bases += x > 100 * Whole ? (x - 100 * Whole) / 5 + 90 * Whole : x;
			}
			else
			{
				bases += level;
			}
		}
		CSupplyArea? area = network.AreaOf(province.Id);
		CCountry? holder = state.Country(province.Controller);
		CSupplyArea? capitalArea = holder is null ? null : network.AreaOf(holder.ActingCapital);
		if (area is not null && !ReferenceEquals(area, capitalArea) && area.Provinces.Count < SmallArea)
		{
			bases *= 2;
		}
		return bases;
	}

	private static long Points(CGameState state, CCountry country, CProvince province)
	{
		long points = province.Points;
		if (points <= 0)
		{
			return 0;
		}
		long plain = (2 * points + 10) * Whole;
		if (province.Owner == country.Tag)
		{
			long owned = country.OwnedVictoryPoints;
			long share = owned > 0 ? points * 100 * Whole * Whole / owned : long.MaxValue;
			return Math.Max(share, plain);
		}
		return province.Controller == country.Tag && state.Country(province.Owner) is { InExile: false } ? plain : 0;
	}

	private static long Ships(CGameState state, CProvince province)
	{
		long ships = 0;
		foreach (CUnit fleet in state.Positions.Of(province.Id))
		{
			if (fleet.Kind == EUnitKind.Navy)
			{
				ships += fleet.SubUnits.Count;
			}
		}
		return ships;
	}
}
