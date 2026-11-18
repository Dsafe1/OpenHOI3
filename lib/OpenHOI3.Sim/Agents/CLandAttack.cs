using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's attack, where its frontier is at war: every enemy front province scored, the best first given the units
/// beside it, the cheapest first, while each makes the odds better, and attacked where the odds reach the stance's bar
/// - a march in for a unit that may go, a support attack for one that must stay - or the attack called off where they do
/// not; then attacks going badly called off. The original's <c>CAIUnit_ManageLandUnits</c>. See
/// <c>docs/mechanics/moving.md</c>, section 11.
/// </summary>
public static class CLandAttack
{
	private const long Scale = CFixedPoint.Scale;

	private const long Ramp = 500;

	private const long OnWay = 90 * Scale;

	private const long BesideWay = 45 * Scale;

	private const long AllTargets = 1000 * Scale;

	private const long OnRoute = 1000 * Scale;

	private const long BesideRoute = 500 * Scale;

	private const long AsBlitz = 500 * Scale;

	private const long GarrisonRating = 95 * Scale;

	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is not { } headquarters || agent.Committed.Count == 0 || !agent.FrontierAtWar)
		{
			return;
		}
		int stance = headquarters.Plan.Stance;
		List<(CProvince Province, long Weight)> targets = Targets(state, country, agent, headquarters, stance);
		foreach ((CProvince target, long weight) in targets)
		{
			if (state.AiTrace is { } weightTrace && weightTrace.Country == country.Tag)
			{
				weightTrace.Write($"  weight\t{state.TotalHours}\t{headquarters.Name}\t{target.Id}\t{weight}\t{stance}");
			}
			Attack(state, country, agent, headquarters, target, weight >= AsBlitz ? CUnitPlan.Blitzing : stance, stance);
		}
		CallOff(state, country, agent);
	}

	// ---- phase 1: the targets

	private static List<(CProvince, long)> Targets(CGameState state, CCountry country, CAgent agent, CUnit headquarters,
		int stance)
	{
		var scored = new List<(CProvince, long)>();
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		long most = 0;
		long least = long.MaxValue;
		CProvince? depot = agent.Area.Count > 0 ? state.Province(network.DepotOf(agent.Area[0])) : null;
		if (depot is not null && depot.Id > 0)
		{
			foreach (int id in agent.FrontProvinces)
			{
				long d = CMapDistance.Between(state.Province(id), depot, state.MapWidth);
				most = System.Math.Max(most, d);
				least = System.Math.Min(least, d);
			}
		}
		long spread = most > least ? most - least : 0;
		foreach (int id in agent.FrontProvinces)
		{
			CProvince p = state.Province(id);
			if (p.Controller is not { } holder || !CCountryLists.IsEnemy(state, country, holder))
			{
				continue;
			}
			long weight = Scale;
			if (spread > 0 && depot is not null && depot.Id > 0)
			{
				weight = (most - CMapDistance.Between(p, depot, state.MapWidth)) * Ramp * Scale / spread;
			}
			if (network.AreaOf(id) is { } area && area.Provinces.Count < 3)
			{
				weight *= 2;
			}
			var approaches = new List<int>();
			var enemies = new List<int>();
			bool worth = false;
			bool allTargets = true;
			foreach (CProvinceEdge edge in p.Edges)
			{
				if (edge.Kind == EProvinceEdgeKind.Impassable)
				{
					continue;
				}
				CProvince q = state.Province(edge.To);
				if (q.IsSea)
				{
					continue;
				}
				if (q.Controller is { } other && CCountryLists.IsEnemy(state, country, other))
				{
					weight += Priority(headquarters, q.Id) * Scale / 2;
					enemies.Add(q.Id);
				}
				if (!CApproaches.IsFriendly(state, country, q.Controller))
				{
					continue;
				}
				if (!agent.TargetProvinces.Contains(q.Id))
				{
					allTargets = false;
				}
				approaches.Add(q.Id);
				if (q.Controller == country.Tag || state.Country(q.Controller) is not null)
				{
					worth = true;
				}
			}
			if (!worth)
			{
				continue;
			}
			if (stance is CUnitPlan.Attacking or CUnitPlan.Blitzing)
			{
				weight += Priority(headquarters, id) * Scale;
				foreach (List<int> way in new[] { headquarters.Plan.Path, headquarters.Plan.Path2 })
				{
					foreach (int step in way)
					{
						weight += step == id ? OnWay : CAreaScores.Borders(state, step, id) ? BesideWay : 0;
					}
				}
				if (allTargets)
				{
					weight += AllTargets;
				}
				weight += agent.Nearness.GetValueOrDefault(id) * Scale;
			}
			long width = stance == CUnitPlan.Attacking ? 20 : stance == CUnitPlan.Blitzing ? 10 : 50;
			weight += (approaches.Count - 1) * width * Scale;
			bool onRoute = false;
			foreach (int route in agent.HostileRoutes)
			{
				if (route == id)
				{
					weight += OnRoute;
					onRoute = true;
				}
				else if (CAreaScores.Borders(state, route, id))
				{
					weight += BesideRoute;
				}
			}
			if (!onRoute && DeadEnd(state, country, agent, network, p, approaches, stance, ref weight))
			{
				continue;
			}
			if (stance >= CUnitPlan.Attacking)
			{
				foreach (int e in enemies)
				{
					if (IsSalient(state, country, e))
					{
						weight += (stance == CUnitPlan.Attacking ? 125 : 250) * Scale;
					}
				}
				if (approaches.Count > 1 && Disconnected(state, approaches))
				{
					weight += (stance == CUnitPlan.Attacking ? 200 : 400) * Scale;
				}
			}
			if (CTheatreFigures.NavalBase(state) is { } naval && p.BuildingMaxLevel(naval).Raw > 0)
			{
				weight += (p.BuildingMaxLevel(naval).Raw / Scale + 20) * 5 * Scale;
			}
			int at = 0;
			while (at < scored.Count && scored[at].Item2 >= weight)
			{
				at++;
			}
			scored.Insert(at, (p, weight));
		}
		return scored;
	}

	private static int Priority(CUnit headquarters, int province)
	{
		foreach (CObjective objective in headquarters.Plan.Objectives)
		{
			if (objective.Province == province)
			{
				return objective.Priority;
			}
		}
		return 0;
	}

	/// <summary>A single way in that leads only into a cul-de-sac another front province covers: dropped, or cut at stance 3 and 4.</summary>
	private static bool DeadEnd(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, CProvince p,
		List<int> approaches, int stance, ref long weight)
	{
		if (approaches.Count != 1 || network.AreaOf(p.Id) is not { } area || area.Provinces.Count <= 5)
		{
			return false;
		}
		int single = approaches[0];
		if (network.AreaOf(single) is not { } its || its.Provinces.Count <= 3)
		{
			return false;
		}
		int onward = 0;
		bool sea = false;
		foreach (CProvinceEdge edge in state.Province(single).Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea)
			{
				// Only a sea the target province touches too counts (FINDINGS-liveread.md, section 59).
				if (p.EdgeTo(beside.Id) is not null)
				{
					sea = true;
				}
			}
			else if (CApproaches.IsFriendly(state, country, beside.Controller))
			{
				onward++;
			}
		}
		if (onward >= 3 || sea)
		{
			return false;
		}
		bool redundant = false;
		foreach (int other in agent.FrontProvinces)
		{
			if (other == p.Id || !ReferenceEquals(network.AreaOf(other), area))
			{
				continue;
			}
			foreach (CProvinceEdge edge in state.Province(other).Edges)
			{
				CProvince q = state.Province(edge.To);
				if (!q.IsSea && edge.To != single && CApproaches.IsFriendly(state, country, q.Controller)
					&& !CAreaScores.Borders(state, p.Id, edge.To))
				{
					redundant = true;
				}
			}
		}
		if (!redundant)
		{
			return false;
		}
		if (onward < 2 || stance < CUnitPlan.Attacking)
		{
			return true;
		}
		weight = stance == CUnitPlan.Attacking ? weight / 4 : weight / 2;
		return false;
	}

	/// <summary>An enemy province with a friendly neighbour and fewer than three enemy ones.</summary>
	private static bool IsSalient(CGameState state, CCountry country, int province)
	{
		bool friendly = false;
		int enemies = 0;
		foreach (CProvinceEdge edge in state.Province(province).Edges)
		{
			CProvince q = state.Province(edge.To);
			if (q.IsSea)
			{
				continue;
			}
			if (CApproaches.IsFriendly(state, country, q.Controller))
			{
				friendly = true;
			}
			else if (q.Controller is { } holder && CCountryLists.IsEnemy(state, country, holder))
			{
				enemies++;
			}
		}
		return friendly && enemies < 3;
	}

	/// <summary>Some approach borders none of the others.</summary>
	private static bool Disconnected(CGameState state, List<int> approaches)
	{
		foreach (int one in approaches)
		{
			bool touches = false;
			foreach (int other in approaches)
			{
				if (one != other && CAreaScores.Borders(state, one, other))
				{
					touches = true;
					break;
				}
			}
			if (!touches)
			{
				return true;
			}
		}
		return false;
	}

	// ---- phase 2: one target

	private static void Attack(CGameState state, CCountry country, CAgent agent, CUnit headquarters, CProvince p,
		int effStance, int planStance)
	{
		bool shared = SharedBoundary(state, country, p) > 1;
		bool mayAttack = planStance > CUnitPlan.Prepare || p.Controller?.Value == CRebelFactions.Rebels
			|| (CSupplyNetworks.Refresh(state).AreaOf(p.Id) is { } area && area.Provinces.Count < 6)
			|| (effStance >= CUnitPlan.Attacking && !HasLandUnit(state, p.Id)) || shared;
		var attackers = new List<CUnit>();
		var candidates = new List<(CUnit Unit, long Cost)>();
		foreach (CProvinceEdge edge in p.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable || state.Province(edge.To).IsSea)
			{
				continue;
			}
			foreach (CUnit unit in state.Positions.Of(edge.To))
			{
				if (!CTheatres.IsLand(unit) || unit.Children.Count > 0 || unit.Owner != country.Tag)
				{
					continue;
				}
				int bound = CAgentUnits.Destination(unit);
				if (bound == p.Id)
				{
					attackers.Add(unit);
				}
				if (!MayCommit(state, country, agent, unit, bound, mayAttack, shared))
				{
					continue;
				}
				long cost = Cost(state, country, agent, unit, p);
				int at = 0;
				while (at < candidates.Count && candidates[at].Cost <= cost)
				{
					at++;
				}
				candidates.Insert(at, (unit, cost));
			}
		}
		List<CUnit> defenders = Defenders(state, country, p);
		long odds = CAttackOdds.Of(state, attackers, defenders, p);
		long before = odds;
		var mustStay = new HashSet<CUnit>();
		long oddsBar = effStance == CUnitPlan.Defensive ? 5 * Scale : effStance == CUnitPlan.Blitzing ? 3 * Scale : 4 * Scale;
		long strengthBar = effStance == CUnitPlan.Defensive ? 700 : effStance == CUnitPlan.Blitzing ? 500 : 600;
		long organisationBar = effStance == CUnitPlan.Defensive ? 750 : effStance == CUnitPlan.Blitzing ? 500 : 600;
		bool largeArea = CSupplyNetworks.Refresh(state).AreaOf(p.Id) is { } pArea && pArea.Provinces.Count > 5;
		var refused = new System.Text.StringBuilder();
		foreach ((CUnit unit, long _) in candidates)
		{
			if (attackers.Contains(unit) || (unit.AttackDelay.Raw > 0 && defenders.Count > 0) || unit.IsRetreating)
			{
				refused.Append(unit.AttackDelay.Raw > 0 ? " delay" : unit.IsRetreating ? " retreating" : " already");
				continue;
			}
			if (CTransportReservation.IsReserved(state, unit))
			{
				refused.Append(" reserved");
				continue;
			}
			if (odds >= oddsBar && EnemyNeighbours(state, country, unit.Location) > 1)
			{
				refused.Append(" enough");
				continue;
			}
			if (largeArea && !shared && (defenders.Count > 0 || effStance < CUnitPlan.Attacking))
			{
				CUnitShares.Of(state, unit, out long strength, out long organisation);
				if (strength < strengthBar || organisation < organisationBar)
				{
					refused.Append($" worn({strength}/{organisation})");
					continue;
				}
			}
			bool stay = MustStay(state, country, agent, unit, attackers, p.Id, effStance);
			attackers.Add(unit);
			long trial = CAttackOdds.Of(state, attackers, defenders, p);
			if (odds > trial)
			{
				attackers.Remove(unit);
				continue;
			}
			odds = trial;
			if (stay)
			{
				mustStay.Add(unit);
			}
		}
		long attackBar = effStance == CUnitPlan.Defensive ? 3 * Scale : effStance == CUnitPlan.Blitzing ? 1100 : 1500;
		long cancelBar = 800;
		if (shared || PortSupply(state, attackers, p))
		{
			attackBar = 200;
			cancelBar = 10;
		}
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write($"  attack {p.Id} stance {effStance}/{planStance}: may {mayAttack}, candidates {candidates.Count}, " +
				$"attackers {attackers.Count} ({mustStay.Count} must stay), defenders {defenders.Count}, odds {before} -> {odds}, " +
				$"bar {attackBar};{refused} [{string.Join(", ", candidates.ConvertAll(c => $"{c.Unit.Name}:{c.Cost}"))}]; " +
				$"attackers [{string.Join(", ", attackers.ConvertAll(u => $"{u.Name}@{u.Location}"))}]");
		}
		if (mayAttack && odds >= attackBar && mustStay.Count < attackers.Count)
		{
			foreach (CUnit unit in attackers)
			{
				int bound = CAgentUnits.Destination(unit);
				if ((bound > 0 && IsEnemyHeld(state, country, bound)) || unit.IsRetreating)
				{
					continue;
				}
				if (mustStay.Contains(unit))
				{
					// A support attack is a posted order and binds the unit to nothing for the rest of the pass, so a later
					// target may still take it (FINDINGS-liveread.md, section 124).
					if (unit.Order != EUnitOrder.SupportAttack)
					{
						state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, unit.Id, EUnitOrder.SupportAttack, p.Id));
					}
				}
				else
				{
					CAiMoves.Move(state, country, unit, p.Id, safe: false);
				}
			}
		}
		else if (cancelBar > before && cancelBar > odds && attackers.Count > 0)
		{
			foreach (CUnit unit in attackers)
			{
				if (CAgentUnits.Destination(unit) == p.Id && !unit.IsRetreating)
				{
					CAiMoves.StandDown(state, country, unit, $"attack on {p.Id} called off at odds {odds}");
				}
			}
		}
	}

	private static bool MayCommit(CGameState state, CCountry country, CAgent agent, CUnit unit, int bound, bool mayAttack,
		bool shared)
	{
		// Only a unit already bound somewhere is asked whether it goes to an enemy's province, is a garrison or is another
		// agent's; one standing still is a candidate whichever it is (FINDINGS-liveread.md, section 58).
		if (bound > 0 && (IsEnemyHeld(state, country, bound) || unit.IsGarrison || !ReferenceEquals(Topmost(unit), agent)))
		{
			return false;
		}
		if (!mayAttack)
		{
			return false;
		}
		if (CCombats.IsHoldingGround(unit) && (bound > 0 || !shared || !agent.TargetProvinces.Contains(unit.Location)))
		{
			return false;
		}
		if (unit.IsHeldBack && !shared)
		{
			return false;
		}
		return !CTransportReservation.IsReserved(state, unit);
	}

	/// <summary>
	/// 10 for each enemy neighbour where it stands, less 10 times its terrain figure at the target, +100 for a garrison; / 10
	/// for another agent's unit, else / 5 with a blocking order.
	/// </summary>
	private static long Cost(CGameState state, CCountry country, CAgent agent, CUnit unit, CProvince p)
	{
		long cost = EnemyNeighbours(state, country, unit.Location) * 10 * Scale
			- 10 * CUnitRatings.Terrain(state, country, p, unit, attacking: true);
		if (CUnitRatings.Garrison(state, unit) > GarrisonRating)
		{
			cost += 100 * Scale;
		}
		if (!ReferenceEquals(Topmost(unit), agent))
		{
			cost /= 10;
		}
		else if (CCommandLadder.IsBlocked(unit))
		{
			cost /= 5;
		}
		return cost;
	}

	/// <summary>
	/// Whether a unit joining an attack stays where it is and supports: something beside it is defended and nobody is
	/// bound there, it stands where a depot supplies, nobody there can stand in for it - a unit already joined to this attack does
	/// not - and it is not in a target area (<c>reversing/findings/FINDINGS-aiconsumer.md</c>;
	/// <c>reversing/findings/FINDINGS-liveread.md</c>, section 54).
	/// </summary>
	private static bool MustStay(CGameState state, CCountry country, CAgent agent, CUnit unit, List<CUnit> attackers, int target,
		int stance)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (Uncovered(state, country, unit, target, stance) == 0 || network.DepotOf(unit.Location) == 0)
		{
			return false;
		}
		foreach (CUnit other in state.Positions.Of(unit.Location))
		{
			if (!ReferenceEquals(other, unit) && CTheatres.IsLand(other) && MayStandIn(state, country, other) && !other.IsFighting
				&& CArmyRole.Of(other) != CArmyRole.Headquarters && CAgentUnits.Destination(other) == 0
				&& !CTransportReservation.IsReserved(state, other) && !attackers.Contains(other))
			{
				return false;
			}
		}
		return !agent.TargetProvinces.Contains(unit.Location);
	}

	/// <summary>Never a rebel unit; a unit of the country's own, or of a co-belligerent it is not at war with while it is not the rebels itself.</summary>
	private static bool MayStandIn(CGameState state, CCountry country, CUnit other)
	{
		if (other.Owner.Value == CRebelFactions.Rebels)
		{
			return false;
		}
		if (other.Owner == country.Tag)
		{
			return true;
		}
		if (country.Tag.Value == CRebelFactions.Rebels || state.Country(other.Owner) is not { } owner)
		{
			return false;
		}
		return CSupplyRelations.IsFightingWith(state, country, owner) && !CCountryLists.IsEnemy(state, country, other.Owner);
	}

	/// <summary>
	/// The original's <c>ai_uncovered_enemy_neighbours</c>, worked out for the unit and the province it is offered: the
	/// neighbours of its province, by edges not impassable, in a real area and held by an enemy - **the target itself left
	/// out** - where anybody stands or the holder's friends are stepping in (or any such neighbour, where the unit's area is
	/// one province and the neighbour's more than five), that no land unit of the country in the unit's province is bound
	/// for - nor, at stance 4, any of the country's (<c>reversing/findings/FINDINGS-liveread.md</c>, section 85).
	/// </summary>
	private static int Uncovered(CGameState state, CCountry country, CUnit unit, int target, int stance)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? own = network.AreaOf(unit.Location);
		int count = 0;
		foreach (CProvinceEdge edge in state.Province(unit.Location).Edges)
		{
			CProvince r = state.Province(edge.To);
			if (edge.Kind == EProvinceEdgeKind.Impassable || r.IsSea || network.AreaOf(edge.To) is not { } area
				|| !IsEnemyHeld(state, country, edge.To) || edge.To == target)
			{
				continue;
			}
			bool lone = own is not null && own.Provinces.Count == 1 && area.Provinces.Count > 5;
			if (!lone && state.Positions.Of(edge.To).Count + SteppingIn(state, r) < 1)
			{
				continue;
			}
			bool covered = false;
			foreach (CUnit other in state.Positions.Of(unit.Location))
			{
				if (CTheatres.IsLand(other) && other.Owner == country.Tag && CAgentUnits.Destination(other) == edge.To)
				{
					covered = true;
					break;
				}
			}
			if (!covered && stance == CUnitPlan.Blitzing)
			{
				foreach (CUnit other in country.AllUnits())
				{
					if (CTheatres.IsLand(other) && CAgentUnits.Destination(other) == edge.To)
					{
						covered = true;
						break;
					}
				}
			}
			if (!covered)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// The land units beside a province whose way ends in it, of its holder or a friend of the holder's: the original's
	/// <c>CollectUnitsMovingIntoProvince</c> asked for friends, which asks the path's last province and not its next step - so
	/// a headquarters passing through on a longer way is not counted (<c>moving.md</c>, section 11).
	/// </summary>
	private static int SteppingIn(CGameState state, CProvince province)
	{
		if (state.Country(province.Controller) is not { } holder)
		{
			return 0;
		}
		int count = 0;
		foreach (CProvinceEdge edge in province.Edges)
		{
			foreach (CUnit other in state.Positions.Of(edge.To))
			{
				if (CTheatres.IsLand(other) && other.Path.Count > 0 && other.Path[^1] == province.Id
					&& CCountryLists.IsFriend(state, holder, other.Owner, withAccess: false))
				{
					count++;
				}
			}
		}
		return count;
	}

	private static List<CUnit> Defenders(CGameState state, CCountry country, CProvince p)
	{
		var defenders = new List<CUnit>();
		foreach (CUnit unit in state.Positions.Of(p.Id))
		{
			if (CTheatres.IsLand(unit) && (unit.Owner.Value == CRebelFactions.Rebels || CCountryLists.IsEnemy(state, country, unit.Owner)))
			{
				defenders.Add(unit);
			}
		}
		return defenders;
	}

	/// <summary>The blocks of land of the target's friendly neighbours.</summary>
	private static int SharedBoundary(CGameState state, CCountry country, CProvince p)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var groups = new HashSet<CSupplyGroup>();
		foreach (CProvinceEdge edge in p.Edges)
		{
			CProvince q = state.Province(edge.To);
			if (!q.IsSea && CApproaches.IsFriendly(state, country, q.Controller) && network.AreaOf(edge.To)?.RegionGroup is { } group)
			{
				groups.Add(group);
			}
		}
		return groups.Count;
	}

	/// <summary>An attacker standing where no depot supplies it while the target has a working port.</summary>
	private static bool PortSupply(CGameState state, List<CUnit> attackers, CProvince p)
	{
		if (p.Owner is null || p.PortSea <= 0 || CTheatreFigures.NavalBase(state) is not { } naval || p.BuildingMaxLevel(naval).Raw <= 0)
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CUnit unit in attackers)
		{
			if (unit.Location > 0 && network.DepotOf(unit.Location) == 0)
			{
				return true;
			}
		}
		return false;
	}

	// ---- phase 3: calling off

	private static void CallOff(CGameState state, CCountry country, CAgent agent)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CUnit unit in agent.Land.ToArray())
		{
			if (unit.Combats.Count == 0)
			{
				continue;
			}
			int bound = CAgentUnits.Destination(unit);
			if (bound <= 0 || !IsEnemyHeld(state, country, bound) || agent.HostileRoutes.Contains(bound))
			{
				continue;
			}
			if (agent.TargetProvinces.Contains(unit.Location))
			{
				CSupplyArea? own = network.AreaOf(unit.Location);
				bool allOwn = true;
				foreach (CProvinceEdge edge in state.Province(bound).Edges)
				{
					CProvince q = state.Province(edge.To);
					if (!q.IsSea && CApproaches.IsFriendly(state, country, q.Controller) && !ReferenceEquals(network.AreaOf(edge.To), own))
					{
						allOwn = false;
					}
				}
				if (allOwn)
				{
					CAiMoves.StandDown(state, country, unit, "call-off: target area");
				}
				continue;
			}
			foreach (CCombat combat in unit.Combats.ToArray())
			{
				if (combat.Kind != ECombatKind.Land)
				{
					continue;
				}
				if (combat.Defender.Units.Contains(unit))
				{
					// A land battle always answers that it may be left, retreating or not (FINDINGS-liveread.md, section 20).
					if (CCombatShares.AttackerShare(combat) > Scale / 2)
					{
						CAiMoves.StandDown(state, country, unit, "call-off: losing defence");
						break;
					}
				}
				else if (unit.Order == EUnitOrder.SupportAttack
					&& combat.Attacker.Units.TrueForAll(other => other.Order is not (EUnitOrder.None or EUnitOrder.Move)))
				{
					CAiMoves.StandDown(state, country, unit, "call-off: support attack");
					break;
				}
			}
		}
	}

	// ---- shared

	private static bool IsEnemyHeld(CGameState state, CCountry country, int province)
	{
		return state.Province(province).Controller is { } holder
			&& (holder.Value == CRebelFactions.Rebels || CCountryLists.IsEnemy(state, country, holder));
	}

	private static bool HasLandUnit(CGameState state, int province)
	{
		foreach (CUnit unit in state.Positions.Of(province))
		{
			if (CTheatres.IsLand(unit))
			{
				return true;
			}
		}
		return false;
	}

	private static int EnemyNeighbours(CGameState state, CCountry country, int province)
	{
		int count = 0;
		foreach (CProvinceEdge edge in state.Province(province).Edges)
		{
			if (edge.Kind != EProvinceEdgeKind.Impassable && !state.Province(edge.To).IsSea && IsEnemyHeld(state, country, edge.To))
			{
				count++;
			}
		}
		return count;
	}

	private static CAgent? Topmost(CUnit unit)
	{
		CAgent? found = null;
		for (CUnit? at = unit; at is not null; at = at.Parent)
		{
			if (at.Agent is { } agent)
			{
				found = agent;
			}
		}
		return found;
	}
}
