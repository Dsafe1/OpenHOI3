using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's line shared over its area's positions by their scores, at most a stack a position, and its committed
/// divisions sent to the positions that want them: each division to the best position with room, then each position
/// still short pulling the nearest from a fuller one, or a fit reserve. The original's <c>CAIUnit_FillLinePositions</c>,
/// <c>CAIUnit_GetPositionCap</c> and <c>CAIUnit_SendReservesToPosition</c>. See <c>docs/mechanics/moving.md</c>, section
/// 7.3.
/// </summary>
public static class CLineFill
{
	private const long Whole = 1000;

	private const int ShutOut = -1000;

	private const int MostFort = 6;

	private const int SmallEnemyArea = 6;

	private const int SmallFrontArea = 3;

	private const int FewEnemies = 5;

	private const long Carry = 100;

	private const long SupplyCut = 3500;

	private const long Cheaper = 660;

	private const long ReserveStrength = 750;

	private const long ReserveOrganisation = 900;

	private const long ReserveReach = 80;

	/// <summary>Where the search for the best position starts: nothing is at or above it.</summary>
	private const float StartKey = 1_000_000f;

	public static bool Fill(CGameState state, CCountry country, CAgent agent, int line, int budget)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var byProvince = new Dictionary<int, CPosition>();
		var small = SmallFronts(state, country, agent, network);
		long carry = 0;
		bool any = false;
		int stack = CPositionCap.Stack(state);
		foreach (CPosition position in agent.Positions)
		{
			byProvince[position.Province] = position;
			CProvince province = state.Province(position.Province);
			if (!CApproaches.IsFriendly(state, country, province.Controller))
			{
				continue;
			}
			position.Have = 0;
			position.Standing = 0;
			if (OnlySmallFront(state, agent, province, small))
			{
				position.Want = ShutOut;
				continue;
			}
			position.Cap = CPositionCap.Of(state, country, agent, province, stack);
			if (position.Score < Whole && agent.PositionSum >= Whole || agent.PositionSum <= 0)
			{
				position.Want = 0;
				continue;
			}
			long exact = line * position.Score;
			long want = exact / agent.PositionSum;
			carry += (exact - want * agent.PositionSum) * Whole / agent.PositionSum;
			if (want > position.Cap)
			{
				carry += (want - position.Cap) * Whole;
				want = position.Cap;
			}
			position.Want = (int)want;
			any |= want > 0;
		}
		CPositionCount.Count(state, country, agent.Committed, agent.Positions.ConvertAll(position => position.Province),
			province => byProvince[province].Have++, province => byProvince[province].Standing++, divisionsOnly: true, withAllies: agent);
		if (!any)
		{
			foreach (CPosition position in agent.Positions)
			{
				// 1 each: the original's 2 is for a position where a unit standing there has an entry on a list of its own,
				// which no unit in the running game was seen to hold and which nothing here keeps - not where it is dug in
				// (FINDINGS-liveread.md, section 102).
				if (position.Want != ShutOut)
				{
					position.Want = 1;
				}
			}
		}
		else
		{
			while (carry > Carry && Least(agent) is { } least)
			{
				least.Want++;
				carry -= Whole;
			}
		}
		SupplyCutBack(state, agent);
		bool moved = SendEach(state, country, agent, network, byProvince, budget);
		moved |= PullShort(state, country, agent, byProvince, budget);
		return moved;
	}

	/// <summary>Front provinces of small areas with few enemy units that a committed division stands in or is bound for.</summary>
	private static HashSet<int> SmallFronts(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network)
	{
		var small = new HashSet<int>();
		var bound = new HashSet<int>();
		foreach (CUnit unit in agent.Committed)
		{
			bound.Add(CAgentUnits.DestinationOrPlace(unit));
		}
		foreach (int id in agent.FrontProvinces)
		{
			if (network.AreaOf(id) is not { } area || area.Provinces.Count > SmallFrontArea || !bound.Contains(id))
			{
				continue;
			}
			int enemies = 0;
			foreach (CUnit unit in state.Positions.Of(id))
			{
				if (CTheatres.IsLand(unit) && !CApproaches.IsFriendly(state, country, unit.Owner))
				{
					enemies++;
				}
			}
			if (enemies < FewEnemies)
			{
				small.Add(id);
			}
		}
		return small;
	}

	private static bool OnlySmallFront(CGameState state, CAgent agent, CProvince province, HashSet<int> small)
	{
		bool any = false;
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (!agent.FrontProvinces.Contains(edge.To))
			{
				continue;
			}
			if (!small.Contains(edge.To))
			{
				return false;
			}
			any = true;
		}
		return any;
	}

	/// <summary>The position with the least want, one at its cap sorted after; the first of equals.</summary>
	private static CPosition? Least(CAgent agent)
	{
		CPosition? least = null;
		long key = long.MaxValue;
		foreach (CPosition position in agent.Positions)
		{
			// A position scoring under 1, where the scores come to 1 or more, is given none of the rest either (FINDINGS-liveread.md, section 16).
			if (position.Want == ShutOut || (position.Score < Whole && agent.PositionSum >= Whole))
			{
				continue;
			}
			long mine = position.Want + (position.Want + 1 > position.Cap ? 100 : 0);
			if (mine < key)
			{
				key = mine;
				least = position;
			}
		}
		return least;
	}

	/// <summary>A position wanting more than three and a half stacks with no supplies there is cut, the rest given on.</summary>
	private static void SupplyCutBack(CGameState state, CAgent agent)
	{
		int spare = 0;
		foreach (CPosition position in agent.Positions)
		{
			int most = (int)(position.Cap * SupplyCut / Whole);
			if (position.Want <= most)
			{
				continue;
			}
			CProvince province = state.Province(position.Province);
			long supplies = CSupplyPass.Held(province.Pool, EGoods.Supplies);
			long need = CSupplyPass.Held(province.SupplyNeed, EGoods.Supplies);
			if (supplies <= 0 || need >= 5 * supplies)
			{
				spare += position.Want - most;
				position.Want = most;
			}
		}
		for (int round = 0; spare > 0 && round < 2; round++)
		{
			bool gave = true;
			while (spare > 0 && gave)
			{
				gave = false;
				foreach (CPosition position in agent.Positions)
				{
					bool first = position.Score >= Whole;
					if (spare <= 0 || (round == 0) != first || position.Want == ShutOut
						|| position.Want >= (int)(position.Cap * SupplyCut / Whole))
					{
						continue;
					}
					position.Want++;
					spare--;
					gave = true;
				}
			}
		}
	}

	/// <summary>Each committed division kept, sent on or sent to the best position with room.</summary>
	private static bool SendEach(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network,
		Dictionary<int, CPosition> byProvince, int budget)
	{
		bool moved = false;
		bool atWar = agent.FrontierAtWar;
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		bool airTransports = agent.AirTransports.Count > 0;
		foreach (CUnit unit in agent.Committed)
		{
			if (unit.IsGarrison || (CArmyRole.Of(unit) == CArmyRole.Airborne && airTransports) || unit.Location <= 0)
			{
				continue;
			}
			int bound = CAgentUnits.DestinationOrPlace(unit);
			if (CAgentUnits.Destination(unit) > 0)
			{
				if (IsEnemyHeld(state, country, bound))
				{
					if (unit.Path.Count < 2 || network.AreaOf(bound) is not { } far || far.Provinces.Count <= SmallFrontArea)
					{
						continue;
					}
				}
				else if (!unit.IsRetreating && CCombats.IsHoldingGround(unit) && network.DepotOf(bound) != bound)
				{
					moved |= CAiMoves.Move(state, country, unit, unit.Path.Count > 1 ? unit.Path[0] : bound, safe: false);
				}
			}
			// It stays where it is wanted and the position is not over its cap (FINDINGS-moving.md A.4e).
			if (byProvince.TryGetValue(bound, out CPosition? mine) && mine.Want > 0 && mine.Standing <= mine.Cap
				&& (!agent.TargetProvinces.Contains(bound) || BesideRoute(state, agent, bound)))
			{
				continue;
			}
			if (!CUnitMayLeave.Allows(state, country, unit, null, CUnitMayLeave.IsStrict(agent), notDefending: false))
			{
				continue;
			}
			// The positions in their order, read on the listing (FINDINGS-liveread.md, section 17): one where the division stands is
			// taken at once, unless it is bound for a target province away from the hostile routes, when only a position beside
			// one will do; otherwise the lowest key, in single precision as the original's, that it can reach.
			bool besideRouteOnly = agent.TargetProvinces.Contains(bound) && !BesideRoute(state, agent, bound);
			CPosition? best = null;
			float least = StartKey;
			foreach (CPosition position in agent.Positions)
			{
				if (position.Want == 0 || position.Have >= position.Cap)
				{
					continue;
				}
				CProvince province = state.Province(position.Province);
				if (budget <= 0 && IsMoveToAlliedSoil(state, country, network, capitalArea, unit, province))
				{
					continue;
				}
				if (besideRouteOnly)
				{
					if (!BesideRoute(state, agent, position.Province))
					{
						continue;
					}
				}
				else if (position.Province == unit.Location)
				{
					best = position;
					break;
				}
				float ratio = (float)((double)(float)position.Have / (float)position.Want);
				float key = (float)((double)ratio * 100.0);
				if (ratio > 1f)
				{
					key = (float)((double)key + ((double)ratio - 1.0) * 100.0);
				}
				if (atWar)
				{
					long distance = CMapDistance.Between(state.Province(bound), province, state.MapWidth);
					key = (float)((double)(float)distance * ((double)ratio + 0.1f));
					if (HasLandBattle(state, position.Province))
					{
						key = (float)((double)key * 0.66f);
					}
				}
				key = (float)((double)key - CUnitRatings.Terrain(state, country, province, unit, attacking: false) / (double)Whole * 50.0);
				if (network.DepotOf(position.Province) == position.Province)
				{
					key = (float)((double)key * 0.66f);
				}
				if (BesideRoute(state, agent, position.Province))
				{
					key = (float)((double)key * 0.66f);
				}
				if (state.AiTrace is { } keyTrace && keyTrace.Country == country.Tag)
				{
					keyTrace.Write($"  key\t{unit.Name}\t{position.Province}\t{position.Have}\t{position.Want}\t{key:F6}");
				}
				if (key < least && CAiMoves.ReachesByLand(state, country, unit, position.Province))
				{
					least = key;
					best = position;
				}
			}
			if (best is not null)
			{
				if (best.Province == bound)
				{
					continue;
				}
				if (mine is not null)
				{
					mine.Have--;
					if (mine.Province == unit.Location)
					{
						mine.Standing--;
					}
				}
				best.Have++;
				if (best.Province == unit.Location)
				{
					best.Standing++;
				}
				// The original counts it sent whatever the order answers.
				CAiMoves.Move(state, country, unit, best.Province, safe: true);
				moved = true;
			}
			else if (mine is null && agent.SecondRing.Count > 0)
			{
				// Seeded with the original's own number for the unit - the serial of its save pair, not this game's one count
				// (FINDINGS-liveread.md, section 91).
				var draw = new CRandom((uint)state.OriginalSerial(unit.Id));
				int index = (int)(draw.Next() % agent.SecondRing.Count);
				if (state.AiTrace is { } ringTrace && ringTrace.Country == country.Tag)
				{
					ringTrace.Write($"  second ring for {unit.Name}#{unit.Id} (serial {state.OriginalSerial(unit.Id)}): [{string.Join(' ', agent.SecondRing)}], index {index}");
				}
				// Not where it is bound or stands, only where the land reach test lets it, and counted moved whatever the move
				// does, as the original's.
				int ring = agent.SecondRing[index];
				if (ring > 0 && ring != bound && CAiMoves.ReachesByLand(state, country, unit, ring))
				{
					CAiMoves.Move(state, country, unit, ring, safe: true);
					moved = true;
				}
			}
		}
		return moved;
	}

	/// <summary>
	/// The original's <c>CAIUnit_IsMoveFromHomeAreaToAlliedSoil</c>: the target held by a friend - not the country, nobody
	/// or the rebels; on its side, or fighting beside it and not its enemy - and the unit in the capital's area: where it is
	/// bound (or stands, bound nowhere) where the country holds that, or where it stands, held by the country, while bound
	/// for enemy ground (reversing/findings/FINDINGS-liveread.md, section 37).
	/// </summary>
	private static bool IsMoveToAlliedSoil(CGameState state, CCountry country, CSupplyNetwork network, CSupplyArea? capitalArea,
		CUnit unit, CProvince target)
	{
		if (target.Controller is not { } holder || holder == country.Tag || holder.Value == Revolts.CRebelFactions.Rebels
			|| state.Country(holder) is not { } friend)
		{
			return false;
		}
		if (!CSupplyRelations.IsSameSide(country, friend)
			&& (!CCountryLists.FightingTogether(state, country.Tag, holder) || CCountryLists.IsEnemy(state, country, holder)))
		{
			return false;
		}
		int bound = CAgentUnits.DestinationOrPlace(unit);
		if (capitalArea is null)
		{
			return false;
		}
		if (state.Province(bound).Controller == country.Tag)
		{
			return ReferenceEquals(network.AreaOf(bound), capitalArea);
		}
		return IsEnemyHeld(state, country, bound) && state.Province(unit.Location).Controller == country.Tag
			&& ReferenceEquals(network.AreaOf(unit.Location), capitalArea);
	}

	/// <summary>Held by a country at war with this one, or by the rebels.</summary>
	internal static bool IsEnemyHeld(CGameState state, CCountry country, int province)
	{
		return state.Province(province).Controller is { } holder
			&& (holder.Value == Revolts.CRebelFactions.Rebels || CCountryLists.IsEnemy(state, country, holder));
	}

	/// <summary>Bound for a province of a country at war with this one beside where it stands: an attack under way.</summary>
	private static bool AttacksBeside(CGameState state, CCountry country, CUnit unit, int bound)
	{
		if (state.Province(bound).Controller is not { } holder || !CCountryLists.IsEnemy(state, country, holder))
		{
			return false;
		}
		foreach (CProvinceEdge edge in state.Province(unit.Location).Edges)
		{
			if (edge.To == bound)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Each position still short pulls the nearest division from a fuller one, or a fit reserve.</summary>
	private static bool PullShort(CGameState state, CCountry country, CAgent agent, Dictionary<int, CPosition> byProvince, int budget)
	{
		bool moved = false;
		bool atWar = agent.FrontierAtWar;
		int stance = agent.Unit?.Plan.Stance ?? CUnitPlan.Defensive;
		bool airTransports = agent.AirTransports.Count > 0;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		foreach (CPosition position in agent.Positions)
		{
			if (position.Want <= 0)
			{
				continue;
			}
			// Worked out once for the position, before anybody is pulled to it (FINDINGS-liveread.md, section 17).
			int notDepot = network.DepotOf(position.Province) == position.Province ? 0 : 1;
			float fill = (float)((double)(float)(position.Have + notDepot) / (float)position.Want);
			// The pull counts its picks on a count of its own, from the position's have as the pull begins, one up for each
			// pick whatever the pick does to the records - so a division pulled here already, still a candidate, can be picked
			// again and the pick is spent (FINDINGS-liveread.md, section 110).
			int counted = position.Have;
			// And who stands in it, as the pull begins: the test of a unit standing here reads that, not the record the picks
			// raise (FINDINGS-liveread.md, section 149).
			int standingAtStart = position.Standing;
			while (counted < position.Want)
			{
				CProvince here = state.Province(position.Province);
				var keyed = new List<(float Key, CUnit Unit)>();
				foreach (CUnit unit in agent.Committed)
				{
					if (unit.IsRetreating || unit.IsGarrison || unit.Location <= 0
						|| (CArmyRole.Of(unit) == CArmyRole.Airborne && airTransports))
					{
						continue;
					}
					int bound = CAgentUnits.MovementDestinationOrPlace(unit);
					if (network.DepotOf(bound) == bound && CCommandLadder.IsBlocked(unit))
					{
						continue;
					}
					if (budget <= 0 && IsMoveToAlliedSoil(state, country, network, capitalArea, unit, here))
					{
						continue;
					}
					if (AttacksBeside(state, country, unit, bound)
						|| (CCombats.IsHoldingGround(unit) && byProvince.TryGetValue(unit.Location, out CPosition? holding)
							&& holding.Want > 0))
					{
						continue;
					}
					byProvince.TryGetValue(bound, out CPosition? from);
					// Taken without the test where it is bound for no position, or for one that wants nobody, or - standing
					// elsewhere than it is bound - it stands here and nobody stood here as the pull began (0x4CA515-0x4CA543 on
					// the listing).
					bool bypass = from is null || from.Want <= 0
						|| (unit.Location != bound && standingAtStart < 1 && unit.Location == position.Province);
					if (!bypass)
					{
						int count = unit.Location == bound ? from!.Standing : from!.Have;
						int k = notDepot == 0 ? 0 : counted != 0 ? 1 : count <= 1 ? 1 : 0;
						if (fill >= (float)((double)(float)(count - k) / (float)from.Want))
						{
							continue;
						}
						if (atWar && stance < CUnitPlan.Defensive && count <= from.Want
							&& !WithinTwoSteps(state, unit.Location, position.Province))
						{
							continue;
						}
					}
					// Single precision and in this order, the original's (FINDINGS-liveread.md, section 17).
					long squared = CMapDistance.Squared(state.Province(unit.Location), here, state.MapWidth);
					float key = (float)(squared + (double)(float)(unit.DigIn.Raw / (double)Whole) * 10.0);
					key = (float)((double)key + unit.AttackDelay.Raw / Whole);
					key = (float)((double)key - CUnitRatings.Terrain(state, country, here, unit, attacking: false) / (double)Whole * 100.0);
					if (from is null || from.Want <= 0)
					{
						key = (float)((double)key * 0.25);
					}
					else
					{
						key = (float)((double)key + CUnitRatings.Terrain(state, country, state.Province(bound), unit, attacking: false) / (double)Whole * 100.0);
					}
					keyed.Add((key, unit));
				}
				if (state.AiTrace is { } pullTrace && pullTrace.Country == country.Tag)
				{
					pullTrace.Write($"  pull {position.Province}: want {position.Want}, have {position.Have}, standing {position.Standing}, " +
						$"counted {counted}, not depot {notDepot}, fill {fill:F4}; keyed " + string.Join(" ",
						System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderBy(keyed, entry => entry.Key), entry => $"{entry.Unit.Name}={entry.Key:F2}")));
				}
				CUnit? best = null;
				foreach ((float _, CUnit unit) in System.Linq.Enumerable.OrderBy(keyed, entry => entry.Key))
				{
					if (CAiMoves.ReachesByLand(state, country, unit, position.Province)
						&& CUnitMayLeave.Allows(state, country, unit, null, CUnitMayLeave.IsStrict(agent), notDefending: false))
					{
						best = unit;
						break;
					}
				}
				if (best is null)
				{
					moved |= SendReserve(state, country, agent, position, counted);
					break;
				}
				// Where it was going, read before the order changes it; the original counts it sent whatever the order answers,
				// and takes it off that record even where the record is this position's own.
				int leaving = CAgentUnits.MovementDestinationOrPlace(best);
				CAiMoves.Move(state, country, best, position.Province, safe: true);
				moved = true;
				counted++;
				if (byProvince.TryGetValue(leaving, out CPosition? left))
				{
					left.Have--;
					if (leaving == best.Location)
					{
						left.Standing--;
					}
				}
				position.Have++;
				if (best.Location == position.Province)
				{
					position.Standing++;
				}
			}
		}
		return moved;
	}

	/// <summary>
	/// With the frontier at war, fit held-back divisions sent to a position nobody else can fill, one at a time while it is
	/// short, the nearest first; to one that has anybody only beside a hostile route or under an attack going against it.
	/// The original's <c>CAIUnit_SendReservesToPosition</c>.
	/// </summary>
	private static bool SendReserve(CGameState state, CCountry country, CAgent agent, CPosition position, int have)
	{
		if (!agent.FrontierAtWar || agent.HeldBack.Count == 0
			|| (have > 0 && !BesideRoute(state, agent, position.Province) && !UnderLosingAttack(state, position.Province)))
		{
			return false;
		}
		CProvince here = state.Province(position.Province);
		bool sent = false;
		for (int short_ = position.Want - have; short_ > 0; short_--)
		{
			CUnit? best = null;
			long least = long.MaxValue;
			foreach (CUnit unit in agent.HeldBack)
			{
				if (unit.IsRetreating || unit.Location <= 0 || CTheatreFigures.IsFrontProvince(state, state.Province(unit.Location), false))
				{
					continue;
				}
				CUnitShares.Of(state, unit, out long strength, out long organisation);
				long distance = CMapDistance.Between(state.Province(unit.Location), here, state.MapWidth);
				if (strength >= ReserveStrength && organisation >= ReserveOrganisation && distance <= ReserveReach && distance < least)
				{
					least = distance;
					best = unit;
				}
			}
			if (best is null)
			{
				break;
			}
			agent.HeldBack.Remove(best);
			best.IsHeldBack = false;
			CAiMoves.Move(state, country, best, position.Province, safe: true);
			sent = true;
		}
		return sent;
	}

	internal static bool HasLandBattle(CGameState state, int province)
	{
		foreach (CCombat combat in state.Combats)
		{
			if (combat.Province == province && combat.Kind == ECombatKind.Land)
			{
				return true;
			}
		}
		return false;
	}

	private static bool UnderLosingAttack(CGameState state, int province)
	{
		foreach (CCombat combat in state.Combats)
		{
			if (combat.Province == province && combat.Kind == ECombatKind.Land
				&& CCombatShares.AttackerShare(combat) > Whole / 2)
			{
				return true;
			}
		}
		return false;
	}

	public static bool BesideRoute(CGameState state, CAgent agent, int province)
	{
		foreach (int route in agent.HostileRoutes)
		{
			if (CAreaScores.Borders(state, route, province))
			{
				return true;
			}
		}
		return false;
	}

	private static bool WithinTwoSteps(CGameState state, int from, int to)
	{
		if (from == to || CAreaScores.Borders(state, from, to))
		{
			return true;
		}
		foreach (CProvinceEdge edge in state.Province(from).Edges)
		{
			if (CAreaScores.Borders(state, edge.To, to))
			{
				return true;
			}
		}
		return false;
	}
}
