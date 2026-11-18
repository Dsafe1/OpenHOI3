using System;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What one of the agent's provinces is worth holding, in thousandths: its forts where an enemy is near, its bases,
/// its victory points, the capital, a strategic resource and its IC - and, asked for a hold position, its depot, its
/// revolt risk and the ships in its port. The second term of the original's
/// <c>CAIUnit::EstimateAreaNeed</c>. See <c>docs/mechanics/agents.md</c>, section
/// 7.5.
/// </summary>
public static class CProvinceHoldValue
{
	private const long Whole = 1000;

	/// <summary>Below this infrastructure a province is worth nothing.</summary>
	private const long LeastInfrastructure = 200;

	/// <summary>A neighbour's threat towards the country, with its antagonism, makes it near from this.</summary>
	private const int NearThreat = 100;

	/// <summary>What the United States values while Britain stands, its lost core victory points under this.</summary>
	private const long FewLost = 2000;

	private const int SmallArea = 15;

	public static long Of(CGameState state, CCountry country, CAgent agent, CProvince province, bool hold)
	{
		return Of(state, country, agent, province, hold, Context(state, country));
	}

	/// <summary>What the value asks of the country alone, worked out once for a run of provinces.</summary>
	public static CHoldContext Context(CGameState state, CCountry country)
	{
		return new CHoldContext(state, CCountryLists.Enemies(state, country).Count == 0, IsFull(state, country));
	}

	public static long Of(CGameState state, CCountry country, CAgent agent, CProvince province, bool hold,
		CHoldContext context)
	{
		if (CSupplyFigures.EffectiveInfrastructure(province) < LeastInfrastructure)
		{
			return 0;
		}
		bool near = IsNear(state, country, province, hold, context.AtPeace);
		bool full = context.Full;
		long value = near ? 3 * Levels(province.Modifier[CCombatModifiers.FortLevel].Raw) * Whole : 0;
		long coastal = Levels(province.Modifier[CCombatModifiers.CoastalFortLevel].Raw);
		if (full && coastal > 0)
		{
			value += 5 * (coastal + 1) * Whole;
		}
		value += Bases(state, country, province, full);
		value += Points(state, country, province);
		if (province.Id == country.ActingCapital)
		{
			value += 100 * Whole;
		}
		if (full && province.StrategicResource.Length > 0)
		{
			value += 60 * Whole;
		}
		long ic = province.Modifier["ic"].Raw / 2;
		if (!hold)
		{
			value += ic;
		}
		else
		{
			CSupplyNetwork network = CSupplyNetworks.Refresh(state);
			if (network.DepotOf(province.Id) == province.Id)
			{
				value += 100 * Whole;
			}
			value += CRevoltRisk.Shown(state, province) * ic / Whole;
			if (full && province.PortSea > 0 && CTheatreFigures.NavalBase(state) is { } naval
				&& province.BuildingMaxLevel(naval).Raw > 0)
			{
				value += (30 + 2 * context.ShipsBasedAt(province.Id)) * Whole;
			}
		}
		foreach (CObjective objective in agent.Unit?.Plan.Objectives ?? new())
		{
			if (objective.Province == province.Id)
			{
				value += objective.Priority * Whole / 4;
				break;
			}
		}
		return value;
	}

	private static long Levels(long raw) => raw / Whole;

	/// <summary>A neighbour by land whose holder is no friend and either threatens the country or is front-worthy to it.</summary>
	private static bool IsNear(CGameState state, CCountry country, CProvince province, bool hold, bool atPeace)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea || beside.Controller is not { } holder || CCountryLists.IsFriend(state, country, holder, true))
			{
				continue;
			}
			int threat = state.Country(holder)?.Strategy.Threat.WeightOf(country.Tag) ?? 0;
			if (atPeace && country.UnitAi.RunsUnits && !hold)
			{
				threat += country.Strategy.Antagonize.WeightOf(holder);
			}
			if (threat >= NearThreat || CFrontWorthy.Is(state, country, holder))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Everything counts but for the United States at war while Britain stands and its own cores are nearly whole.</summary>
	private static bool IsFull(CGameState state, CCountry country)
	{
		if (country.Tag.Value != "USA" || CCountryLists.Enemies(state, country).Count == 0)
		{
			return true;
		}
		if (state.Country(new CCountryTag("ENG")) is not { } britain || britain.InExile
			|| CCountryLists.OwnedCount(state, britain.Tag) == 0)
		{
			return true;
		}
		return country.OwnedCoreVictoryPoints - country.HeldCoreVictoryPoints >= FewLost;
	}

	/// <summary>The air base, and the naval base on Germany's and Italy's curve; twice outside a holder's capital area in a small area.</summary>
	private static long Bases(CGameState state, CCountry country, CProvince province, bool full)
	{
		if (!full)
		{
			return 0;
		}
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
				long x = 10 * level + 30 * Whole;
				bases += x > 100 * Whole ? (x - 100 * Whole) / 5 + 90 * Whole : x;
			}
			else
			{
				bases += level;
			}
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? area = network.AreaOf(province.Id);
		CCountry? holder = state.Country(province.Controller);
		CSupplyArea? capitalArea = holder is null ? null : network.AreaOf(holder.ActingCapital);
		if (area is not null && !ReferenceEquals(area, capitalArea) && area.Provinces.Count < SmallArea)
		{
			bases *= 2;
		}
		return bases;
	}

	/// <summary>Owned: twice the points and five, or the points' share of all the country owns in per cent, the greater; held only: the first.</summary>
	private static long Points(CGameState state, CCountry country, CProvince province)
	{
		long points = province.Points;
		if (points <= 0)
		{
			return 0;
		}
		long plain = (2 * points + 5) * Whole;
		if (province.Owner == country.Tag)
		{
			long owned = country.OwnedVictoryPoints;
			long share = owned > 0 ? points * 100 * Whole * Whole / owned : long.MaxValue;
			return Math.Max(plain, share);
		}
		if (province.Controller == country.Tag && state.Country(province.Owner) is { InExile: false })
		{
			return plain;
		}
		return 0;
	}
}
