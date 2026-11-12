using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Where and with what the AI invades: the enemy's coastal areas it can reach ranked by what they hold and how far they
/// lie, those touching its own land or out of reach failed for the month; then, area by area, a force sized from the
/// enemy's units there, a landing picked, spare fit divisions nearest by sea and transports and escorts enough to carry
/// and cover them, made into an invasion; and invasions whose area dropped out of the ranking given up. The original's
/// <c>CEU3AI_RankInvasionAreas</c>, <c>CEU3AI_MayInvadeArea</c>, <c>CEU3AI::PlanAmphibiousInvasion</c> and
/// <c>CEU3AI_PruneInvasions</c>. See <c>docs/mechanics/moving.md</c>, section 16.
/// </summary>
public static class CInvasionPlanner
{
	private const long Whole = 1000;

	private const int Kept = 10;

	private const long NearRange = 2000;

	private const long FarRange = 5000;

	private const long PortRadius = 160_000;

	private const long SupplyMargin = 2000;

	private const long ReadyOrganisation = 800;

	private const long ReadyStrength = 750;

	// ---- the ranking

	public static void Rank(CGameState state, CCountry country)
	{
		CUnitAi ai = country.UnitAi;
		ai.RankedAreas.Clear();
		if (CSealift.SumCapacity(country) < 4 * InfantryWeight(state))
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		bool capitalThreatened = capitalArea is not null && CAreaHostility.TouchesHostileArea(state, capitalArea);
		long range = capitalThreatened ? NearRange : FarRange;
		var reach = new List<CSupplyArea>();
		foreach (CSupplyArea area in network.Areas)
		{
			if (area.Controller == country.Tag
				|| (!capitalThreatened && CCountryLists.FightingTogether(state, country.Tag, area.Controller)))
			{
				reach.Add(area);
			}
		}
		var scored = new List<(CSupplyArea, long)>();
		foreach (CCountry enemy in CCountryLists.Enemies(state, country))
		{
			if (country.Tag.Value == "GER" && capitalThreatened && enemy.Tag.Value is not ("NOR" or "SWE" or "FIN" or "DEN"))
			{
				continue;
			}
			if (country.Faction.Length > 0 && enemy.Faction.Length > 0 && enemy.Tag.Value == "FIN")
			{
				continue;
			}
			foreach (CSupplyArea area in network.Areas)
			{
				if (area.Controller != enemy.Tag || area.IsLandlocked || ai.FailedAreas.Contains(area))
				{
					continue;
				}
				if (country.Faction.Length > 0 && enemy.Faction.Length > 0 && enemy.Tag.Value is "ROM" or "BUL" or "YUG"
					&& ReferenceEquals(area, network.AreaOf(enemy.ActingCapital)))
				{
					continue;
				}
				if (TouchesUs(country, area) || !MayInvade(state, country, area)
					|| !Reachable(state, country, area, reach, range))
				{
					ai.FailedAreas.Add(area);
					continue;
				}
				scored.Add((area, Score(state, country, network, enemy, area, capitalArea)));
			}
		}
		scored.Sort((a, b) => b.Item2.CompareTo(a.Item2));
		foreach ((CSupplyArea area, long score) in scored.Take(Kept))
		{
			ai.RankedAreas.Add((area, score));
		}
		if (ai.RankedAreas.Count > 0)
		{
			long floor = ai.RankedAreas[0].Score / 10;
			ai.RankedAreas.RemoveAll(entry => entry.Score < floor);
		}
	}

