namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What the winner and the loser of the last land, air and sea battle decided anywhere lost - one figure of each for
/// the whole world, whoever fought, kept as the game's own state and set as a battle is decided, before its on_actions.
/// What the six <c>last_*_losses</c> conditions and the event text's <c>$LAND_LOSER_LOSSES$</c> and its five fellows
/// read. Not saved, the original's: a game made or loaded starts them at nought. See
/// <c>docs/mechanics/triggers.md</c>, section 5.2.
/// </summary>
public sealed class CLastBattleLosses
{
	public long LandWinner { get; set; }

	public long LandLoser { get; set; }

	public long AirWinner { get; set; }

	public long AirLoser { get; set; }

	public long NavalWinner { get; set; }

	public long NavalLoser { get; set; }

	/// <summary>
	/// A battle decided: each side's figure is its losses cut to whole numbers - men on land, strength at sea and in the
	/// air a ship gone a whole one - the loser the side that lost, the winner the other. A bombing, a raid on a convoy and
	/// a battle with a side that has no country left write nothing.
	/// </summary>
	public void Record(CCombat combat, bool attackerLost)
	{
		if (combat.IsConvoyBattle || combat.Attacker.Standing.Count == 0 || combat.Defender.Standing.Count == 0)
		{
			return;
		}
		long attacker = combat.Attacker.Losses.Raw / CFixedPoint.Scale;
		long defender = combat.Defender.Losses.Raw / CFixedPoint.Scale;
		long loser = attackerLost ? attacker : defender;
		long winner = attackerLost ? defender : attacker;
		switch (combat.Kind)
		{
			case ECombatKind.Land:
				LandLoser = loser;
				LandWinner = winner;
				break;
			case ECombatKind.Naval:
				NavalLoser = loser;
				NavalWinner = winner;
				break;
			case ECombatKind.Air:
				AirLoser = loser;
				AirWinner = winner;
				break;
		}
	}

	/// <summary>The figure a <c>last_*_losses</c> keyword reads, or null for any other keyword.</summary>
	public long? Of(string keyword)
	{
		return keyword switch
		{
			"last_battle_winner_losses" => LandWinner,
			"last_battle_loser_losses" => LandLoser,
			"last_air_battle_winner_losses" => AirWinner,
			"last_air_battle_loser_losses" => AirLoser,
			"last_naval_battle_winner_losses" => NavalWinner,
			"last_naval_battle_loser_losses" => NavalLoser,
			_ => null,
		};
	}
}
