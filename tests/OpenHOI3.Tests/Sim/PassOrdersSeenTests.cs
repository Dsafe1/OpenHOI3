using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a later country's pass reads of another's units, against <c>docs/mechanics/moving.md</c>, section 7.3: the orders the
/// list's first countries gave this hour, which the original's main thread gives before the worker threads' countries
/// count, and every other country's units where they were bound as the hour began.
/// </summary>
public class PassOrdersSeenTests
{
	private static CUnit Division(CCountry country)
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = country.Index + 1, Location = 1, AiBound = 7, AiBoundAtHour = -1 };
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void Germanys_orders_of_the_hour_are_read_as_given_and_the_later_countries_as_the_hour_began()
	{
		var countries = new[] { "REB", "GER", "ENG", "SOV", "USA", "SLO" }
			.Select(tag => new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "" }).ToArray();
		// The game state numbers the countries in the list's order.
		_ = new CGameState(new CDate(1941, 7, 16), new[] { new CProvince(0, "", isSea: false),
			new CProvince(1, "one", isSea: false) }, countries);
		var units = countries.Select(Division).ToArray();
		foreach (CCountry country in countries)
		{
			CUnitAiPass.ShowOrdersOfTheHour(country);
		}
		Assert.Equal(new[] { 7, 7, -1, -1, -1, -1 }, units.Select(unit => unit.AiBoundAtHour).ToArray());
	}
}
