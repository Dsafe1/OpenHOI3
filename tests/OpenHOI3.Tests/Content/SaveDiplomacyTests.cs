using System.Collections.Generic;
using System.Linq;
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
		CCountry Country(string tag, string faction = "") => new() { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "", Faction = faction };
		return new CGameState(new CDate(1936, 1, 7),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Rome", isSea: false) },
			new[] { Country("ITA", "axis"), Country("ETH"), Country("ENG", "allies"), Country("SAF"), Country("GER", "axis") });
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
		Assert.Equal(new CGuarantee(T("ENG"), T("ETH"), newYear), Assert.Single(state.Guarantees));
		Assert.Equal(new CAlliance(T("ITA"), T("GER")), Assert.Single(state.Alliances));
		Assert.Equal(T("ENG"), state.Country(T("SAF"))!.Overlord);
		Assert.Equal((T("GER"), T("ENG")), (state.Naps.Single().First, state.Naps.Single().Second));
		Assert.Single(state.Embargoes);
		// An influence works for its influencer's faction, an alignment for its leader's.
		Assert.Equal("allies", state.Influences.Single().Faction);
		Assert.Equal("axis", state.Aligns.Single().Faction);

		(CTradeRoute route, int convoy) = Assert.Single(routes);
		Assert.Equal((T("ENG"), T("ITA"), 262), (route.From, route.To, convoy));
		Assert.Equal(3500, route.Terms.From(EGoods.Metal));
		Assert.Equal(229, route.Terms.To(EGoods.Money));
		// What nothing here holds is reported: an alliance's end, and a NAP's balance and powers.
		Assert.Contains(("alliance", "end_date"), report.UnreadKeys.Keys);
		Assert.Contains(("nap", "balance"), report.UnreadKeys.Keys);
		Assert.DoesNotContain(("guarantee", "end_date"), report.UnreadKeys.Keys);
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
