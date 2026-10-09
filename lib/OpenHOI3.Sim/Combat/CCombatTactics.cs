using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// How the two sides of a land battle come by their tactics, and what the tactics make
/// of the battle. They are picked when it starts and again every so many hours, by
/// weight; a tactic the other side's counters does nothing. See
/// <c>docs/mechanics/landcombat.md</c>, section 11.
/// </summary>
public static class CCombatTactics
{
	public const string SwapFrequency = "TACTIC_SWAP_FREQUENCEY";
	public const string CounterAdvantage = "INITIATIVE_PICK_COUNTER_ADVANTAGE_FACTOR";
	public const string AggressivenessImpact = "AGGRESSIVNESS_SELECTION_IMPACT";

	/// <summary>
	/// The skill of a side's leader. Leaders are not built, so every side's is 0: the
	/// attacker always picks first and nobody has the initiative.
	/// </summary>
	public static int LeaderSkill(CCombatSide side)
	{
		return 0;
	}

	/// <summary>Whether one tactic counters another: it is the one the other names as its bane.</summary>
	public static bool Counters(CCombatTactic? tactic, CCombatTactic? other)
	{
		return tactic is not null && other is not null && tactic.Number == other.CounteredByNumber;
	}

	/// <summary>
	/// Whether this hour is one tactics are picked in: a side has none, or the battle
	/// has been fought for a whole number of swap periods. With no tactics to pick from
	/// nothing is, and no roll is made.
	/// </summary>
	public static bool IsDue(CGameState state, CCombat combat)
	{
		if (state.Tactics.Count == 0)
		{
			return false;
		}
		if (combat.Attacker.Tactic is null || combat.Defender.Tactic is null)
		{
			return true;
		}
		long period = state.Defines[CManpower.MilitarySection, SwapFrequency].Raw / CFixedPoint.Scale;
		return period > 0 && combat.Duration % period == 0;
	}

	/// <summary>
	/// Picks both sides' tactics. The side whose leader has less skill picks first; the
	/// other then picks favouring the tactic that counters the first's, the more the
	/// greater its lead. With equal skill the attacker picks first and the defender
	/// favours nothing. Nothing is picked unless both sides have a country.
	/// </summary>
	public static void Pick(CGameState state, CCombat combat)
	{
		if (combat.Attacker.Countries.Count > 0 && combat.Defender.Countries.Count > 0)
		{
			int attack = LeaderSkill(combat.Attacker);
			int defence = LeaderSkill(combat.Defender);
			if (defence < attack)
			{
				PickFor(state, combat, combat.Defender, 0, 0, 0);
				PickFor(state, combat, combat.Attacker, 0, combat.Defender.Tactic?.CounteredByNumber ?? 0, attack - defence);
			}
			else if (attack < defence)
			{
				PickFor(state, combat, combat.Attacker, 0, 0, 0);
				PickFor(state, combat, combat.Defender, 0, combat.Attacker.Tactic?.CounteredByNumber ?? 0, defence - attack);
			}
			else
			{
				PickFor(state, combat, combat.Attacker, 0, 0, 0);
				PickFor(state, combat, combat.Defender, 0, 0, 0);
			}
		}
		SumEffects(combat);
	}

	/// <summary>
	/// Picks one side's tactic. Every tactic whose conditions hold of the side weighs
	/// its base and what the side's first country's technologies add to it; the one
	/// favoured weighs more by the skill it is favoured with; and each weighs less the
	/// further its aggressiveness is from the leader's. Two rolls are made, as the
	/// original makes them: one that the conditions are judged with, and the pick.
	/// </summary>
	public static void PickFor(CGameState state, CCombat combat, CCombatSide side, long aggressiveness,
		int favoured, int skillDifference)
	{
		state.Random.Next();
		var weights = new long[state.Tactics.Count];
		for (int i = 0; i < state.Tactics.Count; i++)
		{
			weights[i] = Weight(state, combat, side, state.Tactics[i], aggressiveness, favoured, skillDifference);
		}

		int roll = state.Random.Next();
		long total = 0;
		foreach (long weight in weights)
		{
			if (weight > 0)
			{
				total += weight;
			}
		}
		side.Tactic = null;
		if (total == 0)
		{
			return;
		}
		long running = 0;
		for (int i = 0; i < weights.Length; i++)
		{
			if (weights[i] <= 0)
			{
				continue;
			}
			running += weights[i];
			if (roll % total < running)
			{
				side.Tactic = state.Tactics[i];
				return;
			}
		}
	}

