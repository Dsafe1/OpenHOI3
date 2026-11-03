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
		Count(agent, byProvince);
		if (!any)
		{
			foreach (CPosition position in agent.Positions)
			{
				if (position.Want != ShutOut)
				{
					position.Want = DugIn(state, position.Province) ? 2 : 1;
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
		moved |= PullShort(state, country, agent, byProvince);
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

	private static void Count(CAgent agent, Dictionary<int, CPosition> byProvince)
	{
		foreach (CUnit unit in agent.Committed)
		{
			if (unit.IsGarrison)
			{
				continue;
			}
			int bound = CAgentUnits.DestinationOrPlace(unit);
			if (byProvince.TryGetValue(bound, out CPosition? position))
			{
				position.Have++;
			}
			if (byProvince.TryGetValue(unit.Location, out CPosition? standing))
			{
				standing.Standing++;
			}
		}
	}

	private static bool DugIn(CGameState state, int province)
	{
		foreach (CUnit unit in state.Positions.Of(province))
		{
			if (unit.DigIn.Raw > 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The position with the least want, one at its cap sorted after; the first of equals.</summary>
	private static CPosition? Least(CAgent agent)
	{
		CPosition? least = null;
		long key = long.MaxValue;
		foreach (CPosition position in agent.Positions)
		{
			if (position.Want == ShutOut)
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
			var keyed = new List<(long Key, CPosition Position)>();
			foreach (CPosition position in agent.Positions)
			{
				if (position.Want <= 0 || position.Have >= position.Cap || position.Have >= position.Want)
				{
					continue;
				}
				CProvince province = state.Province(position.Province);
				if (budget <= 0 && ReferenceEquals(network.AreaOf(unit.Location), capitalArea)
					&& province.Controller != country.Tag)
				{
					continue;
				}
				long key;
				long fill = (long)position.Have * Whole / position.Want;
				if (atWar)
				{
					long distance = CMapDistance.Between(state.Province(unit.Location), province, state.MapWidth);
					key = distance * (fill + 100);
				}
				else
				{
					key = fill * 100;
				}
				if (HasLandBattle(state, position.Province))
				{
					key = key * Cheaper / Whole;
				}
				if (network.DepotOf(position.Province) == position.Province)
				{
					key = key * Cheaper / Whole;
				}
				if (BesideRoute(state, agent, position.Province))
				{
					key = key * Cheaper / Whole;
				}
				key -= 50 * CUnitRatings.Terrain(state, country, province, unit, attacking: false);
				keyed.Add((key, position));
			}
			CPosition? best = null;
			foreach ((long _, CPosition position) in System.Linq.Enumerable.OrderBy(keyed, entry => entry.Key))
			{
				if (CAiMoves.CanReach(state, country, unit, position.Province))
				{
					best = position;
					break;
				}
			}
			if (best is not null)
			{
				if (CAiMoves.Move(state, country, unit, best.Province, safe: true))
				{
					moved = true;
					if (mine is not null)
					{
						mine.Have--;
					}
					best.Have++;
				}
			}
			else if (mine is null && agent.SecondRing.Count > 0)
			{
				var draw = new CRandom((uint)unit.Id);
				int index = (int)(draw.Next() % agent.SecondRing.Count);
				moved |= CAiMoves.Move(state, country, unit, agent.SecondRing[index], safe: true);
			}
		}
		return moved;
	}

	/// <summary>Held by a country at war with this one, or by the rebels.</summary>
	private static bool IsEnemyHeld(CGameState state, CCountry country, int province)
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
	private static bool PullShort(CGameState state, CCountry country, CAgent agent, Dictionary<int, CPosition> byProvince)
	{
		bool moved = false;
		bool atWar = agent.FrontierAtWar;
		int stance = agent.Unit?.Plan.Stance ?? CUnitPlan.Defensive;
		bool airTransports = agent.AirTransports.Count > 0;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CPosition position in agent.Positions)
		{
			while (position.Want > 0 && position.Have < position.Want)
			{
				CProvince here = state.Province(position.Province);
				var keyed = new List<(long Key, CUnit Unit)>();
				foreach (CUnit unit in agent.Committed)
				{
					if (unit.IsRetreating || unit.IsGarrison || unit.Location <= 0
						|| (CArmyRole.Of(unit) == CArmyRole.Airborne && airTransports))
					{
						continue;
					}
					int bound = CAgentUnits.DestinationOrPlace(unit);
					if (bound == position.Province || AttacksBeside(state, country, unit, bound)
						|| (CCombats.IsHoldingGround(unit) && byProvince.TryGetValue(unit.Location, out CPosition? holding)
							&& holding.Want > 0))
					{
						continue;
					}
					byProvince.TryGetValue(bound, out CPosition? from);
					bool bypass = from is null || from.Want <= 0
						|| (position.Standing < 1 && unit.Location == position.Province);
					if (!bypass)
					{
						int count = unit.Location == bound ? from!.Standing : from!.Have;
						int notDepot = network.DepotOf(position.Province) == position.Province ? 0 : 1;
						int k = notDepot == 0 ? 0 : position.Have != 0 ? 1 : count <= 1 ? 1 : 0;
						if ((long)(position.Have + notDepot) * from.Want >= (long)(count - k) * position.Want)
						{
							continue;
						}
						if (atWar && stance < CUnitPlan.Defensive && count <= from.Want
							&& !WithinTwoSteps(state, unit.Location, position.Province))
						{
							continue;
						}
					}
					long squared = CMapDistance.Squared(state.Province(unit.Location), here, state.MapWidth);
					long key = squared * 16 + unit.DigIn.Raw / 100 + unit.AttackDelay.Raw / Whole
						- 100 * CUnitRatings.Terrain(state, country, here, unit, attacking: false) / Whole;
					if (from is null || from.Want <= 0)
					{
						key /= 4;
					}
					else
					{
						key += 100 * CUnitRatings.Terrain(state, country, state.Province(bound), unit, attacking: false) / Whole;
					}
					keyed.Add((key, unit));
				}
				CUnit? best = null;
				foreach ((long _, CUnit unit) in System.Linq.Enumerable.OrderBy(keyed, entry => entry.Key))
				{
					if (CAiMoves.CanReach(state, country, unit, position.Province)
						&& CUnitMayLeave.Allows(state, country, unit, null, CUnitMayLeave.IsStrict(agent), notDefending: false))
					{
						best = unit;
						break;
					}
				}
				if (best is null)
				{
					moved |= SendReserve(state, country, agent, position);
					break;
				}
				if (!CAiMoves.Move(state, country, best, position.Province, safe: true))
				{
					break;
				}
				moved = true;
				int leaving = CAgentUnits.DestinationOrPlace(best);
				if (byProvince.TryGetValue(leaving, out CPosition? left) && !ReferenceEquals(left, position))
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

	/// <summary>At war, a fit held-back division sent to a position beside a hostile route or under an attack going against it.</summary>
	private static bool SendReserve(CGameState state, CCountry country, CAgent agent, CPosition position)
	{
		if (!state.IsAtWar(country.Tag) || (!BesideRoute(state, agent, position.Province)
			&& !UnderLosingAttack(state, position.Province)))
		{
			return false;
		}
		CProvince here = state.Province(position.Province);
		foreach (CUnit unit in agent.HeldBack)
		{
			if (unit.Location <= 0 || CTheatreFigures.IsFrontProvince(state, state.Province(unit.Location), false))
			{
				continue;
			}
			CUnitShares.Of(state, unit, out long strength, out long organisation);
			if (strength < ReserveStrength || organisation < ReserveOrganisation
				|| CMapDistance.Between(state.Province(unit.Location), here, state.MapWidth) > ReserveReach)
			{
				continue;
			}
			if (CAiMoves.Move(state, country, unit, position.Province, safe: true))
			{
				unit.IsHeldBack = false;
				position.Have++;
				return true;
			}
		}
		return false;
	}

	private static bool HasLandBattle(CGameState state, int province)
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
