namespace OpenHOI3.Sim.Countries;

/// <summary>
/// Two countries allied to each other. It is the same alliance read from either end.
/// </summary>
public readonly record struct CAlliance(CCountryTag First, CCountryTag Second)
{
	public bool Joins(CCountryTag a, CCountryTag b)
	{
		return (First == a && Second == b) || (First == b && Second == a);
	}
}
