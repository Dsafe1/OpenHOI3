using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What one country has been given to know of a unit: a level of intelligence, 0 to 9, for a number of the unit's days.
/// See <c>docs/mechanics/espionage.md</c>, section 7.
/// </summary>
public sealed class CIntelGrant
{
	public CIntelGrant(CCountryTag country, int days, int level)
	{
		Country = country;
		Days = days;
		Level = level;
	}

	public CCountryTag Country { get; }

	public int Days { get; set; }

	public int Level { get; set; }
}
