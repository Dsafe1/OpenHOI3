using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A law or a minister changed, against <c>docs/mechanics/politics.md</c>, section 9: the price, charged only to a
/// country a person plays and free to go below nought; the message; the modifier made again; the peace-time revert
/// of a law its <c>allow</c> no longer permits; and a minister put into a post. Germany holds one law of a press group of
/// three, the last allowed only to Britain.
/// </summary>
public class LawChangeTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Eng = new("ENG");

	private static (CGameState State, CLawGroup Press) World()
	{
		var press = new CLawGroup("press_laws", 1);
		CLaw Law(string name, string kind, string value)
		{
			var modifier = new CModifier(name);
			modifier.Set(kind, CFixedPoint.Parse(value));
			var law = new CLaw("press_laws", modifier) { Cost = CFixedPoint.Parse("0.5"), Index = press.Laws.Count + 1 };
			press.Laws.Add(law);
			return law;
		}
		CLaw free = Law("free_press", "national_unity_effect", "0.2");
		Law("state_press", "national_unity_effect", "-0.05");
		CLaw propaganda = Law("propaganda_press", "national_unity_effect", "-0.1");
		// Propaganda only for Britain: a condition Germany fails.
		propaganda.Allow = CTrigger.All();
		propaganda.Allow.Children.Add(new CTrigger(ETriggerKind.Keyword, "tag", new CScriptArgument("ENG", false) { Tag = Eng }));

		CCountry germany = new() { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1, ActingCapital = 1, BaseIc = 100 };
		germany.Laws["press_laws"] = free;
		CCountry britain = new() { Tag = Eng, Name = "Britain", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger } },
			new[] { germany, britain })
		{
			LawGroups = new[] { press },
			GovernmentPositions = new[] { new CGovernmentPosition("foreign_minister", 1, true) },
		};
		state.Defines.Set(CResources.EconomySection, CResources.IcToMoney, CFixedPoint.Parse("0.05"));
		return (state, press);
	}

	[Fact]
	public void A_law_costs_thirty_days_of_money_from_the_whole_ic_and_only_a_played_country_pays_it()
	{
		(CGameState state, CLawGroup press) = World();
		CCountry germany = state.Country(Ger)!;
		// 100 IC for thirty days at 0.05 is 150 money; the cost of 0.5 makes it 75.
		Assert.Equal(CFixedPoint.Zero, CLaws.ChangeCost(state, germany, press.Laws[1]));
		state.Played.Add(Ger);
		Assert.Equal(CFixedPoint.FromInt(75), CLaws.ChangeCost(state, germany, press.Laws[1]));

		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		new CChangeLawCommand(Ger, "state_press", "press_laws").Execute(state);
		Assert.Equal("state_press", germany.Laws["press_laws"].Name);
		Assert.Equal(CFixedPoint.FromInt(-75), state.Stockpile(germany)![EGoods.Money]);      // nothing asks whether it is there
		Assert.Equal(CFixedPoint.Parse("-0.05"), germany.Modifier["national_unity_effect"]);   // the modifier made again
		CPactMessage message = Assert.Single(told);
		Assert.Equal(CLaws.AppointedMessage, message.We);
		Assert.Contains(("POSITION", "press_laws"), message.Values);

		// A change made free and silent pays nothing and tells nobody.
		new CChangeLawCommand(Ger, "free_press", "press_laws", payCost: false, postMessage: false).Execute(state);
		Assert.Equal(CFixedPoint.FromInt(-75), state.Stockpile(germany)![EGoods.Money]);
		Assert.Single(told);
	}

	[Fact]
	public void At_peace_a_law_no_longer_allowed_gives_way_to_the_last_of_its_group_that_is_and_never_at_war()
	{
		(CGameState state, CLawGroup press) = World();
		CCountry germany = state.Country(Ger)!;
		germany.Laws["press_laws"] = press.Laws[2];                     // propaganda, which Germany may not have

		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Eng);
		state.Wars.Add(war);
		CLaws.OnDiplomacyChanged(state, germany);
		Assert.Empty(state.Outbox);

		// The group's list runs backwards from the file's order, so the walk meets state press before free press: the last
		// allowed in the file's order (measured: China's three laws at the 1939 bookmark).
		state.Wars.Clear();
		CLaws.OnDiplomacyChanged(state, germany);
		var command = Assert.IsType<CChangeLawCommand>(Assert.Single(state.Outbox));
		Assert.Equal("state_press", command.Law);
		Assert.False(command.PayCost);
		Assert.True(command.PostMessage);
	}

	[Fact]
	public void The_peace_time_revert_is_silent_while_the_game_is_being_made()
	{
		(CGameState state, CLawGroup press) = World();
		CCountry germany = state.Country(Ger)!;
		germany.Laws["press_laws"] = press.Laws[2];
		state.IsBeingMade = true;

		CLaws.OnDiplomacyChanged(state, germany);

		// The original's command keeps neither flag before its game is under way.
		var command = Assert.IsType<CChangeLawCommand>(Assert.Single(state.Outbox));
		Assert.False(command.PayCost);
		Assert.False(command.PostMessage);
	}

	[Fact]
	public void A_country_leaving_a_war_by_the_end_of_a_new_games_entry_keeps_the_laws_the_war_allowed()
	{
		(CGameState state, CLawGroup press) = World();
		CCountry germany = state.Country(Ger)!;
		germany.Laws["press_laws"] = press.Laws[2];
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Eng);
		state.Wars.Add(war);

		// Britain owns nothing and is in no exile: taken off the war by the war's own removal, which tells nobody of a change
		// of diplomacy - so Germany, at peace now, is not reverted (measured: the Baltic states at both 1941 bookmarks).
		CWarSides.RemoveLandless(state);
		Assert.Empty(war.Defenders);
		Assert.Empty(state.Outbox);

		// A war left otherwise does tell it.
		war.Defenders.Add(Eng);
		CWarSides.Leave(state, war, Eng);
		Assert.Contains(state.Outbox, posted => posted is CChangeLawCommand { Law: "state_press" });
	}

	[Fact]
	public void A_minister_is_put_into_his_post_with_nothing_checked_and_the_player_told()
	{
		(CGameState state, _) = World();
		CCountry germany = state.Country(Ger)!;
		var minister = new CMinister(7, "Neurath");
		germany.Ministers[7] = minister;
		state.Played.Add(Ger);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;

		Assert.False(new CChangeMinisterCommand(Ger, 7, "no_such_post").MayExecute(state));
		var command = new CChangeMinisterCommand(Ger, 7, "foreign_minister");
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		Assert.Same(minister, germany.Government["foreign_minister"]);    // though his block names no post at all
		Assert.Equal(CChangeMinisterCommand.AppointedMessage, Assert.Single(told).We);
	}
}
