using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Script;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Messages, against <c>docs/mechanics/messages.md</c>, on <see cref="ScriptWorld"/>'s strip of
/// ground, where each country's name is its tag and Germany's head of state is Head.
/// </summary>
public class MessageTests
{
	private static readonly Dictionary<string, string> Table = new()
	{
		["EVENTHAPPENUS_HEADER"] = "$head_of_state$ reports on",
		["EVENTHAPPENUS_1"] = "§W$EVENT$§W!",
		["EVENTHAPPENUS_4"] = "*",
		["EVENTHAPPENUS_LOG"] = "Event: '$EVENT$' for player.",
		["MINISTER_MESSAGE_ENTRY"] = "$TITLE$ $NAME$",
		["head_of_state"] = "Head of State",
		["EVT_1_TITLE"] = "The Title",
		["January"] = "January",
		["June"] = "June",
		["LONG_LOG"] = "one two three four five six",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	private static CMessageTypes Types()
	{
		return CMessageTypes.Read(ClausewitzParser.Parse(
			"EVENTHAPPENUS = { log = yes popup = no categories = \"cat_events\" }\n"
			+ "MAJOREVENTHAPPENOTHER = { log = yes popup = yes categories = \"cat_events\" }\n"
			+ "EVENTHAPPENOTHEROPTION = { log = no categories = \"cat_event\" }\n"
			+ "EVENTHAPPENOTHEROPTION = { log = no popup = yes }\n"), _ => { });
	}

	private static CEvent Event(int options, bool major)
	{
		var made = new CEvent(isProvinceEvent: false, "events/test.txt", 1) { Id = 1, Title = "EVT_1_TITLE", Major = major };
		for (int i = 0; i < options; i++)
		{
			made.Options.Add(new CEventOption { Name = "OPT" });
		}
		return made;
	}

	[Fact]
	public void A_type_takes_its_keys_a_misspelt_category_is_other_and_a_second_declaration_is_the_type()
	{
		CMessageTypes types = Types();

		Assert.Equal(EMessageCategory.Events, types.Find("EVENTHAPPENUS").Category);
		Assert.True(types.Find("MAJOREVENTHAPPENOTHER").PopsUp);
		CMessageType twice = types.Find("EVENTHAPPENOTHEROPTION");
		Assert.True(twice.Popup);
		Assert.Equal(EMessageCategory.Other, twice.Category);
	}

	[Fact]
	public void A_type_nobody_declared_logs_and_nothing_else()
	{
		CMessageType missing = Types().Find("PROVDECISIONUS");

		Assert.Same(CMessageType.Null, missing);
		Assert.True(missing.Log);
		Assert.False(missing.PopsUp);
	}

	[Fact]
	public void The_players_own_event_is_us_and_anothers_major_one_of_one_option_is_major_other()
	{
		CGameState state = ScriptWorld.State();
		CEventScope germany = CEventScope.Start(ScriptWorld.Ger);
		CEventScope poland = CEventScope.Start(ScriptWorld.Pol);

		Assert.Equal("EVENTHAPPENUS", CMessages.EventAnswered(Localise, Types(), state, ScriptWorld.Ger, Event(2, false), germany, 0).Type.Name);
		Assert.Equal("MAJOREVENTHAPPENOTHER", CMessages.EventAnswered(Localise, Types(), state, ScriptWorld.Ger, Event(1, true), poland, 0).Type.Name);
		Assert.Equal("EVENTHAPPENOTHEROPTION", CMessages.EventAnswered(Localise, Types(), state, ScriptWorld.Ger, Event(2, false), poland, 1).Type.Name);
	}

	[Fact]
	public void The_header_takes_the_players_government_and_a_line_its_words_with_white_made_dark()
	{
		CGameState state = ScriptWorld.State();
		CMessage message = CMessages.EventAnswered(Localise, Types(), state, ScriptWorld.Ger, Event(1, false), CEventScope.Start(ScriptWorld.Ger), 0);

		Assert.Equal("Head of State Head reports on", CMessageText.Header(Localise, message));
		Assert.Equal("§bThe Title§b!", CMessageText.Line(Localise, message, 1));
		Assert.Equal("", CMessageText.Line(Localise, message, 4));
		Assert.Equal(" 0:00, 1 June, 1936 Event: 'The Title' for player.", CMessageText.Log(Localise, message));
	}

	[Fact]
	public void A_units_arrival_is_its_kinds_type_about_where_it_stands_with_its_name_and_its_kinds_word()
	{
		CGameState state = ScriptWorld.State();
		var division = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Division, ScriptWorld.Ger) { Id = 7, Location = 1, Name = "1. Division" };
		var fleet = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Navy, ScriptWorld.Ger) { Id = 8, Location = 1 };
		var air = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Air, ScriptWorld.Ger) { Id = 9, Location = 1 };
		CMessageTypes types = CMessageTypes.Read(ClausewitzParser.Parse(
			"UNITARRIVED_LAND = { log = yes }\nUNITARRIVED_NAVAL = { log = yes }\nUNITARRIVED_AIR = { log = yes }\n"), _ => { });

		CMessage message = CMessages.UnitArrived(Localise, types, state, ScriptWorld.Ger, division)!;
		Assert.Equal("UNITARRIVED_LAND", message.Type.Name);
		Assert.Equal(1, message.Province);
		Assert.Equal(7, message.Unit);
		Assert.Equal(ScriptWorld.Ger, message.FirstCountry);
		Assert.Equal(ScriptWorld.Ger, message.SecondCountry);
		Assert.Equal("COUNTRY", message.Variables[0].Key);
		Assert.Contains(("MESSENGER", "MESSAGE_HEAD_MARS"), message.Variables);
		Assert.Contains(("NAME", "1. Division"), message.Variables);
		Assert.Contains(("PROV", "p1"), message.Variables);
		Assert.Equal(("TYPE", "ARMY"), message.Variables[^1]);

		Assert.Contains(("TYPE", "NAVY"), CMessages.UnitArrived(Localise, types, state, ScriptWorld.Ger, fleet)!.Variables);
		CMessage flown = CMessages.UnitArrived(Localise, types, state, ScriptWorld.Ger, air)!;
		Assert.Equal("UNITARRIVED_AIR", flown.Type.Name);
		Assert.Contains(("TYPE", "EXP_SQUADRON"), flown.Variables);
		Assert.Null(CMessages.UnitArrived(Localise, types, state, ScriptWorld.Pol, division));   // not the player's unit
		Assert.Equal(EMessageGoto.Unit, CMessageGotos.Of(message.Type.Name));
	}

	[Fact]
	public void The_log_keeps_200_messages_and_cuts_one_row_for_each_added()
	{
		var log = new CMessageLog(Localise, 1000f, text => text.Length);
		CMessageType type = Types().Find("EVENTHAPPENUS");
		for (int i = 0; i < 205; i++)
		{
			log.Add(new CMessage(type, " date"));
		}

		Assert.Equal(200, log.Messages.Count);
		Assert.Equal(CMessageLog.MostRows, log.Rows.Count);
	}

	[Fact]
	public void A_log_line_is_laid_out_as_the_text_layout_lays_it_out_a_row_for_each_piece()
	{
		// Each row is cut at the last space before the character that reaches 12 less the O's 1, the space dropped.
		var log = new CMessageLog(Localise, 12f, text => text.Length);
		log.Add(new CMessage(new CMessageType("LONG"), " d"));

		Assert.Equal(new[] { " d one two", "three four", "five six" }, log.Rows.Select(row => row.Text));
	}

	[Fact]
	public void A_message_of_several_rows_lets_the_log_grow_past_a_hundred()
	{
		var log = new CMessageLog(Localise, 12f, text => text.Length);
		var type = new CMessageType("LONG");
		for (int i = 0; i < 40; i++)
		{
			log.Add(new CMessage(type, " d"));
		}

		Assert.True(log.Rows.Count > CMessageLog.MostRows);
	}

	[Fact]
	public void A_hidden_category_keeps_its_messages_without_rows_and_a_rebuild_stops_after_101()
	{
		var log = new CMessageLog(Localise, 1000f, text => text.Length);
		CMessageType events = Types().Find("EVENTHAPPENUS");
		for (int i = 0; i < 150; i++)
		{
			log.Add(new CMessage(events, " " + i));
		}
		log.SetHidden(EMessageCategory.Events, true);
		Assert.Empty(log.Rows);
		Assert.Equal(150, log.Messages.Count);

		log.SetHidden(EMessageCategory.Events, false);

		Assert.Equal(CMessageLog.MostRebuilt, log.Rows.Count);
		Assert.StartsWith(" 0 ", log.Rows[0].Text);
	}

	[Fact]
	public void A_pacts_message_is_the_players_by_his_part_in_it_with_his_rulers_title_and_none_where_the_type_is_empty()
	{
		CGameState state = ScriptWorld.State();
		state.Country(ScriptWorld.Ger)!.GovernmentType = "national_socialism";
		var table = new Dictionary<string, string>(Table)
		{
			["national_socialism_ruler"] = "Führer",
			["WENAP_2"] = "$MONARCHTITLE$, we have decided to sign a",
			["OTHERNAP_LOG"] = "$ACTOR$ signed a non-aggression pact with $RECIPIENT$.",
			["DIPLOEXPIRE_4"] = "$ACTION$",
			["DIPLOEXPIRE_ALIGN"] = "Align towards the §Y$FACTION$§W",
			["axis"] = "Axis",
		};
		string Words(string key) => table.GetValueOrDefault(key, key);
		CMessageTypes types = CMessageTypes.Read(ClausewitzParser.Parse(
			"WENAP = { } THEYNAP = { } OTHERNAP = { } DIPLOEXPIRE = { }"), _ => { });
		var nap = new CPactMessage("WENAP", "THEYNAP", "OTHERNAP", ScriptWorld.Ger, ScriptWorld.Pol);

		CMessage ours = CMessages.Pact(Words, types, state, ScriptWorld.Ger, nap)!;
		Assert.Equal("WENAP", ours.Type.Name);
		Assert.Equal("Führer, we have decided to sign a", CMessageText.Text(Words, ours, "_2"));
		Assert.Equal("THEYNAP", CMessages.Pact(Words, types, state, ScriptWorld.Pol, nap)!.Type.Name);
		CMessage theirs = CMessages.Pact(Words, types, state, ScriptWorld.Ita, nap)!;
		Assert.Equal("OTHERNAP", theirs.Type.Name);
		Assert.Equal("GER signed a non-aggression pact with POL.", CMessageText.Text(Words, theirs, "_LOG"));

		var expired = new CPactMessage("DIPLOEXPIRE", "", "", ScriptWorld.Ger, ScriptWorld.Ita)
		{
			Faction = "axis", Action = "DIPLOEXPIRE_ALIGN",
		};
		Assert.Null(CMessages.Pact(Words, types, state, ScriptWorld.Pol, expired));
		Assert.Equal("Align towards the §YAxis§W",
			CMessageText.Text(Words, CMessages.Pact(Words, types, state, ScriptWorld.Ger, expired)!, "_4"));
	}

	[Fact]
	public void A_trades_messages_name_its_goods_from_the_side_told_and_an_automated_player_is_not_told()
	{
		CGameState state = ScriptWorld.State();
		var table = new Dictionary<string, string>(Table)
		{
			["ACTRAWITHUS_6"] = "We will supply them with $ACT_TRADE$ per day in exchange for $REC_TRADE$ per day.",
			["WETRADEAG_4"] = "We will supply them with $ACT_TRADE$ per day in exchange for $REC_TRADE$ per day.",
			["BRKTRADEUS_6"] = "We will no longer receive $REC_TRADE$ for $ACT_TRADE$",
			["WEDETRA_5"] = "$ACTORS$",
			["METAL_IRO"] = "Metal",
			["MONEY_IRO"] = "Money",
		};
		string Words(string key) => table.GetValueOrDefault(key, key);
		CMessageTypes types = CMessageTypes.Read(ClausewitzParser.Parse(
			"ACTRAWITHUS = { } WETRADEAG = { } OTHERTRADEAG = { } WEDETRA = { } BRKTRADEUS = { }"), _ => { });
		// Germany buys 10 metal a day from Poland for 0.75 money.
		var terms = OpenHOI3.Sim.Trade.CTradeTerms.None
			.With(fromBlock: true, OpenHOI3.Sim.Economy.EGoods.Money, 750)
			.With(fromBlock: false, OpenHOI3.Sim.Economy.EGoods.Metal, 10000);
		var deal = new OpenHOI3.Sim.Trade.CTradeDeal(ScriptWorld.Ger, ScriptWorld.Pol, terms);
		var accepted = new CPactMessage("ACTRAWITHUS", "WETRADEAG", "OTHERTRADEAG", ScriptWorld.Ger, ScriptWorld.Pol) { Deal = deal };

		Assert.Equal("We will supply them with §Y0.75 Money§W  per day in exchange for §Y10.00 Metal§W  per day.",
			CMessageText.Text(Words, CMessages.Pact(Words, types, state, ScriptWorld.Ger, accepted)!, "_6"));
		Assert.Equal("We will supply them with §Y10.00 Metal§W  per day in exchange for §Y0.75 Money§W  per day.",
			CMessageText.Text(Words, CMessages.Pact(Words, types, state, ScriptWorld.Pol, accepted)!, "_4"));

		// Poland breaks it: Poland told it is the actor, Germany the one who no longer receives the metal.
		var broken = new CPactMessage("WEBRKTRADE", "BRKTRADEUS", "BRKTRADEOTH", ScriptWorld.Pol, ScriptWorld.Ger) { Deal = deal };
		Assert.Equal("We will no longer receive §Y10.00 Metal§W  for §Y0.75 Money§W ",
			CMessageText.Text(Words, CMessages.Pact(Words, types, state, ScriptWorld.Ger, broken)!, "_6"));

		var declined = new CPactMessage("DETRAWITHUS", "WEDETRA", "DETRAWITHOTHER", ScriptWorld.Ger, ScriptWorld.Pol);
		Assert.Equal("GER", CMessageText.Text(Words, CMessages.Pact(Words, types, state, ScriptWorld.Pol, declined)!, "_5"));

		state.AutomateTrade.Add(ScriptWorld.Pol);
		Assert.Null(CMessages.Pact(Words, types, state, ScriptWorld.Pol, accepted));
		Assert.Null(CMessages.Pact(Words, types, state, ScriptWorld.Pol, declined));
	}
}
