using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's divisions sent across water to the areas of its theatre short of them: every area's need against the
/// divisions in it or bound for it, targets out of the theatre and surpluses released, the short areas filled from other
/// pieces of land - a port for one of ours, a landing province for the enemy's - by a march where one can walk and by
/// sea where none can; then a debark voted for, an embark taken where most of those bound for it stand, the troops
/// boarded there and transports sent until they can carry them. Only a country with transports does any of it. The
/// original's <c>CAIUnit::AssignAreaTargetsAndSealift</c>. See <c>docs/mechanics/moving.md</c>, section 15.
/// </summary>
public static class CSealift
{
	private const long Whole = 1000;

	private const long Stranded = 10_000;

	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		agent.SealiftWeight = 0;
		agent.SealiftPort = 0;
		if (agent.Unit is not null)
		{
			agent.ReassignAreasNow = false;
		}
		if (agent.Unit is not { } headquarters || SumCapacity(country) < 1
			|| CAgents.TheatreOf(state, country, agent) is not { } theatre)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		bool atWar = state.IsAtWar(country.Tag);
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		var areas = new List<CSupplyArea>(theatre.Areas);
		var need = new Dictionary<CSupplyArea, long>();
		var have = new Dictionary<CSupplyArea, long>();
		foreach (CSupplyArea area in areas)
		{
			bool relaxed = atWar && !CAreaHostility.TouchesHostileArea(state, area);
			long wanted = CPlanPowers.NeedOf(state, country, agent, area, relaxed);
			if (relaxed && ReferenceEquals(area, capitalArea))
			{
				wanted /= 2;
			}
			need[area] = wanted;
		}
		foreach (CUnit unit in agent.Land)
		{
			if (unit.Kind == EUnitKind.Division && network.AreaOf(Anchor(unit)) is { } area && need.ContainsKey(area))
			{
				have[area] = have.GetValueOrDefault(area) + 1;
			}
		}
		ReleaseOutside(state, country, agent, network, need, have);
		ReleaseSurplus(state, country, agent, network, areas, need, have, headquarters);
		var listed = new List<CUnit>();
		long weight = 0;
		int stranded = Shippable(state, country, agent, network, need, have, listed, ref weight);
		Fill(state, country, agent, network, areas, need, have, listed, ref weight, stranded, theatre);
		Headquarters(state, country, agent, network);
		Pair(state, country, agent, network, headquarters, listed, weight, need);
	}

	/// <summary>Where a unit counts for: its sealift target, else where it is bound, else where it is.</summary>
	public static int Anchor(CUnit unit) => unit.SealiftTarget > 0 ? unit.SealiftTarget : CAgentUnits.DestinationOrPlace(unit);

	/// <summary>Each unit's carrying, whole units cut: the original's <c>UnitList_SumTransportCapacity</c>.</summary>
	public static long SumCapacity(CCountry country)
	{
		long sum = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			if (!unit.IsRemoved)
			{
				sum += CTransport.Capacity(unit);
			}
		}
		return sum;
	}

	/// <param name="why">What set or cleared it, for a tool's trace.</param>
	private static void SetTarget(CGameState state, CCountry country, CUnit unit, int target, string why)
	{
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write($"    sealift {unit.Name}#{unit.Id} at {unit.Location}: target {unit.SealiftTarget} -> {target} ({why})");
		}
		unit.SealiftTarget = target;
		state.Outbox.Add(new CSetAiParamCommand(country.Tag, unit.Id, target, unit.Staging));
	}

	/// <summary>A target in a real area out of the theatre is kept only by a headquarters-role unit in land the country may use.</summary>
	private static void ReleaseOutside(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network,
		Dictionary<CSupplyArea, long> need, Dictionary<CSupplyArea, long> have)
	{
		foreach (CUnit unit in agent.Land)
		{
			if (unit.SealiftTarget <= 0 || network.AreaOf(unit.SealiftTarget) is not { } area || need.ContainsKey(area))
			{
				continue;
			}
			if (CArmyRole.Of(unit) == CArmyRole.Headquarters
				&& CApproaches.IsFriendly(state, country, state.Province(unit.SealiftTarget).Controller))
			{
				continue;
			}
			SetTarget(state, country, unit, 0, "outside the theatre");
			if (unit.Kind == EUnitKind.Division && network.AreaOf(unit.Location) is { } here && need.ContainsKey(here))
			{
				have[here] = have.GetValueOrDefault(here) + 1;
			}
		}
	}

	/// <summary>While an area has more than it needs, the division bound there from farthest is released.</summary>
	private static void ReleaseSurplus(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network,
		List<CSupplyArea> areas, Dictionary<CSupplyArea, long> need, Dictionary<CSupplyArea, long> have, CUnit headquarters)
	{
		int embark = headquarters.Plan.Embark;
		CSupplyArea? embarkArea = embark > 0 ? network.AreaOf(embark) : null;
		foreach (CSupplyArea area in areas)
		{
			while (have.GetValueOrDefault(area) > need[area])
			{
				CUnit? farthest = null;
				long most = -10;
				foreach (CUnit unit in agent.Land)
				{
					if (unit.Kind != EUnitKind.Division || unit.Transport is not null || unit.SealiftTarget <= 0
						|| !ReferenceEquals(network.AreaOf(unit.SealiftTarget), area)
						|| network.AreaOf(CAgentUnits.DestinationOrPlace(unit)) is not { } standing
						|| need.GetValueOrDefault(standing) <= 0)
					{
						continue;
					}
					long distance = embarkArea is null
						? CMapDistance.Between(state.Province(unit.Location), state.Province(unit.SealiftTarget), state.MapWidth)
						: !ReferenceEquals(network.AreaOf(unit.Location), embarkArea)
							? Stranded
							: CMapDistance.Between(state.Province(unit.Location), state.Province(embark), state.MapWidth);
					if (distance > most)
					{
						most = distance;
						farthest = unit;
					}
				}
				if (farthest is null)
				{
					break;
				}
				SetTarget(state, country, farthest, 0, "surplus");
				have[area]--;
				if (network.AreaOf(farthest.Location) is { } its && need.ContainsKey(its))
				{
					have[its] = have.GetValueOrDefault(its) + 1;
				}
			}
		}
	}

	/// <summary>
	/// Each unit with a target kept on the sea list or released where it may no longer be shipped; each without one in our
	/// land outside the theatre counted stranded. Answers the stranded count.
	/// </summary>
	private static int Shippable(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network,
		Dictionary<CSupplyArea, long> need, Dictionary<CSupplyArea, long> have, List<CUnit> listed, ref long weight)
	{
		int stranded = 0;
		foreach (CUnit unit in agent.Land)
		{
			if (unit.SealiftTarget > 0)
			{
				if (network.AreaOf(unit.SealiftTarget) is not { } target || !MayShip(state, country, agent, network, unit, target))
				{
					if (network.AreaOf(unit.SealiftTarget) is { } was && have.ContainsKey(was))
					{
						have[was]--;
					}
					SetTarget(state, country, unit, 0, "may not be shipped");
					if (network.AreaOf(unit.Location) is { } here && need.ContainsKey(here))
					{
						have[here] = have.GetValueOrDefault(here) + 1;
					}
					continue;
				}
				listed.Add(unit);
				weight += Weight(unit);
				continue;
			}
			if (unit.Kind == EUnitKind.Division && network.AreaOf(CAgentUnits.DestinationOrPlace(unit)) is { } area
				&& !need.ContainsKey(area) && area.Controller == country.Tag)
			{
				unit.IsStranded = true;
				stranded++;
			}
			else
			{
				unit.IsStranded = false;
			}
		}
		return stranded;
	}

	/// <summary>The areas short of divisions, the others' first in reverse, each filled from other land by the best candidate.</summary>
	private static void Fill(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, List<CSupplyArea> areas,
		Dictionary<CSupplyArea, long> need, Dictionary<CSupplyArea, long> have, List<CUnit> listed, ref long weight,
		int stranded, CTheatre theatre)
	{
		var ours = new List<(CSupplyArea Area, long Deficit)>();
		var others = new List<(CSupplyArea Area, long Deficit)>();
		foreach (CSupplyArea area in areas)
		{
			long deficit = need[area] - have.GetValueOrDefault(area);
			bool own = area.Controller == country.Tag;
			Insert(own ? ours : others, area, deficit, own);
		}
		foreach ((CSupplyArea area, long deficit) in others)
		{
			ours.Insert(0, (area, deficit));
		}
		CSupplyArea? largest = CTheatreProvinces.LargestArea(theatre);
		for (int index = 0; index < ours.Count; index++)
		{
			(CSupplyArea area, long d) = ours[index];
			bool enemy = CCountryLists.IsEnemy(state, country, area.Controller);
			long extra = !enemy && stranded > d && (CAreaHostility.TouchesHostileArea(state, area) || ReferenceEquals(area, largest))
				? stranded
				: 0;
			long want = System.Math.Max(d, extra);
			if (want < 1)
			{
				continue;
			}
			int target = enemy ? CSealiftPickers.LandingProvince(state, country, agent, area) : CSealiftPickers.PortNearObjectives(state, agent, area);
			if (target <= 0)
			{
				continue;
			}
			long next = index + 1 < ours.Count ? ours[index + 1].Deficit : 0;
			while (want >= next || (enemy && want > 0))
			{
				if (want < 1 && !enemy && next <= 0 && want < 0)
				{
					break;
				}
				CUnit? unit = Best(state, country, agent, network, area, d, need, have);
				if (unit is null)
				{
					break;
				}
				SetTarget(state, country, unit, target, "fill");
				if (CRouteFinder.Find(state, country, unit, unit.Location, target) is not null)
				{
					CAiMoves.Move(state, country, unit, target, safe: true);
				}
				else
				{
					listed.Add(unit);
					weight += Weight(unit);
				}
				have[area] = have.GetValueOrDefault(area) + 1;
				if (network.AreaOf(unit.Location) is { } from && have.ContainsKey(from))
				{
					have[from]--;
				}
				if (unit.IsStranded)
				{
					unit.IsStranded = false;
					extra--;
				}
				else
				{
					d--;
				}
				want = System.Math.Max(d, extra);
				if (want < 0)
				{
					break;
				}
			}
		}
	}

	private static void Insert(List<(CSupplyArea, long)> list, CSupplyArea area, long deficit, bool own = false)
	{
		// One of the country's own areas with the head's deficit and more provinces than the head's area goes in front
		// (FINDINGS-liveread.md, section 59).
		if (own && list.Count > 0 && list[0].Item2 == deficit && area.Provinces.Count > list[0].Item1.Provinces.Count)
		{
			list.Insert(0, (area, deficit));
			return;
		}
		int at = list.Count;
		while (at > 0 && list[at - 1].Item2 < deficit)
		{
			at--;
		}
		list.Insert(at, (area, deficit));
	}

	/// <summary>The candidate with the strictly highest score above nought.</summary>
	private static CUnit? Best(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, CSupplyArea target,
		long deficit, Dictionary<CSupplyArea, long> need, Dictionary<CSupplyArea, long> have)
	{
		bool enemyTarget = CCountryLists.IsEnemy(state, country, target.Controller);
		bool targetTouches = CAreaHostility.TouchesHostileArea(state, target);
		CUnit? best = null;
		float bestScore = 0;
		foreach (CUnit unit in agent.Land)
		{
			if (unit.Kind != EUnitKind.Division || unit.SubUnits.Count == 0 || unit.SealiftTarget > 0
				|| ReferenceEquals(unit, agent.Unit) || network.AreaOf(CAgentUnits.DestinationOrPlace(unit)) is not { } own
				|| !MayShip(state, country, agent, network, unit, target))
			{
				continue;
			}
			if (CArmyRole.Of(unit) == CArmyRole.Airborne && agent.AirTransports.Count > 0)
			{
				continue;
			}
			// No enemy area beside its own may touch the target area as well (FINDINGS-moving.md F.5, step 4).
			if (EnemyBetween(state, country, own, target))
			{
				continue;
			}
			long ownNeed = need.GetValueOrDefault(own);
			long ownHave = have.GetValueOrDefault(own);
			if (need.ContainsKey(own) && ownNeed > 0 && ownHave <= ownNeed)
			{
				continue;
			}
			if (!unit.IsStranded && deficit < 1)
			{
				continue;
			}
			bool bordersEnemy = CAreaHostility.TouchesHostileArea(state, own);
			// In single precision, as the original keeps the score, with the garrison rating in its own precision: two divisions
			// a ten-thousandth of a share apart score apart (reversing/findings/FINDINGS-liveread.md, section 61).
			float rating = CUnitRatings.GarrisonExact(state, unit);
			float score = (float)(ownHave - ownNeed) + (unit.IsStranded ? Stranded : 0);
			if (enemyTarget)
			{
				score = (float)((double)score + CUnitRatings.Fitness(state, unit) / (double)Whole);
			}
			else if (!targetTouches && bordersEnemy)
			{
				score = (float)((double)score + rating);
			}
			else if (targetTouches && !bordersEnemy)
			{
				score = (float)((double)score + (101.0 - rating));
			}
			if (target.Neighbours.Count < 1)
			{
				score = (float)((double)score + 2.0 * rating);
			}
			if (unit.Parent is { } parent)
			{
				if (network.AreaOf(Anchor(parent)) == target)
				{
					score = (float)((double)score + 100.0);
				}
				// And 50 for each other land unit under the same parent anchored there.
				foreach (CUnit sibling in parent.Children)
				{
					if (!ReferenceEquals(sibling, unit) && Theatres.CTheatres.IsLand(sibling) && network.AreaOf(Anchor(sibling)) == target)
					{
						score = (float)((double)score + 50.0);
					}
				}
			}
			if (bordersEnemy)
			{
				// Quartered where it is anchored - bound, or standing - in the area of operations (FINDINGS-liveread.md, section 61).
				score = unit.IsGarrison ? (float)((double)score * 2.0) : agent.Area.Contains(CAgentUnits.DestinationOrPlace(unit)) ? (float)((double)score * 0.25) : score;
			}
			if (state.AiTrace is { } trace && trace.Country == country.Tag)
			{
				trace.Write($"    sealift score {unit.Name} for {target.Provinces[0]}: {score:F4} (own {ownHave}-{ownNeed}, rating {rating:F5}, " +
					$"garrison {unit.IsGarrison}, in area {agent.Area.Contains(CAgentUnits.DestinationOrPlace(unit))}, borders {bordersEnemy})");
			}
			if (score > bestScore)
			{
				bestScore = score;
				best = unit;
			}
		}
		return best;
	}

	/// <summary>Whether an enemy-held area beside the unit's own area also borders the target area.</summary>
	private static bool EnemyBetween(CGameState state, CCountry country, CSupplyArea own, CSupplyArea target)
	{
		foreach (CSupplyArea beside in own.Neighbours)
		{
			if (CCountryLists.IsEnemy(state, country, beside.Controller) && beside.Neighbours.Contains(target))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether a unit may be given a target in an area: never within its own block of land, never where it cannot reach a
	/// portless area, never from a battle into enemy land, never a headquarters or garrison into an enemy's, and only by a
	/// sea route that is open. The original's <c>CAIUnit_MayShipUnitToArea</c>.
	/// </summary>
	public static bool MayShip(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, CUnit unit,
		CSupplyArea area)
	{
		CSupplyArea? own = network.AreaOf(unit.Location);
		if (own is not null && own.RegionGroup is not null && ReferenceEquals(own.RegionGroup, area.RegionGroup))
		{
			return false;
		}
		// The landlocked test is of the area the unit is bound for or stands in, the reach of the area asked about
		// (FINDINGS-moving.md A.4e).
		int place = CAgentUnits.DestinationOrPlace(unit);
		if (network.AreaOf(place) is { } bound && (bound.IsLandlocked || bound.Ports.Count < 1) && area.Provinces.Count > 0
			&& CRouteFinder.Find(state, country, unit, unit.Location, area.Provinces[0]) is null)
		{
			return false;
		}
		if (state.Province(place).Controller is { } holder && CCountryLists.IsEnemy(state, country, holder))
		{
			return false;
		}
		if (Combat.CCombats.IsHoldingGround(unit) || unit.IsRetreating
			|| !CUnitMayLeave.Allows(state, country, unit, null, strict: true, notDefending: true))
		{
			return false;
		}
		bool enemy = CCountryLists.IsEnemy(state, country, area.Controller) || area.Controller.Value == CRebelFactions.Rebels;
		int role = CArmyRole.Of(unit);
		if (enemy && role is CArmyRole.Headquarters or CArmyRole.Garrison)
		{
			return false;
		}
		if (!enemy && role is CArmyRole.Armour or CArmyRole.Fast
			&& ((area.Neighbours.Count < 1 && !ReferenceEquals(area, network.AreaOf(country.ActingCapital)))
				|| (own is not null && CAreaHostility.TouchesHostileArea(state, own))))
		{
			return false;
		}
		return own is null || CSealiftPickers.SeaRouteOpen(state, country, own, area);
	}

	/// <summary>A headquarters whose land children stand mostly in another area is given that area's port, no move.</summary>
	private static void Headquarters(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network)
	{
		foreach (CUnit unit in agent.NonDivisions)
		{
			if (unit.SealiftTarget > 0 || unit.Kind == EUnitKind.Theatre || network.AreaOf(unit.Location) is not { } own)
			{
				continue;
			}
			var counts = new Dictionary<CSupplyArea, int>();
			foreach (CUnit child in unit.Children)
			{
				if (CTheatres.IsLand(child) && network.AreaOf(child.Location) is { } area)
				{
					counts[area] = counts.GetValueOrDefault(area) + 1;
				}
			}
			CSupplyArea best = own;
			int most = counts.GetValueOrDefault(own);
			foreach ((CSupplyArea area, int count) in counts)
			{
				if (count > most)
				{
					most = count;
					best = area;
				}
			}
			if (!ReferenceEquals(best, own) && CSealiftPickers.PortNearObjectives(state, agent, best) is var port and > 0)
			{
				unit.SealiftTarget = port;
				state.Outbox.Add(new CSetAiParamCommand(country.Tag, unit.Id, port, 0));
			}
		}
	}

	/// <summary>The debark voted for and the embark where most of those bound for it stand; the troops boarded and transports sent.</summary>
	private static void Pair(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, CUnit headquarters,
		List<CUnit> listed, long weight, Dictionary<CSupplyArea, long> need)
	{
		agent.SealiftWeight = weight;
		agent.SealiftPort = weight > 0 && CAgents.TheatreOf(state, country, agent) is { } held
			&& CTheatreProvinces.LargestArea(held) is { Ports.Count: > 0 } largest ? largest.Ports[0] : 0;
		int embark = headquarters.Plan.Embark;
		int debark = headquarters.Plan.Debark;
		CSupplyArea? embarkArea = embark > 0 ? network.AreaOf(embark) : null;
		int atEmbark = 0;
		foreach (CUnit unit in listed)
		{
			// Where it is bound, or stands bound nowhere: the original counts the area of its destination (FINDINGS-liveread.md, section 43).
			if (unit.SealiftTarget == debark && embarkArea is not null
				&& ReferenceEquals(network.AreaOf(CAgentUnits.DestinationOrPlace(unit)), embarkArea))
			{
				atEmbark++;
			}
		}
		if ((embark > 0) != (debark > 0) || (embark > 0 && atEmbark == 0))
		{
			state.Outbox.Add(new CSetPlanTransportStateCommand(country.Tag, headquarters.Id, 0, 0));
			embark = 0;
			debark = 0;
		}
		if (debark == 0 && weight > 0 && agent.TransportFleets.Count > 0)
		{
			var tried = new HashSet<int>();
			while (true)
			{
				// The original's votes are a hash map, and a tie goes to whichever target its walk meets first
				// (reversing/findings/FINDINGS-liveread.md, section 62).
				var votes = new CStdHashMap();
				foreach (CUnit unit in listed)
				{
					long vote = network.AreaOf(unit.Location) is { } own && need.GetValueOrDefault(own, 1) < 1 ? 4 : 1;
					if (state.Province(unit.SealiftTarget).Controller != country.Tag)
					{
						vote += 10;
					}
					votes.Add(unit.SealiftTarget, vote);
				}
				int chosen = 0;
				long most = 0;
				// A target already tried keeps its place in the map, and is passed over only here.
				foreach ((int target, long count) in votes.Walk())
				{
					if (count > most && !tried.Contains(target))
					{
						most = count;
						chosen = target;
					}
				}
				if (chosen == 0)
				{
					break;
				}
				CSupplyArea? debarkArea = network.AreaOf(chosen);
				var byArea = new Dictionary<CSupplyArea, int>();
				foreach (CUnit unit in listed)
				{
					if (unit.SealiftTarget == chosen && network.AreaOf(unit.Location) is { } area && !ReferenceEquals(area, debarkArea))
					{
						byArea[area] = byArea.GetValueOrDefault(area) + 1;
					}
				}
				int port = 0;
				var order = new List<KeyValuePair<CSupplyArea, int>>(byArea);
				order.Sort((a, b) => b.Value.CompareTo(a.Value));
				foreach ((CSupplyArea area, int _) in order)
				{
					port = CSealiftPickers.EmbarkPort(state, agent, listed, area, chosen);
					if (port > 0)
					{
						break;
					}
				}
				if (port > 0)
				{
					state.Outbox.Add(new CSetPlanTransportStateCommand(country.Tag, headquarters.Id, port, chosen));
					embark = port;
					debark = chosen;
					break;
				}
				tried.Add(chosen);
			}
		}
		if (embark > 0 && debark > 0)
		{
			Board(state, country, agent, network, listed, embark, debark);
		}
	}

	/// <summary>
	/// What a unit weighs to the sea lift: its summed definition's transport weight, whole units cut on their own - so a land
	/// brigade with no width that is worn out counts nothing, as the original's rebuilt definition leaves it out; a
	/// headquarters at no organisation weighs nought (FINDINGS-moving.md, section F; FINDINGS-liveread.md, section 50).
	/// </summary>
	private static long Weight(CUnit unit)
	{
		return CUnitTotals.Sum(unit, CSubUnitType.TransportWeight).Raw / CFixedPoint.Scale;
	}

	/// <summary>The troops bound for debark in embark's area marched there or boarded; transports sent until they can carry them.</summary>
	private static void Board(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, List<CUnit> listed,
		int embark, int debark)
	{
		CSupplyArea? embarkArea = network.AreaOf(embark);
		long troops = 0;
		var waiting = new List<CUnit>();
		foreach (CUnit unit in listed)
		{
			if (unit.SealiftTarget != debark || unit.Transport is not null || unit.IsFighting || unit.IsRetreating
				|| !ReferenceEquals(network.AreaOf(unit.Location), embarkArea))
			{
				continue;
			}
			troops += Weight(unit);
			if (unit.Location == embark)
			{
				waiting.Add(unit);
			}
			else
			{
				CAiMoves.Move(state, country, unit, embark, safe: true);
			}
		}
		long capacity = 0;
		var present = new List<CUnit>();
		foreach (CUnit fleet in agent.TransportFleets)
		{
			if (fleet.Location == embark && fleet.Order == EUnitOrder.None)
			{
				capacity += CTransport.Capacity(fleet);
				present.Add(fleet);
			}
		}
		// Those boarded now count as aboard for the sailing below: the load and the order to sail go out in one pass, and the
		// load runs first, as the original's (reversing/findings/FINDINGS-liveread.md, section 63).
		var boarding = new HashSet<CUnit>();
		int left = 0;
		foreach (CUnit unit in waiting)
		{
			bool loaded = false;
			foreach (CUnit fleet in present)
			{
				if (CTransport.CanCarry(fleet, unit))
				{
					state.Outbox.Add(new CLoadUnitCommand(country.Tag, unit.Id, fleet.Id));
					boarding.Add(fleet);
					loaded = true;
					break;
				}
			}
			if (!loaded)
			{
				left++;
			}
		}
		while (troops > capacity)
		{
			CUnit? best = null;
			long least = long.MaxValue;
			foreach (CUnit fleet in agent.TransportFleets)
			{
				// Bound for embark or there already, or kept busy: the original asks its blocking test only of a fleet whose order
				// is a busy one, so a fleet on a plain way - home from a change of base, say - is sent (FINDINGS-liveread.md,
				// section 84).
				if (CAgentUnits.DestinationOrPlace(fleet) == embark || present.Contains(fleet)
					|| (fleet.Order is not (EUnitOrder.None or EUnitOrder.Move) && CCommandLadder.IsBlocked(fleet)))
				{
					continue;
				}
				CUnitShares.Of(state, fleet, out long strength, out long organisation);
				long distance = CSeaTargets.Sea(state, fleet.Location, embark);
				if (organisation < 10 || distance < 0)
				{
					continue;
				}
				long key = (((Whole - strength) + (Whole - organisation)) * 2 + Whole) * distance / Whole;
				if (key < least)
				{
					least = key;
					best = fleet;
				}
			}
			if (best is null)
			{
				break;
			}
			present.Add(best);
			capacity += CTransport.Capacity(best);
			// The AI's own move, as for any unit: its destination written at once, so the naval body that follows in the same
			// pass sees the fleet under way (FINDINGS-liveread.md, section 57).
			CAiMoves.Move(state, country, best, embark, safe: false);
		}
		// Ours: a fleet at embark carrying troops, or taking them aboard now, with nobody left waiting there unboarded, sails them
		// to debark.
		if (left == 0)
		{
			foreach (CUnit fleet in present)
			{
				if (fleet.Location == embark && (fleet.Passengers.Count > 0 || boarding.Contains(fleet)) && fleet.Order == EUnitOrder.None)
				{
					state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, EUnitOrder.Transport, debark, ai: true));
				}
			}
		}
	}
}
