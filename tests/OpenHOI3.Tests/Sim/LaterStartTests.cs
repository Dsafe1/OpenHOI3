using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a new game takes from the history's wars and does as its entry ends, at the start date and at a later one -
/// <c>docs/mechanics/war.md</c>, section 4, <c>strategicwarfare.md</c> and <c>rebels.md</c>, section 8. Three countries in
/// a row, Germany, Poland and Lithuania, each owning and holding its one province.
/// </summary>
public class LaterStartTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Lit = new("LIT");

	private static CGameState World(CDate date)
	{
		CCountry Country(CCountryTag tag, int capital) =>
			new() { Tag = tag, Name = tag.Value, Adjective = tag.Value + "ish", DefinitionFile = "", Capital = capital };
		CCountryTag[] owners = { Ger, Pol, Lit };
		var provinces = new List<CProvince> { new(0, "", isSea: false) };
		for (int id = 1; id <= 3; id++)
		{
			provinces.Add(new CProvince(id, $"p{id}", isSea: false) { Owner = owners[id - 1], Controller = owners[id - 1] });
		}
		var state = new CGameState(date, provinces, new[] { Country(Ger, 1), Country(Pol, 2), Country(Lit, 3) },
			staticModifiers: new[] { new CModifier(CWarSides.Mobilisation) })
		{
			StartDate = new CDate(1936, 1, 1),
		};
		state.Defines.Set("country", "MOBILIZATION_BONUS_DAYS", CFixedPoint.FromInt(30));
		return state;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static CWar War(CGameState state, CCountryTag[] attackers, CCountryTag[] defenders)
	{
		var war = new CWar("The War");
		war.Attackers.AddRange(attackers);
		war.Defenders.AddRange(defenders);
		state.Wars.Add(war);
		return war;
	}

	[Fact]
	public void A_history_war_at_a_later_start_has_its_countries_mobilised_and_their_mobilisation_bonus_gone()
	{
		CGameState state = World(new CDate(1939, 9, 1));
		War(state, new[] { Ger }, new[] { Pol });

		// Germany was added first and Poland against it, as the history's file has them.
		CWarSides.SetFromHistory(state, new[] { (Pol, Ger) });

		Assert.True(C(state, Ger).Mobilised);
		Assert.True(C(state, Pol).Mobilised);
		Assert.False(C(state, Lit).Mobilised);
		// Given at the start date as the history was replayed, for thirty days: long gone.
		Assert.Empty(C(state, Ger).EventModifiers);
		// The joiner's record towards the other side has last_war at the start date; the other's has none.
		Assert.Equal(new CDate(1936, 1, 1).DayNumber * 24L, CGameState.Status(C(state, Pol), C(state, Ger)).LastWar);
		Assert.Null(CGameState.Status(C(state, Ger), C(state, Pol)).LastWar);
		// Both sides had their modifiers made then.
		Assert.Contains(Ger, state.MobilisedByHistory);
		Assert.Contains(Pol, state.MobilisedByHistory);
	}

	[Fact]
	public void A_history_war_at_the_start_date_keeps_its_thirty_days()
	{
		CGameState state = World(new CDate(1936, 1, 1));
		War(state, new[] { Ger }, new[] { Pol });

		CWarSides.SetFromHistory(state, new[] { (Pol, Ger) });

		CTimedModifier given = Assert.Single(C(state, Ger).EventModifiers);
		Assert.Equal(state.TotalHours + 30 * 24L, given.UntilHour);
	}

	[Fact]
	public void A_country_owning_nothing_and_not_in_exile_is_taken_off_its_wars_and_out_of_its_faction()
	{
		CGameState state = World(new CDate(1941, 6, 22));
		state.Province(3).Owner = Ger;
		state.Province(2).Owner = Ger;
		C(state, Lit).Faction = "allies";
		C(state, Pol).InExile = true;
		CWar war = War(state, new[] { Ger }, new[] { Pol, Lit });

		CWarSides.RemoveLandless(state);

		Assert.Equal(new[] { Pol }, war.Defenders);
		Assert.Equal("", C(state, Lit).Faction);
		Assert.Contains(war, state.Wars);
	}

	[Fact]
	public void A_side_left_empty_leaves_its_war_standing()
	{
		CGameState state = World(new CDate(1941, 6, 22));
		state.Province(3).Owner = Ger;
		CWar war = War(state, new[] { Ger }, new[] { Lit });

		CWarSides.RemoveLandless(state);

		Assert.Empty(war.Defenders);
		Assert.Equal(new[] { Ger }, war.Attackers);
		Assert.Contains(war, state.Wars);
	}

	[Fact]
	public void The_played_country_at_war_starts_a_record_of_strategic_warfare_for_each_enemy()
	{
		CGameState state = World(new CDate(1939, 9, 1));
		state.Played.Add(Ger);
		War(state, new[] { Ger }, new[] { Pol, Lit });

		state.Warfare.SetUpForNewGame(state);

		Assert.Equal(Ger, state.Warfare.Player);
		Assert.Equal(new[] { Pol, Lit }, state.Warfare.Records.Select(record => record.Enemy));
	}

	[Fact]
	public void The_played_country_at_peace_starts_none()
	{
		CGameState state = World(new CDate(1939, 9, 1));
		state.Played.Add(Lit);
		War(state, new[] { Ger }, new[] { Pol });

		state.Warfare.SetUpForNewGame(state);

		Assert.Equal(Lit, state.Warfare.Player);
		Assert.Empty(state.Warfare.Records);
	}

	[Fact]
	public void Land_the_rebels_hold_as_a_game_begins_was_taken_by_them_that_day()
	{
		CGameState state = World(new CDate(1941, 6, 22));
		state.Province(2).Controller = new CCountryTag("REB");

		CRisings.AtNewGame(state);

		Assert.Equal(state.Date, state.Province(2).RebelsTook);
		Assert.Null(state.Province(1).RebelsTook);
	}
}
