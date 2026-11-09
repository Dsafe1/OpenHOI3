using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>The tooltip over a port's or an airfield's picture, against <c>tooltips.md</c>, section 7.2.</summary>
public class BaseTooltipTests
{
	private static string Localise(string key) => key == "UNITBASEPROVINCE_INFO" ? "units $AMOUNT$ daily $DAILY$" : key;

	private static CGameState World()
	{
		var countries = new[] { new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" } };
		return new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Kiel", isSea: false) }, countries);
	}

	[Fact]
	public void AnEmptyPortSaysSoAboveItsLine()
	{
		Assert.Equal("PORT\n\nunits 0 daily 0.00", CBaseTooltip.Build(Localise, World(), 1, EMapBuilding.Port, new CCountryTag("GER")));
	}

	[Fact]
	public void AnEmptyAirfieldHasOnlyItsLine()
	{
		Assert.Equal("units 0 daily 1.00", CBaseTooltip.Build(Localise, World(), 1, EMapBuilding.Airfield, new CCountryTag("GER")));
	}
}
