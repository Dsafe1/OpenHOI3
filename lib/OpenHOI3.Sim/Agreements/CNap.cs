using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agreements;

/// <summary>A non-aggression pact between two countries, from the hour it was made.</summary>
public sealed record CNap(CCountryTag First, CCountryTag Second, long Start)
{
	public bool Joins(CCountryTag a, CCountryTag b) => (First == a && Second == b) || (First == b && Second == a);
}
