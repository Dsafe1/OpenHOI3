using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Settings;
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
		state.MarkFiredOnce(state.Events[0]);
		state.MarkFiredOnce(state.Events[1]);
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
	public void The_fired_events_are_written_by_the_originals_511_buckets_the_latest_of_a_bucket_first()
	{
		// The first four of BlackICE's save of 16 March 1936, fired here in another order; and two sharing a bucket.
		int[] ids = { 4220, 95584, 88911, 95071, 1000, 1511 };
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false) }, new[] { germany })
		{
			Events = ids.Select(id => new CEvent(isProvinceEvent: false, "events/test.txt", id) { Id = id }).ToArray(),
		};
		foreach (CEvent fired in state.Events)
		{
			state.MarkFiredOnce(fired);
		}
		var writer = new CSaveTextWriter();
		writer.OpenRoot();

		CSaveTop.WriteFiredEvents(writer, state);

		List<int> written = ClausewitzParser.Parse(writer.Text).Get("fired_events")!.All("id").Select(CSaveValues.Id).ToList();
		Assert.Equal(new[] { 1511, 1000 }, written.Where(id => id is 1000 or 1511));
		Assert.Equal(new[] { 88911, 95071, 95584, 4220 }, written.Where(id => id > 1511 || id == 4220));
	}

	[Fact]
	public void The_players_automation_and_the_difficulty_are_written_and_read_back()
	{
		CGameState state = World();
		var germany = new CCountryTag("GER");
		state.Played.Add(germany);
		state.AutomateTrade.Add(germany);
		state.Automation.Flags[(int)EAutomation.Diplomacy] = 1;
		state.Automation.Flags[(int)EAutomation.Deployment] = 1;
		state.Automation.ArmiesLevel = 3;
		state.Automation.Technology = 2;
		state.Automation.Sliders = 1;
		state.GamePlay.Difficulty = 4;
		var writer = new CSaveTextWriter();
		writer.OpenRoot();

		CSaveTop.WriteAutomation(writer, state, germany);
		CSaveTop.WriteGamePlay(writer, state);

		// The Armies level in the sixth flag's place.
		Assert.Contains("1 0 0 0 0 3 1 ", writer.Text);
		Assert.Contains("automate_trade=yes", writer.Text);
		CGameState read = World();
		var report = new CSaveReport();
		foreach (ClausewitzNode node in ClausewitzParser.Parse(writer.Text).Children)
		{
			if (node.Key != "automate_trade")
			{
				Assert.True(CSaveTop.Read(read, node, report));
			}
		}
		Assert.Equal(state.Automation.Flags, read.Automation.Flags);
		Assert.Equal((3, 2, 1), (read.Automation.ArmiesLevel, read.Automation.Technology, read.Automation.Sliders));
		Assert.Equal((4, 0), (read.GamePlay.Difficulty, read.GamePlay.GameMode));
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

	[Fact]
	public void A_rebel_faction_is_read_and_written_back_to_the_byte_its_army_on_the_rebels_counter()
	{
		// BlackICE's Spanish rising of 16 March 1936, as its save writes it.
		const string block = "rebel_faction=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=16\r\n\t\ttype=39\r\n\t}\r\n" +
			"\ttype=\"disgruntled_rabble\"\r\n\tname=\"Striking workers and political agitators\"\r\n\tcountry=\"GER\"\r\n" +
			"\ttarget=\"GER\"\r\n\tindependence=\"---\"\r\n\tgovernment=\"social_democracy\"\r\n\tprovince=4294\r\n" +
			"\tarmy=\r\n\t{\r\n\t\tid=17\r\n\t\ttype=39\r\n\t}\r\n\tprovinces=\r\n\t{\r\n\t\t4294 5066 \t}\r\n}\r\n";
		CGameState state = World();
		state.RebelTypes = new[] { new OpenHOI3.Sim.Revolts.CRebelType("disgruntled_rabble", 0) };
		state.Governments["social_democracy"] = new OpenHOI3.Sim.Politics.CGovernment("social_democracy");
		var ids = new CSaveIds();
		ids.SetHistoryNext(100);

		CSaveRebelFactions.Read(state, Top(block, "rebel_faction"), ids, new CSaveReport());
		// What the loader does once every unit is read: the army keeps its number on the rebels' counter.
		foreach (((int serial, int type), int id) in ids.Others)
		{
			state.RebelSerials[id] = serial;
		}

		OpenHOI3.Sim.Revolts.CRebelFaction faction = Assert.Single(state.RebelFactions);
		Assert.Equal((16, 4294, "Striking workers and political agitators"), (faction.Id, faction.Province, faction.Name));
		Assert.Equal(new[] { new CCountryTag("GER") }, faction.Targets);
		Assert.Null(faction.Independence);
		Assert.Equal(new[] { 4294, 5066 }, faction.Provinces);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveRebelFactions.Write(writer, state);
		Assert.Equal(block, writer.Text);
	}

	[Fact]
	public void The_strategic_warfare_managers_null_tag_is_no_player()
	{
		CGameState state = World();
		CSaveWarfare.Read(state, Top("strategic_warfare=\r\n{\r\n\tplayer=\"---\"\r\n\tcount=0\r\n}\r\n", "strategic_warfare"));
		Assert.Null(state.Warfare.Player);

		CSaveWarfare.Read(state, Top("strategic_warfare=\r\n{\r\n\tplayer=\"GER\"\r\n\tcount=0\r\n}\r\n", "strategic_warfare"));
		Assert.Equal(new CCountryTag("GER"), state.Warfare.Player);
	}
}
