using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.StrategicWarfare;

/// <summary>What the played country and one enemy have cost each other, by sea, in the air and on land. See <c>docs/mechanics/strategicwarfare.md</c>, section 2.</summary>
public sealed class CWarfareRecord
{
	public const int Naval = 0;
	public const int Air = 1;
	public const int Land = 2;

	public CWarfareRecord(CCountryTag enemy, long startHour, int years)
	{
		Enemy = enemy;
		StartHour = startHour;
		Years = years;
		Categories = new[] { new CWarfareCategory(years), new CWarfareCategory(years), new CWarfareCategory(years) };
	}

	public CCountryTag Enemy { get; }

	public long StartHour { get; set; }

	public long EndHour { get; set; }

	public bool Ended { get; set; }

	public int Years { get; }

	public CWarfareCategory[] Categories { get; }
}
