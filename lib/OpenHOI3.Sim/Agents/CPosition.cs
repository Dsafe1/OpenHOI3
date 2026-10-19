namespace OpenHOI3.Sim.Agents;

/// <summary>
/// One province of an agent's area of operations as the land block shares its line over them: its score, how many
/// divisions it wants, has bound for it, has standing in it, and holds at most. The original's record of a scored
/// province. See <c>docs/mechanics/moving.md</c>, sections 3 and 7.3.
/// </summary>
public sealed class CPosition
{
	public CPosition(int province, long score)
	{
		Province = province;
		Score = score;
	}

	public int Province { get; }

	/// <summary>Its share of the line, in thousandths.</summary>
	public long Score { get; }

	public int Want { get; set; }

	public int Have { get; set; }

	public int Standing { get; set; }

	public int Cap { get; set; }
}
