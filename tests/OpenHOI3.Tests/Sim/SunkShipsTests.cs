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

	private static bool Holds(CGameState state, string text, CCountryTag country)
	{
		return OpenHOI3.Sim.Script.CConditions.Holds(state, ScriptWorld.Condition(text),
			OpenHOI3.Sim.Script.CEventScope.Start(country));
	}

	[Fact]
	public void The_two_conditions_count_the_list_by_its_owners_and_by_whose_the_sinker_is_now()
	{
		CGameState state = ScriptWorld.State();
		var destroyer = new CSubUnitType("destroyer", EUnitBranch.Naval);
		var fleet = new CUnit(EUnitKind.Navy, ScriptWorld.Ger) { Id = 1, Location = 5 };
		var hunter = new CSubUnit(destroyer) { Id = 2, Unit = fleet };
		fleet.SubUnits.Add(hunter);
		state.Country(ScriptWorld.Ger)!.Units.Add(fleet);
		// A German ship disbanded, sunk by nobody; a German ship sunk by a Polish one that is itself on the list; and three
		// Polish ships - one sunk by the German ship still afloat, one by that German ship on the list, one by a wing gone.
		var disbanded = new CSubUnit(destroyer) { Id = 10 };
		var sunkGerman = new CSubUnit(destroyer) { Id = 11, SunkById = 20 };
		var polishSinker = new CSubUnit(destroyer) { Id = 20, SunkById = 2 };
		var byTheList = new CSubUnit(destroyer) { Id = 21, SunkById = 11 };
		var byAGoneWing = new CSubUnit(destroyer) { Id = 22, SunkById = 99 };
		CSunkShips.Add(state, ScriptWorld.Ger, disbanded);
		CSunkShips.Add(state, ScriptWorld.Ger, sunkGerman);
		CSunkShips.Add(state, ScriptWorld.Pol, polishSinker);
		CSunkShips.Add(state, ScriptWorld.Pol, byTheList);
		CSunkShips.Add(state, ScriptWorld.Pol, byAGoneWing);

		Assert.Equal(2, CSunkShips.OursSunk(state, ScriptWorld.Ger));
		Assert.Equal(3, CSunkShips.OursSunk(state, ScriptWorld.Pol));
		Assert.Equal(2, CSunkShips.SunkBy(state, ScriptWorld.Ger));
		Assert.Equal(1, CSunkShips.SunkBy(state, ScriptWorld.Pol));
		Assert.True(Holds(state, "total_of_ours_sunk = 2", ScriptWorld.Ger));
		Assert.False(Holds(state, "total_of_ours_sunk = 3", ScriptWorld.Ger));
		Assert.True(Holds(state, "total_sunk_by_us = 2.9", ScriptWorld.Ger));      // thousandths cut to whole
		Assert.False(Holds(state, "total_sunk_by_us = 3", ScriptWorld.Ger));

		// The sinker's unit given to Poland: its ship's count goes with it.
		fleet.Owner = ScriptWorld.Pol;
		Assert.Equal(1, CSunkShips.SunkBy(state, ScriptWorld.Ger));
		Assert.Equal(2, CSunkShips.SunkBy(state, ScriptWorld.Pol));
	}
}
