using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>The list of ships that have left the game, as <c>docs/mechanics/savegame.md</c>, section 7, keeps it.</summary>
public class SunkShipsTests
{
	[Fact]
	public void A_fleet_taken_out_of_the_game_puts_every_ship_still_in_it_on_the_list()
	{
		var tag = new CCountryTag("ENG");
		var england = new CCountry { Tag = tag, Name = "England", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "North Sea", isSea: true) }, new[] { england });
		var destroyer = new CSubUnitType("destroyer", EUnitBranch.Naval);
		var fleet = new CUnit(EUnitKind.Navy, tag) { Id = 1, Location = 1 };
		var first = new CSubUnit(destroyer) { Id = 2, Unit = fleet };
		var second = new CSubUnit(destroyer) { Id = 3, Unit = fleet };
		fleet.SubUnits.Add(first);
		fleet.SubUnits.Add(second);
		england.Units.Add(fleet);

		CCombatRetreat.RemoveFromGame(state, fleet);

		Assert.Equal(new[] { new CSunkShip(tag, first), new CSunkShip(tag, second) }, state.SunkShips);
		Assert.Same(second, CSunkShips.Find(state, 3));
		Assert.Null(CSunkShips.Find(state, 4));
	}
}
