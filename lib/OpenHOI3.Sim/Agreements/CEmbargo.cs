using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agreements;

/// <summary>An embargo, <see cref="First"/>'s on <see cref="Second"/>; it stands between them both ways.</summary>
public sealed record CEmbargo(CCountryTag First, CCountryTag Second)
{
	/// <summary>Its number among the pacts (<see cref="CGameState.NextPact"/>).</summary>
	public long Made { get; init; }

	/// <summary>The hour it began, which a save writes.</summary>
	public long Start { get; init; }

	public bool Joins(CCountryTag a, CCountryTag b) => (First == a && Second == b) || (First == b && Second == a);
}
