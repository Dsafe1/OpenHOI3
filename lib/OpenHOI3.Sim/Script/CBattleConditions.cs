namespace OpenHOI3.Sim.Script;

/// <summary>
/// The six <c>last_*_losses</c> conditions: what the winner or the loser of the last land, air or sea battle decided
/// anywhere lost, <b>strictly more</b> than the whole number written. The scope is not read.
/// See <c>docs/mechanics/triggers.md</c>, section 5.2.
/// </summary>
public static class CBattleConditions
{
	public static bool TryAnswer(CGameState state, CTrigger trigger, out bool answer)
	{
		if (state.LastBattleLosses.Of(trigger.Keyword) is not { } figure)
		{
			answer = false;
			return false;
		}
		answer = figure > CConditions.WholeOf(trigger.Argument.Amount);
		return true;
	}
}
