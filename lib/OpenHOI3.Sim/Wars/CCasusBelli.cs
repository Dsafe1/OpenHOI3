namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A casus belli held, on the holder's record towards the other: its type's number - nought, the null
/// type, for every one made in play - and the hour it ends, or none. See <c>docs/mechanics/war.md</c>,
/// section 2.
/// </summary>
public sealed record CCasusBelli(int Type, long? End = null)
{
	/// <summary>The hour it was given, which a save keeps; none where nothing says.</summary>
	public long? Start { get; init; }

	/// <summary>
	/// Its place among the pacts, in the order they were made: the original keeps a casus belli on the one list of
	/// relations a save's <c>diplomacy</c> is written from (<c>docs/mechanics/savegame.md</c>, section 10).
	/// </summary>
	public long Made { get; init; }
}
