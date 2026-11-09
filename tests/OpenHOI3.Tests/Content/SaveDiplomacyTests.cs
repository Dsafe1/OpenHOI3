using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Trade;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Content;

/// <summary>A save's wars, pacts, trade routes and convoys as <c>docs/mechanics/savegame.md</c>, section 6, reads them.</summary>
public class SaveDiplomacyTests
{
	private static CGameState State()
	{
		CCountry Country(string tag, string adjective, string faction = "") =>
			new() { Tag = new CCountryTag(tag), Name = tag, Adjective = adjective, DefinitionFile = "", Faction = faction };
		return new CGameState(new CDate(1936, 1, 7),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Rome", isSea: false) },
			new[] { Country("ITA", "Italian", "axis"), Country("ETH", "Ethiopian"), Country("ENG", "British", "allies"), Country("SAF", "South African"),
				Country("GER", "German", "axis") });
	}

	private static CCountryTag T(string tag) => new(tag);

	[Fact]
	public void Every_kind_of_pact_is_read_with_its_start_and_a_route_answers_its_convoys_number()
	{
		CGameState state = State();
		var report = new CSaveReport();
		List<(CTradeRoute Route, int Convoy)> routes = CSaveDiplomacy.Read(state, ClausewitzParser.Parse("""
			guarantee = { first = "ENG" second = "ETH" end_date = "1956.1.1.0" start_date = "1936.1.1.0" }
			alliance = { first = "ITA" second = "GER" end_date = "1944.10.11.0" start_date = "1936.1.1.0" }
			vassal = { first = "ENG" second = "SAF" start_date = "1936.1.1.0" }
			nap = { balance = 1000.000 our_power = 0 their_power = 0 first = "GER" second = "ENG" start_date = "1936.1.5.3" }
			embargo = { first = "ENG" second = "ITA" start_date = "1936.1.6.0" }
			influence = { first = "ENG" second = "SAF" start_date = "1936.1.1.16" }
			align = { first = "ETH" second = "GER" start_date = "1936.1.6.5" }
			trade = { to = "ITA" from = "ENG" trade_from = { 0.000 0.000 0.000 0.000 3.500 0.000 0.000 } trade_to = { 0.000 0.000 0.229 0.000 0.000 0.000 0.000 } first = "ENG" second = "ITA" start_date = "1936.1.1.4" convoy = { id = 262 type = 42 } }
			"""), report);

		Assert.True(CSaveValues.TryHours("1936.1.1.0", out long newYear));
		Assert.True(CSaveValues.TryHours("1956.1.1.0", out long guaranteeEnd));
		Assert.True(CSaveValues.TryHours("1944.10.11.0", out long allianceEnd));
		// Each numbered in the save's order, which is the order a save writes them in; an end the history gave is kept.
		Assert.Equal(new CGuarantee(T("ENG"), T("ETH"), newYear) { Made = 1, End = guaranteeEnd }, Assert.Single(state.Guarantees));
		Assert.Equal(new CAlliance(T("ITA"), T("GER")) { Made = 2, Start = newYear, End = allianceEnd }, Assert.Single(state.Alliances));
		Assert.Equal(T("ENG"), state.Country(T("SAF"))!.Overlord);
		Assert.Equal((3L, newYear, (long?)null), (state.Country(T("SAF"))!.PuppetMade, state.Country(T("SAF"))!.PuppetStart, state.Country(T("SAF"))!.PuppetEnd));
		Assert.Equal(new[] { 4L, 5L, 6L, 7L, 8L }, new[] { state.Naps.Single().Made, state.Embargoes.Single().Made,
			state.Influences.Single().Made, state.Aligns.Single().Made, routes.Single().Route.Made });
		Assert.Equal((T("GER"), T("ENG")), (state.Naps.Single().First, state.Naps.Single().Second));
		Assert.Single(state.Embargoes);
		// An influence works for its influencer's faction, an alignment for its leader's.
		Assert.Equal("allies", state.Influences.Single().Faction);
		Assert.Equal("axis", state.Aligns.Single().Faction);

		(CTradeRoute route, int convoy) = Assert.Single(routes);
		Assert.Equal((T("ENG"), T("ITA"), 262), (route.From, route.To, convoy));
		Assert.Equal(3500, route.Terms.From(EGoods.Metal));
		Assert.Equal(229, route.Terms.To(EGoods.Money));
		Assert.DoesNotContain(("nap", "balance"), report.UnreadKeys.Keys);
		Assert.Equal(1000000, state.Naps[0].Balance);
		Assert.DoesNotContain(("alliance", "end_date"), report.UnreadKeys.Keys);
		Assert.DoesNotContain(("guarantee", "end_date"), report.UnreadKeys.Keys);
	}

