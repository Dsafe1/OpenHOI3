using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The stances of a country's plans: air and naval offensive everywhere; the ground stance prepare at peace and, at war,
/// defensive, attacking or blitzing by the AI's power against the enemy's and how much of the front its units hold. The
/// original's <c>CEU3AI::UpdateUnitStances</c> and <c>CAIUnit::SetPlanStance</c>. See <c>docs/mechanics/agents.md</c>, section 7.2.
/// </summary>
public static class CPlanStances
{
	private const long Whole = 1000;

	/// <summary>The ratio taken where the enemy has no power: ten.</summary>
	private const long NoEnemy = 10 * Whole;

	private const long AttackingAt = 990;

	private const long BlitzingAt = 1980;

	/// <summary>
	/// Every hour while the country's stances are unsettled, and once a day on the forced hour. An agent with no unit ends
	/// the pass unsettled.
	/// </summary>
	public static void Update(CGameState state, CCountry country, bool force)
	{
		CUnitAi ai = country.UnitAi;
		if (!force && ai.StancesSettled)
		{
			return;
		}
		var allEnemies = new List<CCountryTag>();
		CAgent? least = null;
		long leastSum = 10_000_000 * Whole;
		foreach (CAgent agent in ai.Agents)
		{
			if (agent.Unit is not { } unit)
			{
				return;
			}
			long sum = 0;
			if (agent.FrontierAtWar && CAgents.TheatreOf(state, country, agent) is { } theatre)
			{
				var mine = new List<CCountryTag>();
				foreach (CFront front in theatre.Fronts)
				{
					if (!front.IsHot || front.Enemy is not { } enemy || mine.Contains(enemy))
					{
						continue;
					}
					mine.Add(enemy);
					if (!allEnemies.Contains(enemy))
					{
						allEnemies.Add(enemy);
					}
					if (CCountryLists.AtWar(state, country.Tag, enemy) || enemy.Value == Revolts.CRebelFactions.Rebels
						|| country.Tag.Value == Revolts.CRebelFactions.Rebels)
					{
						sum += unit.Plan.TheirPower;
					}
				}
			}
			agent.EnemyPower = sum;
			if (sum < leastSum)
			{
				leastSum = sum;
				least = agent;
			}
		}
		bool unsettled = false;
		foreach (CAgent agent in ai.Agents)
		{
			if (agent.Unit is not { } unit)
			{
				continue;
			}
			CUnitPlan plan = unit.Plan;
			if (plan.AirStance != CUnitPlan.Offensive || plan.NavalStance != CUnitPlan.Offensive)
			{
				state.Outbox.Add(new CSetPlanCommand(country.Tag, unit.Id, CSetPlanCommand.Unchanged, CUnitPlan.Offensive,
					CUnitPlan.Offensive));
			}
			if (plan.Area.Count == 0)
			{
				if (agent.FrontierAtWar)
				{
					unsettled = true;
				}
				continue;
			}
			int stance = CUnitPlan.Prepare;
			if (agent.FrontierAtWar)
			{
				long ratio = plan.TheirPower <= 0 ? NoEnemy : plan.OurPower * Whole / plan.TheirPower;
				if (allEnemies.Count > 1 && !ReferenceEquals(agent, least))
				{
					ratio /= 2;
				}
				long value = agent.Coverage * ratio / Whole;
				stance = value < AttackingAt ? CUnitPlan.Defensive : value < BlitzingAt ? CUnitPlan.Attacking : CUnitPlan.Blitzing;
			}
			if (stance != plan.Stance)
			{
				SetStance(state, country, agent, stance);
			}
		}
		if (!unsettled)
		{
			ai.StancesSettled = true;
		}
	}

	/// <summary>Slot 77: the ground stance posted, the plan marked posted, and the subordinates sorted again.</summary>
	public static void SetStance(CGameState state, CCountry country, CAgent agent, int stance)
	{
		if (agent.Unit is not { } unit)
		{
			return;
		}
		agent.PlanPosted = true;
		state.Outbox.Add(new CSetPlanCommand(country.Tag, unit.Id, stance, CSetPlanCommand.UnchangedAirOrNaval,
			CSetPlanCommand.UnchangedAirOrNaval));
		agent.ReplanSubordinates = true;
		CAreaScores.Score(state, country, agent, stance);
		CPlanAxis.Update(state, country, agent, stance);
		// A change of stance releases the reserve: every held-back division's mark cleared and the list emptied, so the
		// next sort decides them again (FINDINGS-liveread.md, section 45).
		foreach (Units.CUnit held in agent.HeldBack)
		{
			held.IsHeldBack = false;
		}
		agent.HeldBack.Clear();
	}
}
