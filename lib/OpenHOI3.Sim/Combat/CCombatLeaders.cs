using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The leaders a battle asks: a side's, which is the one of highest rank among its units'
/// leaders - what a land battle's tactic is picked by and what a side's positioning at sea
/// is made of - and a unit's own. See <c>docs/mechanics/landcombat.md</c>, section 11, and
/// <c>docs/mechanics/leaders.md</c>, section 10.
/// </summary>
public static class CCombatLeaders
{
	/// <summary>
	/// The leader of highest rank among the side's units, the first of equals; null where
	/// none of them has a leader in service.
	/// </summary>
	public static CLeader? HighestRanked(CCombatSide side)
	{
		CLeader? best = null;
		int rank = 0;
		foreach (CUnit unit in side.Units)
		{
			if (unit.Leader is { } leader && leader.Rank > rank)
			{
				rank = leader.Rank;
				best = leader;
			}
		}
		return best;
	}

	/// <summary>The skill of the side's highest ranked leader, or nothing.</summary>
	public static int Skill(CCombatSide side)
	{
		return HighestRanked(side)?.Skill ?? 0;
	}

	/// <summary>
	/// The skill of a unit's own leader, whatever his rank, or nothing where it has none -
	/// what drawing a fleet up, picking a target and a bomber's list of ships ask.
	/// </summary>
	public static int UnitSkill(CUnit unit)
	{
		return unit.Leader?.Skill ?? 0;
	}

	/// <summary>
	/// <c>BM_LEADER_BONUS</c> at sea and in the air: <c>COMBAT_LEADER_IMPACT</c> for each
	/// point of the unit's own leader's skill, where he has the rank for it and some skill.
	/// No trait comes into it, and the same on either side. Null where there is none to add.
	/// </summary>
	public static long? Bonus(CGameState state, CUnit unit)
	{
		if (!CLeaders.HasRankFor(state, unit) || unit.Leader is not { Skill: > 0 } leader)
		{
			return null;
		}
		return leader.Skill * CFixedPoint.Scale * state.Defines[CManpower.MilitarySection, CCombatModifiers.LeaderImpact].Raw
			/ CFixedPoint.Scale;
	}

	/// <summary>
	/// Whether the side's highest ranked leader has a trait himself - one his commanders'
	/// chain brings him does not count.
	/// </summary>
	public static bool HasTrait(CCombatSide side, string key)
	{
		if (HighestRanked(side) is not { } leader)
		{
			return false;
		}
		foreach (CTrait trait in leader.Traits)
		{
			if (string.Equals(trait.Key, key, System.StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
