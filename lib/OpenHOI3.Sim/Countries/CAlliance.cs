namespace OpenHOI3.Sim.Countries;

/// <summary>
/// Two countries allied to each other. It is the same alliance read from either end. Its number among the pacts, its
/// start and the end the history gave it are what a save writes; nothing ends it on that date here
/// (<c>savegame.md</c>, section 10).
/// </summary>
public readonly record struct CAlliance(CCountryTag First, CCountryTag Second)
{
	/// <summary>Its number among the pacts (<see cref="CGameState.NextPact"/>).</summary>
	public long Made { get; init; }

	/// <summary>The hour it began.</summary>
	public long Start { get; init; }

	/// <summary>The end the history or a save gave it; none for one made in play.</summary>
	public long? End { get; init; }

	public bool Joins(CCountryTag a, CCountryTag b)
	{
		return (First == a && Second == b) || (First == b && Second == a);
	}
}
