using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Tests.Content;

/// <summary>The top level's counters, flags, victory conditions, fired events and fronts, as <c>docs/mechanics/savegame.md</c>, section 14, has them.</summary>
public class SaveTopTests
{
	private static CGameState World()
	{
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false) }, new[] { germany })
		{
			Events = new[]
			{
				new CEvent(isProvinceEvent: false, "events/test.txt", 1) { Id = 7001 },
				new CEvent(isProvinceEvent: false, "events/test.txt", 2) { Id = 7000 },
			},
		};
		state.VictoryConditions = new List<(string, List<CVictoryCondition>)>
		{
			("axis", new List<CVictoryCondition>
			{
				new("operation_sealion", CTrigger.All(), true), new("ax_extra", CTrigger.All(), false),
				new("ax_control_paris", CTrigger.All(), true),
			}),
		};
		return state;
	}

	private static ClausewitzNode Top(string text, string key) => ClausewitzParser.Parse(text).Get(key)!;

	[Fact]
	public void The_victory_conditions_a_save_names_are_chosen_and_those_with_a_date_met()
	{
		CGameState state = World();
		state.VictoryConditions[0].Conditions[1].Chosen = true;
		var report = new CSaveReport();

		Assert.True(CSaveTop.Read(state, Top(
			"victory_conditions=\r\n{\r\n\tfaction=\r\n\t{\r\noperation_sealion 0.1.0.0 ax_control_paris 1940.8.11.0 ax_none 0.1.0.0 axis \t}\r\n" +
			"\tgame_finished=yes\r\n}\r\n", "victory_conditions"), report));

		List<CVictoryCondition> axis = state.VictoryConditions[0].Conditions;
		Assert.True(axis[0].Chosen);
		Assert.False(axis[0].Met);
		Assert.Null(axis[0].Date);
		// One the save does not name is no longer chosen; a name the faction has not is reported.
		Assert.False(axis[1].Chosen);
		Assert.True(axis[2].Met);
		Assert.True(CSaveValues.TryHours("1940.8.11.0", out long taken));
		Assert.Equal(taken, axis[2].Date);
		Assert.True(state.GameFinished);
		Assert.Single(report.UnreadKeys);
	}

	[Fact]
	public void The_top_levels_blocks_are_written_in_the_originals_layout()
	{
		CGameState state = World();
		state.Flags.Set("spanish_civil_war");
		List<CVictoryCondition> axis = state.VictoryConditions[0].Conditions;
		axis[0].Chosen = true;
		axis[2].Chosen = true;
		Assert.True(CSaveValues.TryHours("1940.8.11.0", out long taken));
		axis[2].Date = taken;
		state.FiredOnce.Add(state.Events[0]);
		state.FiredOnce.Add(state.Events[1]);
		var front = new CWeatherFront(13779, 15, 0) { MoveCountdown = 1 };
		front.Provinces.Add(13626);
		state.Weather.Fronts.Add(front);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();

		CSaveTop.WriteFlags(writer, state);
		CSaveTop.WriteVictoryConditions(writer, state);
		CSaveTop.WriteFiredEvents(writer, state);
		CSaveTop.WriteFronts(writer, state);

		// The chosen alone, the null date as "0.1.0.0" and the faction's name last; the fired events by number.
		Assert.Equal(
			"flags=\r\n{\r\n\tspanish_civil_war=yes\r\n}\r\n" +
			"victory_conditions=\r\n{\r\n\tfaction=\r\n\t{\r\noperation_sealion 0.1.0.0 ax_control_paris 1940.8.11.0 axis \t}\r\n" +
			"\tgame_finished=no\r\n}\r\n" +
			"fired_events=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=7000\r\n\t\ttype=40\r\n\t}\r\n\tid=\r\n\t{\r\n\t\tid=7001\r\n\t\ttype=40\r\n\t}\r\n}\r\n" +
			"weather=\r\n{\r\n\tfront=\r\n\t{\r\n\t\tdirection=0.015\r\n\t\trotation=0.000\r\n\t\tspeed=1\r\n\t\thome=13779\r\n" +
			"\t\tprovinces=\r\n\t\t{\r\n13779 13626 \t\t}\r\n\t}\r\n}\r\n",
			writer.Text);
	}

	[Fact]
	public void A_saves_fronts_counters_and_fired_events_are_read()
	{
		CGameState state = World();
		var report = new CSaveReport();
		ClausewitzNode root = ClausewitzParser.Parse(
			"rebel=6\r\ntheatre=516\r\nfired_events=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=7000\r\n\t\ttype=40\r\n\t}\r\n}\r\n" +
			"weather=\r\n{\r\n\tfront=\r\n\t{\r\n\t\tdirection=0.546\r\n\t\trotation=0.000\r\n\t\tspeed=2\r\n\t\thome=11566\r\n" +
			"\t\tprovinces=\r\n\t\t{\r\n11465 11458 \t\t}\r\n\t}\r\n}\r\n");

		foreach (ClausewitzNode node in root.Children)
		{
			Assert.True(CSaveTop.Read(state, node, report));
		}

		Assert.Equal(5, state.LastRebelId);
		Assert.Equal(516, state.NextTheatreId);
		Assert.Equal(new[] { 7000 }, state.FiredOnce.Select(fired => fired.Id));
		CWeatherFront front = Assert.Single(state.Weather.Fronts);
		Assert.Equal((11566, 546, 0, 2), (front.Home, front.Direction, front.Rotation, front.MoveCountdown));
		// Only the provinces the save gives, its home not put in front of them.
		Assert.Equal(new[] { 11465, 11458 }, front.Provinces);
	}
}
