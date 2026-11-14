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
			Trace(state, country, $"invasions: lift {CSealift.SumCapacity(country)} under four infantry brigades' {4 * InfantryWeight(state)}");
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
		var scored = new List<(CSupplyArea Area, long Score)>();
		var staging = new List<CSupplyArea>();
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
				if (area.Controller != enemy.Tag || area.IsLandlocked)
				{
					continue;
				}
				if (country.Faction.Length > 0 && enemy.Faction.Length > 0 && enemy.Tag.Value is "ROM" or "BUL" or "YUG"
					&& ReferenceEquals(area, network.AreaOf(enemy.ActingCapital)))
				{
					continue;
				}
				// Its neighbours first, which gather the staging areas whether or not it is failed already.
				bool touches = Touches(state, country, network, enemy, area, capitalArea, staging);
				if (ai.FailedAreas.Contains(area))
				{
					continue;
				}
				if (touches || !MayInvade(state, country, area) || !Reachable(state, country, area, reach, range))
				{
					ai.FailedAreas.Add(area);
					continue;
				}
				Insert(scored, area, Score(state, country, network, enemy, area, capitalArea));
			}
		}
		foreach ((CSupplyArea area, long score) in scored.Take(Kept))
		{
			ai.RankedAreas.Add((area, score));
		}
		AddStaging(state, country, network, capitalArea, staging);
		// The final cut: from the tail, while more than one is left, those under a tenth of the head's score.
		if (ai.RankedAreas.Count > 1)
		{
			long floor = ai.RankedAreas[0].Score / 10;
			while (ai.RankedAreas.Count > 1 && ai.RankedAreas[^1].Score < floor)
			{
				ai.RankedAreas.RemoveAt(ai.RankedAreas.Count - 1);
			}
		}
		Trace(state, country, $"invasions ranked: {string.Join(", ", ai.RankedAreas.Select(entry => $"{entry.Area.Provinces[0]}={entry.Score}"))}; failed {ai.FailedAreas.Count}");
	}

	private static void Trace(CGameState state, CCountry country, string line)
	{
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write("  " + line);
		}
	}

	/// <summary>
	/// Whether a candidate area touches us, gathering staging areas on the way: a neighbour with a port that is ours touches
	/// us, and goes on the staging list unless it is our capital's area; and, where the candidate is its holder's capital
	/// area, a neighbour with a port held by a friend of ours at war with that holder - not played, of our faction where
	/// both have one, not in our capital's group of areas, our subject or (we not Japan) nobody's subject and not Japan,
	/// Poland or Denmark - touches us and is a staging area too (<c>docs/mechanics/moving.md</c>, section 16).
	/// </summary>
	private static bool Touches(CGameState state, CCountry country, CSupplyNetwork network, CCountry holder, CSupplyArea area,
		CSupplyArea? capitalArea, List<CSupplyArea> staging)
	{
		bool touches = false;
		bool holdersCapital = ReferenceEquals(area, network.AreaOf(holder.ActingCapital));
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (beside.IsLandlocked)
			{
				continue;
			}
			if (beside.Controller == country.Tag)
			{
				touches = true;
				if (!ReferenceEquals(beside, capitalArea) && !staging.Contains(beside))
				{
					staging.Add(beside);
				}
				continue;
			}
			if (!holdersCapital || beside.Controller == holder.Tag || state.Country(beside.Controller) is not { } friend
				|| beside.Provinces.Count == 0)
			{
				continue;
			}
			if (!CCountryLists.IsFriend(state, country, friend.Tag, withAccess: false)
				|| !Movement.CMovementRules.IsEnemyIn(state, friend.Tag, holder.Tag, beside.Provinces[0])
				|| state.Played.Contains(friend.Tag)
				|| (country.Faction.Length > 0 && friend.Faction.Length > 0 && friend.Faction != country.Faction)
				|| (beside.RegionGroup is not null && ReferenceEquals(beside.RegionGroup, capitalArea?.RegionGroup)))
			{
				continue;
			}
			bool subject = friend.Overlord == country.Tag
				|| (country.Tag.Value != "JAP" && friend.Tag.Value is not ("JAP" or "POL" or "DEN") && friend.Overlord is null);
			if (!subject)
			{
				continue;
			}
			touches = true;
			if (!staging.Contains(beside))
			{
				staging.Add(beside);
			}
		}
		return touches;
	}

	/// <summary>
	/// The staging areas, added after the cut to ten: first the areas of the priority provinces held by a country on our
	/// side or fighting beside us and not our enemy; then each not failed, its sea route open from our capital's area (or
	/// it is failed), and one of our own only with 40 provinces of enemy-held land beside it (25 where one is its holder's
	/// capital area), scored 500 - 10500 for a priority area - and a draw on the AI's generator below a fifth of that, less
	/// 5 a province, doubled for its holder's capital area, in single precision and rounded half to even; distance nought
	/// (<c>docs/mechanics/moving.md</c>, section 16; <c>reversing/findings/FINDINGS-liveread.md</c>, section 89).
	/// </summary>
	private static void AddStaging(CGameState state, CCountry country, CSupplyNetwork network, CSupplyArea? capitalArea,
		List<CSupplyArea> staging)
	{
		CUnitAi ai = country.UnitAi;
		var priority = new List<CSupplyArea>();
		foreach (int id in ai.PriorityProvinces)
		{
			if (network.AreaOf(id) is { } area && !priority.Contains(area))
			{
				priority.Add(area);
			}
		}
		foreach (CSupplyArea area in priority)
		{
			CCountryTag holder = area.Controller;
			if (area.Provinces.Count == 0 || holder.Value == Revolts.CRebelFactions.Rebels || state.Country(holder) is null)
			{
				continue;
			}
			if ((IsSameSide(state, country, holder)
					|| (CCountryLists.FightingTogether(state, country.Tag, holder) && !CCountryLists.IsEnemy(state, country, holder)))
				&& !staging.Contains(area))
			{
				staging.Add(area);
			}
		}
		foreach (CSupplyArea area in staging)
		{
			if (ai.FailedAreas.Contains(area))
			{
				continue;
			}
			bool open = capitalArea is not null
				? CSealiftPickers.SeaRouteOpen(state, country, capitalArea, area)
				: !state.IsAtWar(country.Tag);
			if (!open)
			{
				ai.FailedAreas.Add(area);
				continue;
			}
			if (area.Controller == country.Tag && !BesideEnoughEnemyLand(state, country, network, area))
			{
				continue;
			}
			float s = priority.Contains(area) ? 10500f : 500f;
			long k = System.Math.Max(RoundEven((float)(s / 5.0)), 5);
			s = (float)(s + (double)(state.AiRandom.Next() % k));
			s = (float)(s - (double)(5 * area.Provinces.Count));
			if (state.Country(area.Controller) is { } holder && ReferenceEquals(network.AreaOf(holder.ActingCapital), area))
			{
				s = (float)(s * 2.0);
			}
			long score = RoundEven(s);
			Trace(state, country, $"staging area {area.Provinces[0]} ({area.Controller}, {area.Provinces.Count} provinces): {score}");
			int at = ai.RankedAreas.Count;
			while (at > 0 && ai.RankedAreas[at - 1].Score < score)
			{
				at--;
			}
			ai.RankedAreas.Insert(at, (area, score));
		}
	}

	/// <summary>One of our own areas staged from only with 40 provinces of enemy-held land beside it, 25 where one is its holder's capital area.</summary>
	private static bool BesideEnoughEnemyLand(CGameState state, CCountry country, CSupplyNetwork network, CSupplyArea area)
	{
		int provinces = 0;
		bool capital = false;
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (beside.Controller == country.Tag || beside.Provinces.Count == 0
				|| !Movement.CMovementRules.IsEnemyIn(state, country.Tag, beside.Controller, beside.Provinces[0]))
			{
				continue;
			}
			provinces += beside.Provinces.Count;
			if (state.Country(beside.Controller) is { } holder && ReferenceEquals(network.AreaOf(holder.ActingCapital), beside))
			{
				capital = true;
			}
		}
		return provinces >= (capital ? 25 : 40);
	}

	/// <summary>
	/// The original's <c>CCountry::IsSameSide</c>: not the rebels; itself, or one the other's overlord, or the two under one
	/// overlord.
	/// </summary>
	private static bool IsSameSide(CGameState state, CCountry country, CCountryTag tag)
	{
		if (tag.Value == Revolts.CRebelFactions.Rebels)
		{
			return false;
		}
		if (tag == country.Tag || country.Overlord == tag)
		{
			return true;
		}
		CCountry? other = state.Country(tag);
		return other?.Overlord == country.Tag || (country.Overlord is { } overlord && other?.Overlord == overlord);
	}

	/// <summary>Into a list kept by descending score, an equal score after its equals.</summary>
	private static void Insert(List<(CSupplyArea Area, long Score)> list, CSupplyArea area, long score)
	{
		int at = list.Count;
		while (at > 0 && list[at - 1].Score < score)
		{
			at--;
		}
		list.Insert(at, (area, score));
	}

	/// <summary>A single-precision figure rounded as the original's <c>fistp</c> rounds it: to the nearest, half to even.</summary>
	private static long RoundEven(float value)
	{
		return (long)System.Math.Round((double)value, System.MidpointRounding.ToEven);
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
			Trace(state, country, $"invasions planned none: {ai.Invasions.Count} of {cap}, {ai.RankedAreas.Count} ranked, preparing war {Ai.CWarTargets.IsPreparingWar(state, country)}");
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
			Trace(state, country, "invasions planned none: no division free");
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
			bool enemyHeld = CCountryLists.IsEnemy(state, country, area.Controller);
			bool special = IsSpecial(state, country, area);
			int n;
			long supply = 0;
			if (enemyHeld && !special)
			{
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
				n = System.Math.Max(enemies, 1);
				Trace(state, country, $"invasion of {area.Provinces[0]}: enemies {n}, lift {lift}, divisions free {divisions.Count}");
				if (n * reference > 2 * lift || divisions.Count < n)
				{
					continue;
				}
			}
			else
			{
				n = Reinforcement(state, country, network, area, special, out supply);
				Trace(state, country, $"reinforcement of {area.Provinces[0]}{(special ? " (special)" : "")}: wanted {n}, supply there {supply}, lift {lift}, divisions free {divisions.Count}");
				if (n < 1)
				{
					continue;
				}
			}
			int landing = enemyHeld || area.Ports.Count == 0 ? CLandingPicker.Pick(state, country, area, null) : NearestPort(state, country, area);
			if (landing <= 0)
			{
				Trace(state, country, "  no landing");
				continue;
			}
			long capacity = enemyHeld || area.Ports.Count == 0 ? Capacity(state, area, landing) : PortsCapacity(state, area);
			if (enemyHeld && !special && state.Country(area.Controller) is { } holder && Ai.CWarTargets.Prunes(holder)
				&& ReferenceEquals(network.AreaOf(holder.ActingCapital), area))
			{
				n *= 3;
			}
			var picked = new List<CUnit>();
			long weight = 0;
			long smallest = long.MaxValue;
			while (picked.Count < n && supply <= capacity + SupplyMargin * Whole)
			{
				CUnit? best = NearestDivision(state, country, network, divisions, picked, area, landing, lift - weight, enemyHeld);
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
			if (enemyHeld && weight > 2 * lift)
			{
				ai.FailedAreas.Add(area);
				continue;
			}
			Trace(state, country, $"  landing {landing}, capacity {capacity}, picked {picked.Count} of {n} [{string.Join("; ", picked.Select(unit => unit.Name))}], weight {weight}");
			if (picked.Count == 0 || (enemyHeld && picked.Count < n))
			{
				continue;
			}
			(List<CUnit> transports, List<CUnit> escorts) = Fleets(state, fleets, landing, weight);
			long chosen = transports.Sum(CTransport.Capacity);
			Trace(state, country, $"  transports {transports.Count} carrying {chosen}, escorts {escorts.Count}");
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

	/// <summary>
	/// The original's <c>COwnerArea_IsWellPorted</c>: its ports' naval bases' working levels, summed, over its provinces, more
	/// than half a level a province.
	/// </summary>
	private static bool IsWellPorted(CGameState state, CSupplyArea area)
	{
		if (area.Provinces.Count == 0 || CTheatreFigures.NavalBase(state) is not { } naval)
		{
			return false;
		}
		long levels = 0;
		foreach (int port in area.Ports)
		{
			levels += state.Province(port).BuildingLevel(naval).Raw;
		}
		return levels / area.Provinces.Count > 500;
	}

	/// <summary>
	/// Britain and its dominions up to 1941 treat the areas of Paris, Brugge and Amsterdam as a friend's to reinforce,
	/// whoever holds them, five divisions at most.
	/// </summary>
	private static bool IsSpecial(CGameState state, CCountry country, CSupplyArea area)
	{
		return country.Tag.Value is "ENG" or "NZL" or "CAN" or "SAF" or "AST" && state.Date.Year <= 1941
			&& (area.Provinces.Contains(Paris) || area.Provinces.Contains(Brugge) || area.Provinces.Contains(Amsterdam));
	}

	private const int Paris = 2613;
	private const int Brugge = 2137;
	private const int Amsterdam = 1910;

	/// <summary>
	/// How many divisions a friend's area - or a special one - wants sent: one, and one for each enemy land unit in the enemy
	/// areas beside it, less one for each land unit of ours that is no headquarters already in its group of areas, each we
	/// lent its holder there and each of our invasions' bound for it; at most 5 for a special area, 20 otherwise. The
	/// supplies those units of ours use are what the force's supplies start from.
	/// </summary>
	private static int Reinforcement(CGameState state, CCountry country, CSupplyNetwork network, CSupplyArea area, bool special,
		out long supply)
	{
		supply = 0;
		int n = 1;
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (!CCountryLists.IsEnemy(state, country, beside.Controller))
			{
				continue;
			}
			foreach (int id in beside.Provinces)
			{
				foreach (CUnit unit in state.Positions.Of(id))
				{
					if (CTheatres.IsLand(unit) && CCountryLists.IsEnemy(state, country, unit.Owner))
					{
						n++;
					}
				}
			}
		}
		CSupplyGroup? group = area.RegionGroup;
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.IsRemoved || !CTheatres.IsLand(unit) || CArmyRole.Of(unit) == CArmyRole.Headquarters)
			{
				continue;
			}
			bool there = group is not null && ReferenceEquals(network.AreaOf(unit.Location)?.RegionGroup, group);
			bool bound = unit.Invasion is { } invasion && group is not null
				&& ReferenceEquals(network.AreaOf(invasion.Objective)?.RegionGroup, group);
			if (there || bound)
			{
				n--;
				supply += Supply(unit);
			}
		}
		if (state.Country(area.Controller) is { } holder && holder.Tag != country.Tag)
		{
			foreach (CUnit unit in holder.UnitsInOrder())
			{
				if (!unit.IsRemoved && CTheatres.IsLand(unit) && unit.Lender == country.Tag && group is not null
					&& ReferenceEquals(network.AreaOf(unit.Location)?.RegionGroup, group))
				{
					n--;
					supply += Supply(unit);
				}
			}
		}
		return System.Math.Min(n, special ? 5 : 20);
	}

	/// <summary>
	/// A friend's area is landed at its port nearest our true capital, the distance times 3000 over the working level of
	/// its naval base - the plain distance at none; the first of equals.
	/// </summary>
	private static int NearestPort(CGameState state, CCountry country, CSupplyArea area)
	{
		CBuilding? naval = CTheatreFigures.NavalBase(state);
		CProvince? capital = country.Capital > 0 ? state.Province(country.Capital) : null;
		int best = 0;
		double least = double.MaxValue;
		foreach (int port in area.Ports)
		{
			CProvince p = state.Province(port);
			double distance = capital is null ? 0 : CMapDistance.Between(capital, p, state.MapWidth);
			long level = naval is null ? 0 : p.BuildingLevel(naval).Raw / Whole;
			double score = level > 0 ? distance * 3000 / level : distance;
			if (score < least)
			{
				least = score;
				best = port;
			}
		}
		return best;
	}

	/// <summary>What a friend's area's ports land in a day between them.</summary>
	private static long PortsCapacity(CGameState state, CSupplyArea area)
	{
		long capacity = 0;
		foreach (int port in area.Ports)
		{
			capacity += CConvoyRules.PortCapacity(state, state.Province(port));
		}
		return capacity;
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
				// Halved in the capital's area, unless it borders hostile land or is well ported.
				if (ReferenceEquals(area, capitalArea) && !CAreaHostility.TouchesHostileArea(state, area) && !IsWellPorted(state, area))
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
		List<CUnit> picked, CSupplyArea target, int landing, long liftLeft, bool enemyHeld)
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
			// Armour and garrisons stay out of a landing on enemy ground, and go to a friend's.
			if ((enemyHeld && role is CArmyRole.Armour or CArmyRole.Garrison)
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