	/// <summary>
	/// What one tactic weighs in a side's pick, in thousandths: nothing where its
	/// conditions do not hold of the side; else its base and what the technologies of
	/// the side's first country add to it, more by the lead where it is the one
	/// favoured, and less by how far its aggressiveness is from the leader's.
	/// </summary>
	public static long Weight(CGameState state, CCombat combat, CCombatSide side, CCombatTactic tactic,
		long aggressiveness, int favoured, int skillDifference)
	{
		if (tactic.Condition is { } condition && !condition.Holds(state, combat, side))
		{
			return 0;
		}
		CCountry? country = side.Countries.Count > 0 ? state.Country(side.Countries[0]) : null;
		long weight = (country?.Technology[tactic.Name].Raw ?? 0) + tactic.Base.Raw;
		if (tactic.Number == favoured)
		{
			long lead = state.Defines[CManpower.MilitarySection, CounterAdvantage].Raw
				* (skillDifference * (long)CFixedPoint.Scale) / CFixedPoint.Scale;
			weight = (lead + CFixedPoint.Scale) * weight / CFixedPoint.Scale;
		}
		long distance = Math.Clamp(Math.Abs(aggressiveness - tactic.Aggressiveness.Raw), 0, CFixedPoint.Scale);
		long impact = state.Defines[CManpower.MilitarySection, AggressivenessImpact].Raw;
		return (CFixedPoint.Scale - distance * impact / CFixedPoint.Scale) * weight / CFixedPoint.Scale;
	}

	/// <summary>
	/// What the two tactics make of the battle: its width, the damage of each side's
	/// hits and the attackers' pace. A tactic the other side's counters adds nothing,
	/// and nothing is added once a side has no country left.
	/// </summary>
	public static void SumEffects(CCombat combat)
	{
		combat.TacticWidth = CFixedPoint.Zero;
		combat.TacticAttacker = CFixedPoint.Zero;
		combat.TacticDefender = CFixedPoint.Zero;
		combat.TacticSpeed = CFixedPoint.Zero;
		if (combat.Attacker.Countries.Count == 0 || combat.Defender.Countries.Count == 0)
		{
			return;
		}
		foreach (CCombatSide side in new[] { combat.Attacker, combat.Defender })
		{
			if (side.Tactic is not { } tactic || Counters(combat.Other(side).Tactic, tactic))
			{
				continue;
			}
			combat.TacticWidth += tactic.Width;
			combat.TacticAttacker += tactic.Attacker;
			combat.TacticDefender += tactic.Defender;
			combat.TacticSpeed += tactic.Speed;
		}
	}

	/// <summary>
	/// Numbers the tactics that counter others, once all are known: each tactic's
	/// <see cref="CCombatTactic.CounteredBy"/> is looked up by name. A name that is no
	/// tactic is reported and counters nothing.
	/// </summary>
	public static void Link(IReadOnlyList<CCombatTactic> tactics, Action<string>? warn = null)
	{
		foreach (CCombatTactic tactic in tactics)
		{
			tactic.CounteredByNumber = 0;
			if (tactic.CounteredBy.Length == 0)
			{
				continue;
			}
			foreach (CCombatTactic other in tactics)
			{
				if (other.Name.Equals(tactic.CounteredBy, StringComparison.OrdinalIgnoreCase))
				{
					tactic.CounteredByNumber = other.Number;
					break;
				}
			}
			if (tactic.CounteredByNumber == 0)
			{
				warn?.Invoke($"tactic '{tactic.Name}' is countered by '{tactic.CounteredBy}', which is no tactic");
			}
		}
	}
}