	[Fact]
	public void The_pacts_are_written_back_to_the_byte_in_the_order_they_were_made_whatever_their_kind()
	{
		// A save's own order mixes the kinds, as the original keeps one list; each kind's keys in the original's order.
		const string Saved =
			"diplomacy=\r\n{\r\n" +
			"\tguarantee=\r\n\t{\r\n\t\tfirst=\"ENG\"\r\n\t\tsecond=\"ETH\"\r\n\t\tend_date=\"1956.1.1.0\"\r\n\t\tstart_date=\"1936.1.1.0\"\r\n\t}\r\n" +
			"\ttrade=\r\n\t{\r\n\t\tto=\"ITA\"\r\n\t\tfrom=\"ENG\"\r\n\t\ttrade_from=\r\n\t\t{\r\n0.000 0.000 0.000 0.000 3.500 0.000 0.000 \t\t}\r\n" +
			"\t\ttrade_to=\r\n\t\t{\r\n0.000 0.000 0.229 0.000 0.000 0.000 0.000 \t\t}\r\n\t\tconvoy=\r\n\t\t{\r\n\t\t\tid=262\r\n\t\t\ttype=42\r\n\t\t}\r\n" +
			"\t\tfirst=\"ENG\"\r\n\t\tsecond=\"ITA\"\r\n\t\tstart_date=\"1936.1.1.4\"\r\n\t}\r\n" +
			"\talliance=\r\n\t{\r\n\t\tfirst=\"ITA\"\r\n\t\tsecond=\"GER\"\r\n\t\tstart_date=\"1936.1.3.0\"\r\n\t}\r\n" +
			"\tvassal=\r\n\t{\r\n\t\tfirst=\"ENG\"\r\n\t\tsecond=\"SAF\"\r\n\t\tend_date=\"1945.1.1.0\"\r\n\t\tstart_date=\"1936.1.1.0\"\r\n\t}\r\n" +
			"\tnap=\r\n\t{\r\n\t\tbalance=-1.000\r\n\t\tour_power=0\r\n\t\ttheir_power=0\r\n\t\tfirst=\"GER\"\r\n\t\tsecond=\"ENG\"\r\n\t\tstart_date=\"1936.1.5.3\"\r\n\t}\r\n" +
			"\tembargo=\r\n\t{\r\n\t\tfirst=\"ENG\"\r\n\t\tsecond=\"ITA\"\r\n\t\tstart_date=\"1936.1.6.0\"\r\n\t}\r\n" +
			"\tinfluence=\r\n\t{\r\n\t\tfirst=\"ENG\"\r\n\t\tsecond=\"SAF\"\r\n\t\tstart_date=\"1936.1.1.16\"\r\n\t}\r\n" +
			"\talign=\r\n\t{\r\n\t\tfirst=\"ETH\"\r\n\t\tsecond=\"GER\"\r\n\t\tstart_date=\"1936.1.6.5\"\r\n\t}\r\n" +
			"}\r\n";
		CGameState state = State();
		foreach ((CTradeRoute route, int convoy) in CSaveDiplomacy.Read(state, ClausewitzParser.Parse(Saved).Get("diplomacy")!, new CSaveReport()))
		{
			route.Convoy = new CConvoy { Id = convoy, Owner = T("ITA"), Start = 1, End = 1 };
			state.TradeRoutes.Add(route);
		}

		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveDiplomacy.Write(writer, state);

		Assert.Equal(Saved, writer.Text);
	}

