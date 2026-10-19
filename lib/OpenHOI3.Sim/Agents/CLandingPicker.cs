using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Where an invasion comes ashore in an enemy's area: a port beside real sea, not the Black Sea's or the Baltic's, near
/// the army's priority provinces, lightly held and with lightly held enemy land behind it, reachable from a port of the
/// capital's area - the least score of the area's provinces. The original's <c>CEU3AI::PickInvasionLandingProvince</c>
/// and <c>CEU3AI::EstimateProvinceDefence</c>. See <c>docs/mechanics/moving.md</c>, section 16.
/// </summary>
public static class CLandingPicker
{
	private const long Whole = 1000;

	private const long Unreached = 1_000_000 * Whole;

	private const long HeavilyHeld = 5 * Whole;

	private const long StraitsOnly = 10_000 * Whole;

	private const long Taken = 1000 * Whole;

	public static int Pick(CGameState state, CCountry country, CSupplyArea area, CInvasion? invasion)
	{
		CBuilding? naval = CTheatreFigures.NavalBase(state);
		if (naval is null)
		{
			return 0;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		bool favoured = Favoured(state, country, network, area);
		var objectives = new List<int>();
		foreach (int id in country.UnitAi.PriorityProvinces)
		{
			if (ReferenceEquals(network.AreaOf(id), area))
			{
				objectives.Add(id);
			}
		}
		int best = 0;
		long least = Unreached;
		foreach (int id in area.Provinces)
		{
			CProvince p = state.Province(id);
			bool coastal = false;
			bool straitsOnly = true;
			long value = 0;
			long navalSum = 0;
			int n = 0;
			foreach (CProvinceEdge edge in p.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (beside.IsSea)
				{
					if (!IsLake(state, beside))
					{
						coastal = true;
					}
					continue;
				}
				if (network.AreaOf(edge.To) is null || beside.Controller is not { } holder || holder == country.Tag
					|| !(CCountryLists.IsEnemy(state, country, holder) || holder.Value == CRebelFactions.Rebels))
				{
					continue;
				}
				if (edge.Kind != EProvinceEdgeKind.Crossing)
				{
					straitsOnly = false;
				}
				navalSum += beside.BuildingMaxLevel(naval).Raw;
				if (p.PortSea > 0 && beside.PortSea == p.PortSea)
				{
					value += (2 * Whole - Defence(state, country, beside)) * 20;
					value += 2 * beside.Points * Whole;
					if (state.Country(holder)?.ActingCapital == beside.Id)
					{
						value += 10 * Whole;
					}
				}
				n++;
			}
			if (!coastal || p.BuildingMaxLevel(naval).Raw < Whole)
			{
				continue;
			}
			value += navalSum;
			if (p.PortSea > 0 && (InRegion(state, p.PortSea, "black_sea_region") || InRegion(state, p.PortSea, "baltic_sea_region")))
			{
				continue;
			}
			long defence = Defence(state, country, p);
			if (area.Provinces.Count > 1 && defence > HeavilyHeld && objectives.Count == 0)
			{
				continue;
			}
			long distance = 0;
			foreach (int aim in objectives)
			{
				distance += CMapDistance.Between(p, state.Province(aim), state.MapWidth) * Whole;
			}
			if (objectives.Count > 0)
			{
				distance /= objectives.Count;
			}
			if (objectives.Contains(id))
			{
				distance /= 10;
			}
			long score = distance - CConvoyRules.PortCapacity(state, p) / Whole * 10 * Whole - value / 2 - n * Whole
				- (2 * Whole - defence) * 20;
			if (straitsOnly && n > 0)
			{
				score += StraitsOnly;
			}
			score += defence * 34 - 2 * p.Points * Whole;
			if (favoured)
			{
				score = score < 0 ? score * 2 : score / 2;
				if (p.Owner?.Value == "FRA")
				{
					score = score < 0 ? score * 5 / 4 : score * 3 / 4;
				}
			}
			if (state.Country(p.Controller)?.ActingCapital == id)
			{
				score -= 20 * Whole;
			}
			foreach (CInvasion other in country.UnitAi.Invasions)
			{
				if (!ReferenceEquals(other, invasion) && other.Objective == id)
				{
					score += Taken;
				}
			}
			if (score >= least)
			{
				continue;
			}
			if (invasion is not null && !LeadMayEnter(state, invasion, p))
			{
				continue;
			}
			if (!FromCapital(state, country, network, p))
			{
				continue;
			}
			least = score;
			best = id;
		}
		return best;
	}

	/// <summary>The enemy's land units there, a third for a headquarters brigade, raised a fifth for each coastal fort level.</summary>
	public static long Defence(CGameState state, CCountry country, CProvince province)
	{
		long total = 0;
		foreach (CUnit unit in state.Positions.Of(province.Id))
		{
			if (!CTheatres.IsLand(unit) || !CCountryLists.IsEnemy(state, country, unit.Owner))
			{
				continue;
			}
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				long share = CFleetRegroup.StrengthShare(brigade);
				if (brigade.Type.Key == "hq_brigade")
				{
					share /= 3;
				}
				total += share;
			}
		}
		long forts = province.Modifier[CCombatModifiers.CoastalFortLevel].Raw;
		return total * (Whole + forts / 5) / Whole;
	}

	/// <summary>Germany's capital area, the invader a great power and the Soviet Union weak: preferred, France's provinces in it more.</summary>
	private static bool Favoured(CGameState state, CCountry country, CSupplyNetwork network, CSupplyArea area)
	{
		if (state.Country(new CCountryTag("GER")) is not { } germany || !ReferenceEquals(network.AreaOf(germany.ActingCapital), area)
			|| !Ai.CWarTargets.Prunes(country) || state.Country(new CCountryTag("SOV")) is not { } soviets)
		{
			return false;
		}
		return Wars.CSurrender.Progress(state, soviets) > 200 || soviets.Manpower.Raw < 100 * Whole;
	}

	private static bool IsLake(CGameState state, CProvince sea)
	{
		foreach (CProvinceEdge edge in sea.Edges)
		{
			if (state.Province(edge.To).IsSea)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>The invasion's first transport may enter a sea beside the province.</summary>
	private static bool LeadMayEnter(CGameState state, CInvasion invasion, CProvince p)
	{
		CUnit? lead = invasion.Fleets.FirstOrDefault(fleet => !fleet.IsRemoved && Transport.CTransport.Capacity(fleet) > 0);
		if (lead is null)
		{
			return false;
		}
		foreach (CProvinceEdge edge in p.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea && state.Country(lead.Owner) is { } owner
				&& Movement.CMovementRules.MayMoveTo(state, owner, lead, beside))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Some port of the country's capital's area reaches it by an open sea.</summary>
	private static bool FromCapital(CGameState state, CCountry country, CSupplyNetwork network, CProvince p)
	{
		if (network.AreaOf(country.ActingCapital) is not { } home)
		{
			return false;
		}
		foreach (int port in home.Ports)
		{
			if (CSeaTargets.Sea(state, port, p.Id) >= 0 && CSealiftPickers.IsSeaRouteOpen(state, country, port, p.Id))
			{
				return true;
			}
		}
		return false;
	}

	private static bool InRegion(CGameState state, int province, string name)
	{
		foreach (CRegion region in state.Regions)
		{
			if (region.Name == name)
			{
				return region.Provinces.Contains(province);
			}
		}
		return false;
	}
}
