using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The list of finished battles and the countries still in a fight, against
/// <c>docs/mechanics/airmap.md</c>, section 2.
/// </summary>
public class CombatHistoryTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Eng = new("ENG");

	private static CUnit Unit(CCountryTag owner, int id) => new(EUnitKind.Air, owner) { Id = id, Location = 1 };

	[Fact]
	public void TakingAUnitOffMakesTheCountriesStillInTheFightAgainFromTheUnitsLeft()
	{
		var combat = new CCombat(1, ECombatKind.Air);
		CUnit german = Unit(Ger, 1);
		combat.Attacker.Add(german);
		combat.Attacker.Add(Unit(Ita, 2));

		combat.Attacker.Remove(german);

		Assert.Equal(new[] { Ita }, combat.Attacker.Standing);
		Assert.Equal(new[] { Ger, Ita }, combat.Attacker.Countries);
	}

	[Fact]
	public void AnEntryNamesEachSideByItsFirstCountryStillInTheFightAndABeatenSideByNobody()
	{
		var combat = new CCombat(7, ECombatKind.Air);
		combat.Attacker.Add(Unit(Ger, 1));
		combat.Attacker.Add(Unit(Ita, 2));
		CUnit polish = Unit(Pol, 3);
		combat.Defender.Add(polish);
		combat.Defender.Remove(polish);
		var history = new CCombatHistory();

		history.Record(combat, 1000);

		Assert.Equal(new CFinishedCombat(1000, ECombatKind.Air, 7, Ger, null), Assert.Single(history.Entries));
	}

	[Fact]
	public void AGroundBombingsHolderIsNamedUntilAUnitIsTakenOffItsSide()
	{
		var bombing = new CCombat(7, ECombatKind.GroundBombing);
		bombing.Defender.AddCountry(Pol);
		bombing.Attacker.Add(Unit(Ger, 1));
		var history = new CCombatHistory();
		history.Record(bombing, 10);
		Assert.Equal(Pol, history.Entries[0].Defender);

		CUnit english = Unit(Eng, 2);
		bombing.Defender.Add(english);
		bombing.Defender.Remove(english);
		history.Record(bombing, 11);

		Assert.Null(history.Entries[1].Defender);
	}

	[Fact]
	public void AnEntryIsKeptForAWeekOfHoursAfterItsBattleEnded()
	{
		var history = new CCombatHistory();
		history.Record(new CCombat(1), 100);
		history.Record(new CCombat(2), 200);

		history.Prune(100 + CCombatHistory.KeptHours);
		Assert.Equal(2, history.Entries.Count);

		history.Prune(101 + CCombatHistory.KeptHours);
		Assert.Equal(2, Assert.Single(history.Entries).Province);
	}

	[Fact]
	public void AnEntryFromAfterTheHourIsTakenOff()
	{
		var history = new CCombatHistory();
		history.Record(new CCombat(1), 500);

		history.Prune(400);

		Assert.Empty(history.Entries);
	}

	[Fact]
	public void ABattleThatEndsGoesOnTheListAtTheHour()
	{
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Kiel", isSea: false) },
			new[] { new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" } });
		var combat = new CCombat(1, ECombatKind.Air);
		combat.Attacker.Add(Unit(Ger, 1));
		state.Combats.Add(combat);

		CCombats.Finish(state, combat);

		CFinishedCombat entry = Assert.Single(state.CombatHistory.Entries);
		Assert.Equal(state.TotalHours, entry.Hour);
		Assert.Equal(Ger, entry.Attacker);
	}
}