	[Fact]
	public void A_war_made_in_play_is_written_under_its_name_written_out_and_a_finished_one_as_the_original_keeps_it()
	{
		CGameState state = State();
		Assert.True(CSaveValues.TryHours("1939.9.3.11", out long declared));
		var war = new CWar("") { MadeName = new CWarName(CWarName.Aggression, "German", "Polish", 2), Action = declared };
		war.Attackers.Add(T("GER"));
		war.Defenders.Add(T("ENG"));
		war.Defenders.Add(T("SAF"));
		war.Goals[T("GER")] = new List<CWarGoal> { new(CCasusBelliType.Null, T("GER"), T("ENG")) };
		string Localise(string key) => key switch
		{
			CWarName.Aggression => "$ORDER$War of $FIRST$ Aggression",
			"ND" => "nd",
			_ => key,
		};

		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveWars.Write(writer, "active_war", war, Localise);
		CSaveWars.Write(writer, "previous_war", new CWar(""), Localise);

		Assert.Equal(
			"active_war=\r\n{\r\n\tname=\"2nd War of German Aggression\"\r\n\tlimited=no\r\n\tattacker=\"GER\"\r\n\tdefender=\"ENG\"\r\n" +
			"\tdefender=\"SAF\"\r\n\taction=\"1939.9.3.11\"\r\n\toriginal_attacker=\"---\"\r\n\toriginal_defender=\"---\"\r\n" +
			$"\twar_goal=\r\n\t{{\r\n\t\tcasus_belli=\"{CCasusBelliType.Null.Name}\"\r\n\t\tactor=\"GER\"\r\n\t\treceiver=\"ENG\"\r\n\t}}\r\n}}\r\n" +
			// A finished war as every save has two: no name, no sides and the null date.
			"previous_war=\r\n{\r\n\tname=\"\"\r\n\tlimited=no\r\n\taction=\"1.1.1.0\"\r\n\toriginal_attacker=\"---\"\r\n\toriginal_defender=\"---\"\r\n}\r\n",
			writer.Text);
	}

	[Fact]
	public void A_name_a_declaration_made_is_read_back_into_its_parts_and_a_historys_is_left_as_it_is()
	{
		string Localise(string key) => key switch
		{
			CWarName.Aggression => "$ORDER$War of $FIRST$ Aggression",
			CWarName.Normal => "$ORDER$$FIRST$-$SECOND$ War",
			"ST" => "st",
			"ND" => "nd",
			"RD" => "rd",
			"TH" => "th",
			_ => key,
		};
		CGameState state = State();
		CWar war = CSaveWars.Read(state, ClausewitzParser.Parse("""
			active_war = { name = "3rd War of German Aggression" limited = no attacker = "GER" defender = "ENG" action = "1940.4.9.0" }
			""").Get("active_war")!, new CSaveReport(), Localise);

		// So a war declared after the load is not given a name a loaded one holds (war.md, section 7).
		Assert.Equal(new CWarName(CWarName.Aggression, "German", "", 3), war.MadeName);
		var adjectives = new HashSet<string> { "German", "Italian", "Ethiopian" };
		Assert.Equal(new CWarName(CWarName.Aggression, "Italian", "", 1), CSaveWars.MadeName("War of Italian Aggression", Localise, adjectives));
		Assert.Equal(new CWarName(CWarName.Normal, "Ethiopian", "Italian", 1), CSaveWars.MadeName("Ethiopian-Italian War", Localise, adjectives));
		// The history's name fits the revolt war's text, and its parts are no country's adjectives.
		Assert.Null(CSaveWars.MadeName("Second Italo-Abyssinian War", Localise, adjectives));
		Assert.Null(CSaveWars.MadeName("War of Spanish Aggression", Localise, adjectives));
		Assert.Null(CSaveWars.MadeName("", Localise, adjectives));
		// An order written with the wrong suffix is no name the original makes.
		Assert.Null(CSaveWars.MadeName("3th War of German Aggression", Localise, adjectives));
	}

	[Fact]
	public void A_convoy_is_written_back_to_the_byte_a_lend_lease_one_with_its_giver()
	{
		const string Saved =
			"convoy=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=1979\r\n\t\ttype=42\r\n\t}\r\n\tstart=4016\r\n\tend=1255\r\n\tescorts=0\r\n\tconvoys=8\r\n" +
			"\tpath=\r\n\t{\r\n\t\t4016 10920 1255 \t}\r\n\tlast_attack=\"1936.3.20.7\"\r\n\tstart_date=\"1936.3.5.19\"\r\n" +
			"\tdaily=\r\n\t{\r\n\t\tsupplies=30.000\r\n\t\tfuel=30.000\r\n\t}\r\n\ttrade=no\r\n\tship=\r\n\t{\r\n1 1 0 0 0 0 0 \t}\r\n" +
			"\tlend_lease=yes\r\n\tlendlease_targets=\"ENG\"\r\n}\r\n";
		CGameState state = State();
		CConvoy convoy = CSaveConvoys.Read(state, state.Country(T("ITA"))!, ClausewitzParser.Parse(Saved).Get("convoy")!, new CSaveReport());

		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveConvoys.Write(writer, convoy);

		Assert.Equal(Saved, writer.Text);
	}

