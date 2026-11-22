namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A country's finished battles counted over a stretch of time (<see cref="CCombatReports.Tally"/>): how many, how
/// many won and lost - only where a winner is known - how many as attacker and as defender, what it lost and what
/// the other side lost, both in thousandths.
/// </summary>
public sealed class CCombatTally
{
	public int Combats { get; set; }

	public int Won { get; set; }

	public int Lost { get; set; }

	public int AsAttacker { get; set; }

	public int AsDefender { get; set; }

	/// <summary>What the country's own side lost.</summary>
	public long Losses { get; set; }

	/// <summary>What the other side lost.</summary>
	public long Kills { get; set; }

	/// <summary>Another tally's counts added to this one's.</summary>
	public void Add(CCombatTally other)
	{
		Combats += other.Combats;
		Won += other.Won;
		Lost += other.Lost;
		AsAttacker += other.AsAttacker;
		AsDefender += other.AsDefender;
		Losses += other.Losses;
		Kills += other.Kills;
	}
}