	private static bool TouchesUs(CCountry country, CSupplyArea area)
	{
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (beside.Controller == country.Tag && !beside.IsLandlocked)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Some port of the area reaches a port of the land the country may sail from, within the range, by an open sea.</summary>
	private static bool Reachable(CGameState state, CCountry country, CSupplyArea area, List<CSupplyArea> reach, long range)
	{
		foreach (int port in area.Ports)
		{
			foreach (CSupplyArea from in reach)
			{
				foreach (int home in from.Ports)
				{
					long distance = CSeaTargets.Sea(state, port, home);
					if (distance >= 0 && distance <= range && CSealiftPickers.IsSeaRouteOpen(state, country, port, home))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	/// <summary>The area's worth: its provinces' bases, points and resources, its capital or its smallness; a draw; over its distance.</summary>
	private static long Score(CGameState state, CCountry country, CSupplyNetwork network, CCountry enemy, CSupplyArea area,
		CSupplyArea? capitalArea)
	{
		CBuilding? naval = CTheatreFigures.NavalBase(state);
		long ports = 0;
		foreach (int port in area.Ports)
		{
			ports += naval is null ? 0 : state.Province(port).BuildingMaxLevel(naval).Raw / Whole;
		}
		bool island = ports >= 1 && area.Provinces.Count < 5 && area.Neighbours.Count == 0;
		long f;
		if (area.Provinces.Exists(country.UnitAi.PriorityProvinces.Contains))
		{
			f = 100001;
		}
		else if (island)
		{
			f = country.Tag.Value == "GER" && state.Date.Year < 1944 ? -99 : 10 * ports + 51;
		}
		else if (ReferenceEquals(area, network.AreaOf(enemy.ActingCapital)))
		{
			f = System.Math.Min(area.Provinces.Count, 100) + 1;
			if (enemy.Overlord is not null)
			{
				f /= 2;
			}
		}
		else
		{
			f = 1;
		}
		long g = 0;
		int counted = 0;
		foreach (int id in area.Provinces)
		{
			if (counted++ >= 60)
			{
				break;
			}
			CProvince p = state.Province(id);
			g += 8 * (naval is null ? 0 : p.BuildingMaxLevel(naval).Raw / Whole);
			g += p.Points > 0 ? 40 : 0;
			g += p.StrategicResource.Length > 0 ? 40 : 0;
			g += p.Owner == country.Tag ? 5 : 0;
		}
		if (area.Provinces.Count > 20)
		{
			g = g * 20 / area.Provinces.Count;
		}
		long total = g + f;
		long k = System.Math.Max((total + 2) / 5, 5);
		long score = total + state.AiRandom.Next() % k;
		if (capitalArea is not null)
		{
			long distance = PortDistance(state, area, capitalArea) / 200;
			if (distance > 0)
			{
				score = score / distance;
			}
		}
		return score;
	}

	private static long PortDistance(CGameState state, CSupplyArea a, CSupplyArea b)
	{
		long least = 1_000_000;
		foreach (int one in a.Ports)
		{
			foreach (int other in b.Ports)
			{
				least = System.Math.Min(least, CMapDistance.Between(state.Province(one), state.Province(other), state.MapWidth));
			}
		}
		return least;
	}

	/// <summary>The Allies may not land in an Axis-style major's capital area until a neighbour of its fights it or assault ships are theirs.</summary>
	public static bool MayInvade(CGameState state, CCountry country, CSupplyArea area)
	{
		if (area.Provinces.Count > 0 && state.Province(area.Provinces[0]).Owner == country.Tag)
		{
			return true;
		}
		List<CCountry> members = CCountryLists.FactionMembers(state, country.Faction);
		if (members.Count > 0 && !ReferenceEquals(members[0], country))
		{
			if (country.MaxIc < 50 || CSealift.SumCapacity(country) <= 0)
			{
				return false;
			}
		}
		if (state.Country(area.Controller) is not { } holder || !Ai.CWarTargets.Prunes(holder) || holder.Faction.Length == 0
			|| holder.Tag.Value == "JAP" || country.Faction != "allies")
		{
			return true;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (!ReferenceEquals(network.AreaOf(holder.ActingCapital), area))
		{
			return true;
		}
		foreach (CCountry other in CCountryLists.Neighbours(state, holder))
		{
			if (!ReferenceEquals(other, country) && !other.InExile && CCountryLists.AtWar(state, other.Tag, holder.Tag))
			{
				return true;
			}
		}
		return false;
	}

	// ---- the planning

	public static void Plan(CGameState state, CCountry country)
	{
		CUnitAi ai = country.UnitAi;
		int cap = country.Tag.Value switch { "JAP" => 8, "USA" => 4, _ => 2 };
		if (ai.Invasions.Count >= cap || ai.RankedAreas.Count == 0 || Ai.CWarTargets.IsPreparingWar(state, country))
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var invaded = new HashSet<CSupplyArea>();
		foreach (CInvasion invasion in ai.Invasions)
		{
			if (network.AreaOf(invasion.Objective) is { } area)
			{
				invaded.Add(area);
			}
		}
		if (ai.RankedAreas.TrueForAll(entry => invaded.Contains(entry.Area)))
		{
			return;
		}
		List<CUnit> divisions = Divisions(state, country, network);
		if (divisions.Count == 0)
		{
			return;
		}
		var fleets = new List<CUnit>();
		long lift = 0;
		foreach (CUnit fleet in country.UnitsInOrder())
		{
			if (fleet.Kind == EUnitKind.Navy && !fleet.IsRemoved && fleet.Invasion is null && fleet.Passengers.Count == 0)
			{
				fleets.Add(fleet);
				lift += CTransport.Capacity(fleet);
			}
		}
		long reference = 4 * InfantryWeight(state);
		foreach ((CSupplyArea area, long _) in ai.RankedAreas)
		{
			if (ai.Invasions.Count >= cap || lift < reference)
			{
				break;
			}
			if (invaded.Contains(area) || Skipped(state, country, area))
			{
				continue;
			}
			int enemies = 0;
			foreach (int id in area.Provinces)
			{
				foreach (CUnit unit in state.Positions.Of(id))
				{
					if (CTheatres.IsLand(unit) && CCountryLists.IsEnemy(state, country, unit.Owner))
					{
						enemies++;
					}
				}
			}
			int n = System.Math.Max(enemies, 1);
			if (n * reference > 2 * lift || divisions.Count < n)
			{
				continue;
			}
			int landing = CLandingPicker.Pick(state, country, area, null);
			if (landing <= 0)
			{
				continue;
			}
			long capacity = Capacity(state, area, landing);
			if (state.Country(area.Controller) is { } holder && Ai.CWarTargets.Prunes(holder)
				&& ReferenceEquals(network.AreaOf(holder.ActingCapital), area))
			{
				n *= 3;
			}
			var picked = new List<CUnit>();
			long supply = 0;
			long weight = 0;
			long smallest = long.MaxValue;
			while (picked.Count < n && supply <= capacity + SupplyMargin * Whole)
			{
				CUnit? best = NearestDivision(state, country, network, divisions, picked, area, landing, lift - weight);
				if (best is null)
				{
					break;
				}
				picked.Add(best);
				supply += Supply(best);
				long mine = CTransport.Weight(best);
				weight += mine;
				smallest = System.Math.Min(smallest, mine);
			}
			if (weight > 2 * lift)
			{
				ai.FailedAreas.Add(area);
				continue;
			}
			if (picked.Count < n || picked.Count == 0)
			{
				continue;
			}
			(List<CUnit> transports, List<CUnit> escorts) = Fleets(state, fleets, landing, weight);
			long chosen = transports.Sum(CTransport.Capacity);
			if (transports.Count == 0 || chosen < smallest)
			{
				continue;
			}
			var invasion = new CInvasion(country.Tag, landing);
			foreach (CUnit unit in picked)
			{
				invasion.Add(unit);
				divisions.Remove(unit);
			}
			foreach (CUnit fleet in transports.Concat(escorts))
			{
				invasion.Add(fleet);
				fleets.Remove(fleet);
			}
			ai.Invasions.Add(invasion);
			invaded.Add(area);
		}
	}

	/// <summary>The year rules: Britain's dominions before 1942, Germany's London before 1943.</summary>
	private static bool Skipped(CGameState state, CCountry country, CSupplyArea area)
	{
		int year = state.Date.Year;
		if (country.Tag.Value is "NZL" or "CAN" or "SAF" or "AST" && year < 1942)
		{
			return true;
		}
		return country.Tag.Value == "GER" && year <= 1942 && area.Provinces.Contains(1964);
	}

	/// <summary>What the landing and the area's ports within reach of it can land in a day.</summary>
	private static long Capacity(CGameState state, CSupplyArea area, int landing)
	{
		CProvince beach = state.Province(landing);
		long capacity = CConvoyRules.PortCapacity(state, beach);
		foreach (int port in area.Ports)
		{
			long squared = CMapDistance.Squared(beach, state.Province(port), state.MapWidth);
			if (squared >= PortRadius)
			{
				continue;
			}
			long share = (PortRadius - squared) * Whole / PortRadius;
			capacity += (share * CConvoyRules.PortCapacity(state, state.Province(port)) / Whole + Whole / 2) / Whole * Whole;
		}
		return capacity;
	}

	/// <summary>The divisions fit and spare enough to be taken: two brigades, ashore, ready, from land with more than it needs.</summary>
	private static List<CUnit> Divisions(CGameState state, CCountry country, CSupplyNetwork network)
	{
		var list = new List<CUnit>();
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		var spare = new Dictionary<CSupplyArea, long>();
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.Kind != EUnitKind.Division || unit.IsRemoved || unit.SubUnits.Count < 2 || unit.Transport is not null
				|| unit.Invasion is not null || CAgentUnits.NearestAgent(unit) is not { } agent
				|| network.AreaOf(unit.Location) is not { } area || area.IsLandlocked)
			{
				continue;
			}
			if (!ReferenceEquals(area, capitalArea) && area.RegionGroup is { } group && CAreaHostility.GroupBordersEnemy(state, group))
			{
				continue;
			}
			int role = CArmyRole.Of(unit);
			if (role is CArmyRole.Garrison or CArmyRole.Airborne)
			{
				continue;
			}
			CUnitShares.Of(state, unit, out long strength, out long organisation);
			if (organisation < ReadyOrganisation || strength < ReadyStrength)
			{
				continue;
			}
			if (!spare.TryGetValue(area, out long left))
			{
				long need = CPlanPowers.NeedOf(state, country, agent, area);
				if (ReferenceEquals(area, capitalArea) && !CAreaHostility.TouchesHostileArea(state, area))
				{
					need /= 2;
				}
				long have = 0;
				foreach (CUnit other in agent.Land)
				{
					if (other.Kind == EUnitKind.Division && ReferenceEquals(network.AreaOf(other.Location), area))
					{
						have++;
					}
				}
				left = have - need;
			}
			if (left > 0 || role == CArmyRole.Marine)
			{
				list.Add(unit);
				left--;
			}
			spare[area] = left;
		}
		return list;
	}

	private static CUnit? NearestDivision(CGameState state, CCountry country, CSupplyNetwork network, List<CUnit> divisions,
		List<CUnit> picked, CSupplyArea target, int landing, long liftLeft)
	{
		CUnit? best = null;
		long least = long.MaxValue;
		foreach (CUnit unit in divisions)
		{
			if (picked.Contains(unit) || network.AreaOf(unit.Location) is not { } own || own.Ports.Count == 0)
			{
				continue;
			}
			int role = CArmyRole.Of(unit);
			if (role is CArmyRole.Armour or CArmyRole.Garrison
				|| ReferenceEquals(network.AreaOf(CAgentUnits.DestinationOrPlace(unit))?.RegionGroup, target.RegionGroup))
			{
				continue;
			}
			if (CTransport.Weight(unit) > liftLeft)
			{
				continue;
			}
			long distance = CSeaTargets.Sea(state, own.Ports[0], landing);
			if (distance < 0)
			{
				continue;
			}
			long key = distance * Whole;
			key = role == CArmyRole.Marine ? key / 2 : role == CArmyRole.Fast ? key * 2 : key;
			if (key < least)
			{
				least = key;
				best = unit;
			}
		}
		return best;
	}

	/// <summary>Transports until they carry the troops and escorts until their power is half the troops' weight, the nearest of each a round.</summary>
	private static (List<CUnit>, List<CUnit>) Fleets(CGameState state, List<CUnit> fleets, int landing, long weight)
	{
		var transports = new List<CUnit>();
		var escorts = new List<CUnit>();
		long lift = 0;
		long power = 0;
		long wanted = weight * Whole / 2;
		while (lift < weight || power < wanted)
		{
			CUnit? transport = null;
			CUnit? escort = null;
			long nearestTransport = long.MaxValue;
			long nearestEscort = long.MaxValue;
			foreach (CUnit fleet in fleets)
			{
				if (transports.Contains(fleet) || escorts.Contains(fleet) || !Fit(state, fleet))
				{
					continue;
				}
				long distance = CSeaTargets.Sea(state, fleet.Location, landing);
				if (distance < 0)
				{
					continue;
				}
				long d = distance + 500;
				CShipCensus census = CShipCensus.Of(fleet);
				if (census.Transports > 0)
				{
					if (lift < weight && d < nearestTransport)
					{
						nearestTransport = d;
						transport = fleet;
					}
				}
				else if (power < wanted)
				{
					long key = census.Submarines > 0 ? d * 100 : census.Carriers > 0 ? d / 3 : census.Capitals > 0 ? d / 2 : d;
					if (key < nearestEscort)
					{
						nearestEscort = key;
						escort = fleet;
					}
				}
			}
			if (transport is null && escort is null)
			{
				break;
			}
			if (transport is not null)
			{
				transports.Add(transport);
				lift += CTransport.Capacity(transport);
				power += CUnitPower.Of(transport);
			}
			if (escort is not null)
			{
				escorts.Add(escort);
				power += CUnitPower.Of(escort);
			}
		}
		return (transports, escorts);
	}

	private static bool Fit(CGameState state, CUnit fleet)
	{
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			if (CFleetRegroup.StrengthShare(state, ship) < ReadyStrength || CFleetRegroup.OrganisationShare(state, ship) < ReadyOrganisation)
			{
				return false;
			}
		}
		return fleet.SubUnits.Count > 0;
	}

	/// <summary>A unit's supplies a day, in thousandths.</summary>
	private static long Supply(CUnit unit)
	{
		long sum = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			sum += brigade.Stats[CUnitSupply.SupplyStatistic].Raw;
		}
		return sum;
	}

	/// <summary>An infantry brigade's weight aboard, the measure of the lift the AI wants before it invades at all.</summary>
	private static long InfantryWeight(CGameState state)
	{
		return state.SubUnitTypes.TryGetValue("infantry_brigade", out CSubUnitType? infantry)
			? infantry.Stats[CSubUnitType.TransportWeight].Raw / Whole
			: 1;
	}

	// ---- the pruning

	/// <summary>Finished invasions dropped; for a country the AI runs, those whose area is neither ranked nor beside a ranked one.</summary>
	public static void Prune(CGameState state, CCountry country)
	{
		CUnitAi ai = country.UnitAi;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CInvasion invasion in ai.Invasions.ToArray())
		{
			bool drop = invasion.IsFinished;
			if (!drop && ai.RunsUnits)
			{
				CSupplyArea? area = network.AreaOf(invasion.Objective);
				drop = area is null || !ai.RankedAreas.Exists(entry => ReferenceEquals(entry.Area, area)
					|| entry.Area.Neighbours.Contains(area));
			}
			if (!drop)
			{
				continue;
			}
			foreach (CUnit fleet in invasion.Fleets)
			{
				if (fleet.IsRemoved)
				{
					continue;
				}
				if (state.Province(fleet.Location).IsSea == false)
				{
					foreach (CUnit passenger in fleet.Passengers.ToArray())
					{
						state.Outbox.Add(new Commands.CUnloadUnitCommand(country.Tag, passenger.Id));
					}
				}
				if (fleet.Base > 0)
				{
					state.Outbox.Add(new Commands.CSetUnitOrderCommand(country.Tag, fleet.Id, EUnitOrder.Rebase, fleet.Base));
				}
			}
			invasion.Release();
			ai.Invasions.Remove(invasion);
		}
	}
}