	[Fact]
	public void The_historys_pacts_are_numbered_by_their_start_and_then_in_the_order_they_were_read()
	{
		CGameState state = State();
		// Read in the files' order - an alliance, a guarantee, a puppet - the guarantee begun a year earlier.
		state.Alliances.Add(new CAlliance(T("ITA"), T("GER")) { Made = 1, Start = 100 });
		state.Guarantees.Add(new CGuarantee(T("ENG"), T("ETH"), 50) { Made = 2 });
		CCountry southAfrica = state.Country(T("SAF"))!;
		southAfrica.Overlord = T("ENG");
		southAfrica.PuppetMade = 3;
		southAfrica.PuppetStart = 100;

		CGameStateLoader.NumberHistoryPacts(state);

		Assert.Equal((1L, 2L, 3L), (state.Guarantees[0].Made, state.Alliances[0].Made, southAfrica.PuppetMade));
		Assert.Equal(3, state.PactsMade);
	}

	[Fact]
	public void A_war_keeps_its_sides_in_order_and_files_a_goal_under_its_actor()
	{
		CGameState state = State();
		var report = new CSaveReport();
		CWar war = CSaveWars.Read(state, ClausewitzParser.Parse("""
			active_war = { name = "Second Italo-Abyssinian War" limited = no attacker = "ITA" attacker = "GER" defender = "ETH"
				action = "1936.1.1.0" original_attacker = "---" original_defender = "---"
				war_goal = { casus_belli = "puppet" actor = "ITA" receiver = "ETH" } }
			""").Get("active_war")!, report);

		Assert.Equal("Second Italo-Abyssinian War", war.Name);
		Assert.Equal(new[] { T("ITA"), T("GER") }, war.Attackers);
		Assert.Equal(new[] { T("ETH") }, war.Defenders);
		Assert.True(CSaveValues.TryHours("1936.1.1.0", out long action));
		Assert.Equal(action, war.Action);
		CWarGoal goal = Assert.Single(war.GoalsOf(T("ITA")));
		Assert.Equal(T("ETH"), goal.Recipient);
		Assert.Empty(report.UnreadKeys);
	}

	[Fact]
	public void A_convoy_keeps_its_number_ships_way_and_goods_and_raises_the_count()
	{
		CGameState state = State();
		CCountry england = state.Country(T("ENG"))!;
		CConvoy convoy = CSaveConvoys.Read(state, england, ClausewitzParser.Parse("""
			id = { id = 1017 type = 42 } start = 1742 end = 4561 escorts = 2 convoys = 3 path = { 1742 10515 4561 }
			last_attack = "1.1.1.0" start_date = "1936.1.29.2" daily = { money = 0.090 rare_materials = 0.600 }
			trade = yes ship = { 0 0 1 0 0 0 1 } lend_lease = no
			"""), new CSaveReport());

		Assert.Same(convoy, Assert.Single(england.Convoys));
		Assert.Equal((1017, 1742, 4561, 3, 2), (convoy.Id, convoy.Start, convoy.End, convoy.Transports, convoy.Escorts));
		Assert.Equal(new[] { 1742, 10515, 4561 }, convoy.Path);
		Assert.True(convoy.IsTrade);
		Assert.Equal(new[] { EGoods.Money, EGoods.RareMaterials },
			System.Enum.GetValues<EGoods>().Where(goods => convoy.Carries[(int)goods]));
		Assert.Equal(new CFixedPoint(600), convoy.Daily[EGoods.RareMaterials]);
		Assert.Equal(CConvoy.NeverAttacked, convoy.LastAttackHour);
		Assert.Equal(1017, state.LastConvoyId);
	}
}
