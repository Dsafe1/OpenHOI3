using System.Collections.Generic;
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
/// An agent's land block: its divisions placed - the line first, the garrisons only where the line moved nothing, the
/// reserve only where neither did - then defenders taken out of a battle they are losing, and its marches looked at.
/// The original's <c>CAIUnit::PositionLandUnits</c>, <c>CAIUnit_FallBackFromLosingDefences</c> and
/// <c>CAIUnit_CheckLandMarches</c>. See <c>docs/mechanics/moving.md</c>, sections 7 to 9.
/// </summary>
public static class CLandBlock
{
	private const long Whole = 1000;

	private const int FewForts = 3;

	private const int OldBattle = 12;

	private const int OldBattleAttacking = 25;

	/// <summary>A defence is given up where the attacker holds more than this share.</summary>
	private const long Losing = 700;

	private const int SmallArea = 3;

	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		Position(state, country, agent);
		FallBack(state, country, agent);
		CheckMarches(state, country, agent);
	}

	/// <summary>The dispatcher: the allied-soil budget, the split, the line, else the garrisons, else the reserve.</summary>
	private static void Position(CGameState state, CCountry country, CAgent agent)
	{
		agent.FullPassPending = false;
		if (agent.Unit is null)
		{
			return;
		}
		int n = agent.CommittedAtFirst.Count;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int budget;
		List<CCountry> members = CCountryLists.FactionMembers(state, country.Faction);
		if (members.Count > 0 && ReferenceEquals(members[0], country))
		{
			budget = n / 2;
		}
		else if (network.AreaOf(country.ActingCapital) is { } capitalArea && CAreaHostility.TouchesHostileArea(state, capitalArea))
		{
			budget = n / 5;
		}
		else
		{
			budget = n / 3;
		}
		budget -= agent.OnAlliedSoil;
		(int garrisons, int line, List<int> targets, List<CUnit> ordered) = CLandSplit.Split(state, country, agent);
		bool moved = false;
		var trace = state.AiTrace is { } t && t.Country == country.Tag ? t.Write : null;
		trace?.Invoke($"{state.Date}.{state.Hour} {agent.Unit.Name}: committed {n}, held back {agent.HeldBack.Count}, " +
			$"budget {budget}, line {line}, garrisons {garrisons}, targets {targets.Count}, positions {agent.Positions.Count}");
		trace?.Invoke("  committed: " + string.Join(" ", agent.Committed.ConvertAll(u =>
			$"{u.Name}@{u.Location}{(u.IsGarrison ? "g" : "")}{(u.IsRetreating ? $"r({u.Order},{u.AttackDelay.Raw},{u.Combats.Count})" : "")}{(u.IsFighting ? "f" : "")}{(u.IsMoving ? ">" + u.Path[^1] : "")}{(CAgentUnits.Destination(u) is var d && d > 0 && d != u.Location ? "~" + d : "")}")));
		if (line > 0 && agent.Positions.Count > 0)
		{
			moved = CLineFill.Fill(state, country, agent, line, budget);
			trace?.Invoke($"  line moved {moved}: " + string.Join(" ", agent.Positions.FindAll(p => p.Want > 0 || p.Standing > 0
				|| p.Have > 0).ConvertAll(p => $"{p.Province}:{p.Want}/{p.Have}/{p.Standing}/{p.Cap}")));
			trace?.Invoke("  scores: " + string.Join(" ", agent.Positions.ConvertAll(p => $"{p.Province}={p.Score}")));
		}
		if (!moved && garrisons > 0)
		{
			moved = CGarrisonFill.Fill(state, country, agent, garrisons, targets, ordered);
			trace?.Invoke($"  garrisons moved {moved}");
		}
		if (!moved)
		{
			CReservePlacing.Place(state, country, agent);
			trace?.Invoke("  reserve placed");
		}
	}

	/// <summary>Defenders out of a battle of some hours in which the attacker holds more than 70%, to the best neighbours.</summary>
	private static void FallBack(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is not { } headquarters)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int hours = headquarters.Plan.Stance >= CUnitPlan.Attacking ? OldBattleAttacking : OldBattle;
		var area = new HashSet<int>(agent.Area);
		foreach (int id in agent.Area)
		{
			CProvince province = state.Province(id);
			if (network.DepotOf(id) == id || province.Modifier[CCombatModifiers.FortLevel].Raw >= FewForts * Whole)
			{
				continue;
			}
			foreach (CCombat combat in state.Combats.ToArray())
			{
				if (combat.Province != id || combat.Kind != ECombatKind.Land || combat.Hour < hours
					|| !combat.Defender.Units.Exists(unit => unit.Owner == country.Tag)
					|| CCombatShares.AttackerShare(combat) <= Losing)
				{
					continue;
				}
				List<int>? ways = Ways(state, country, network, province, area);
				if (ways is null || ways.Count == 0)
				{
					continue;
				}
				// The defenders are taken from the back of the side's list (FINDINGS-liveread.md, section 24).
				int next = 0;
				CUnit[] defenders = combat.Defender.Units.ToArray();
				System.Array.Reverse(defenders);
				foreach (CUnit unit in defenders)
				{
					if (unit.Owner != country.Tag || !CTheatres.IsLand(unit) || CCommandLadder.IsBlocked(unit))
					{
						continue;
					}
					if (CAiMoves.Move(state, country, unit, ways[next % ways.Count], safe: false))
					{
						agent.FullPassPending = true;
					}
					next++;
				}
			}
		}
	}

	/// <summary>The province's neighbours to fall back to, best first; null where a hostile one lies across a river.</summary>
	private static List<int>? Ways(CGameState state, CCountry country, CSupplyNetwork network, CProvince province,
		HashSet<int> area)
	{
		var scored = new List<(int Province, long Score)>();
		bool outside = false;
		bool comingIn = false;
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea)
			{
				continue;
			}
			// Both kinds of river, and an enemy across one ends the look (FINDINGS-liveread.md, section 24).
			bool river = edge.Kind is EProvinceEdgeKind.River or EProvinceEdgeKind.Crossing;
			if (river && beside.Controller is { } across && CCountryLists.IsEnemy(state, country, across))
			{
				return null;
			}
			// A friend's land unit standing beside and bound for the province keeps its defenders in.
			foreach (CUnit unit in state.Positions.Of(edge.To))
			{
				if (CTheatres.IsLand(unit) && CApproaches.MayOperateIn(state, country, unit.Owner)
					&& CAgentUnits.Destination(unit) == province.Id)
				{
					comingIn = true;
				}
			}
			if (!CApproaches.MayOperateIn(state, country, beside.Controller))
			{
				continue;
			}
			long score = CSupplyFigures.EffectiveInfrastructure(beside)
				+ 2 * beside.Modifier[CCombatModifiers.FortLevel].Raw;
			if (river)
			{
				score += 10 * Whole;
			}
			if (network.DepotOf(province.Id) == edge.To)
			{
				score += 100 * Whole;
			}
			if (area.Contains(edge.To))
			{
				score /= 100;
			}
			else
			{
				outside = true;
			}
			int at = 0;
			while (at < scored.Count && scored[at].Score >= score)
			{
				at++;
			}
			scored.Insert(at, (edge.To, score));
		}
		if (comingIn)
		{
			return null;
		}
		var ways = new List<int>();
		foreach ((int id, long _) in scored)
		{
			if (outside && area.Contains(id))
			{
				break;
			}
			ways.Add(id);
		}
		return ways;
	}


	/// <summary>Long marches made strategic redeployments; marches through enemy land cut; a fight on the way stood down.</summary>
	private static void CheckMarches(CGameState state, CCountry country, CAgent agent)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CUnit unit in agent.Land.ToArray())
		{
			if (unit.IsRemoved || unit.IsRetreating || !unit.IsMoving || unit.Location <= 0 || CAgentUnits.Destination(unit) <= 0)
			{
				continue;
			}
			int destination = unit.Path[^1];
			if (unit.AiBound > 0 && unit.AiBound != destination)
			{
				continue;
			}
			if (unit.Order != EUnitOrder.StrategicRedeployment && !unit.IsFighting
				&& CAiMoves.ShouldRedeploy(state, country, unit, destination))
			{
				state.Outbox.Add(new Commands.CSetUnitOrderCommand(country.Tag, unit.Id, EUnitOrder.StrategicRedeployment,
					destination));
				continue;
			}
			if (unit.Path.Count > 1 && unit.IsFighting && IsEnemyHeld(state, country, unit.Path[0]))
			{
				CAiMoves.StandDown(state, country, unit, "march check: battle ahead");
				continue;
			}
			for (int index = 0; index < unit.Path.Count - 1; index++)
			{
				int id = unit.Path[index];
				if (!IsEnemyHeld(state, country, id) || network.AreaOf(id) is not { } area || area.Provinces.Count <= SmallArea)
				{
					continue;
				}
				if (unit.Kind != EUnitKind.Division)
				{
					CAiMoves.StandDown(state, country, unit, "march check: headquarters");
				}
				else
				{
					CAiMoves.Move(state, country, unit, unit.Path[0], safe: false);
				}
				break;
			}
		}
	}

	private static bool IsEnemyHeld(CGameState state, CCountry country, int province)
	{
		return state.Province(province).Controller is { } holder
			&& (holder.Value == CRebelFactions.Rebels || CCountryLists.IsEnemy(state, country, holder));
	}
}
