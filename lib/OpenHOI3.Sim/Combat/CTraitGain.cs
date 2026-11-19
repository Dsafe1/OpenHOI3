using System.Collections.Generic;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// How a leader earns a trait in battle: every hour each side's units' leaders bank an hour,
/// and the hours banked are counted towards the traits that battle may earn - when a unit
/// leaves the side, and when the battle ends - for each whose condition holds then. See
/// <c>docs/mechanics/leaders.md</c>, section 11.
/// </summary>
public static class CTraitGain
{
	/// <summary>
	/// The head of a side's hour: the leader of each of its units banks the hour, once
	/// however many battles ask - which the hour of the day decides.
	/// </summary>
	public static void BankHour(CGameState state, CCombatSide side)
	{
		int hour = state.Hour;
		foreach (CUnit unit in side.Units)
		{
			if (unit.Leader is { TraitProgress: { } progress } && progress.CurrentHour != hour)
			{
				progress.Duration++;
				progress.CurrentHour = hour;
			}
		}
	}

	/// <summary>
	/// Every leader of the side has his banked hours counted against the battle: what a
	/// unit leaving the side does first, and the end of the battle to each side.
	/// </summary>
	public static void Count(CGameState state, CCombat combat, CCombatSide side)
	{
		foreach (CUnit unit in new List<CUnit>(side.Units))
		{
			if (unit.Leader is { } leader)
			{
				Count(state, combat, side, leader);
			}
		}
	}

	/// <summary>
	/// One leader's banked hours counted: towards each trait the battle may earn him that he
	/// has not and has not finished, where its condition holds for his side and him, a share
	/// of the whole for the hours against those it wants. A trait made whole is his. The
	/// hours are spent either way.
	/// </summary>
	public static void Count(CGameState state, CCombat combat, CCombatSide side, CLeader leader)
	{
		CTraitProgress progress = leader.TraitProgress;
		if (!state.GainableTraits.HasAny(leader.Type))
		{
			progress.Duration = 0;
			return;
		}
		if (progress.Duration == 0)
		{
			return;
		}
		foreach (CGainableTrait gainable in state.GainableTraits.For(combat.Kind, leader.Type))
		{
			if (leader.Traits.Contains(gainable.Trait))
			{
				continue;
			}
			int sofar = progress.Of(gainable);
			if (sofar >= CTraitProgress.Whole)
			{
				continue;
			}
			if (gainable.Condition is { } condition && !condition.Holds(state, combat, side, leader))
			{
				continue;
			}
			long step = CTraitProgress.Whole;
			if (gainable.HoursRequired > 0)
			{
				long share = progress.Duration * 1000L * 1000 / (gainable.HoursRequired * 1000L);
				step = CTraitProgress.Whole * share / 1000;
			}
			long now = sofar + step;
			if (now >= CTraitProgress.Whole)
			{
				progress.Progress[gainable] = CTraitProgress.Whole;
				leader.Traits.Add(gainable.Trait);
			}
			else
			{
				progress.Progress[gainable] = (int)now;
			}
		}
		progress.Duration = 0;
	}

	/// <summary>
	/// Whether a side has lost the battle, as <c>is_winner</c> asks once it is over. Ours:
	/// the original asks the side again then; this takes what the battle was decided as, so
	/// a battle broken off is lost by nobody.
	/// </summary>
	public static bool HasLost(CCombat combat, CCombatSide side)
	{
		return combat.IsConcluded && combat.AttackerLost == side.IsAttacker;
	}
}
