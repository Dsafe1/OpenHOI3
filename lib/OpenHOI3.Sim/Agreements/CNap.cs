using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agreements;

/// <summary>A non-aggression pact between two countries, from the hour it was made.</summary>
public sealed record CNap(CCountryTag First, CCountryTag Second, long Start)
{
	/// <summary>Its number among the pacts (<see cref="CGameState.NextPact"/>).</summary>
	public long Made { get; init; }

	/// <summary>The first party's brigades along the border to the second's, in thousandths (<see cref="CNapBalance"/>).</summary>
	public long Balance { get; set; } = CNapBalance.Unmeasured;

	/// <summary>The first party's brigades counted, the last time it was measured.</summary>
	public int OurPower { get; set; }

	/// <summary>The second party's brigades counted, an edge at a time.</summary>
	public int TheirPower { get; set; }

	public bool Joins(CCountryTag a, CCountryTag b) => (First == a && Second == b) || (First == b && Second == a);
}
