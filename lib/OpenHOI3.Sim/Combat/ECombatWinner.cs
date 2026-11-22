namespace OpenHOI3.Sim.Combat;

/// <summary>Who a finished battle's report says won: nobody where both sides, or neither, still stood - every bombing.</summary>
public enum ECombatWinner
{
	Nobody,
	Attacker,
	Defender,
}
