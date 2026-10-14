using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The AI's politics against <c>docs/mechanics/politics.md</c>, section 12: a new game's laws looked at until they stop
/// moving, the player's left alone; the minister's countdown and its hour; and the weekly filling of empty posts, whose
/// head of state is not asked whether he may take the post. Germany is played and Italy is not; two ideologies of one
/// group and a third of another; three posts; a press group of three laws.
/// </summary>
public class PoliticsAiTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");

	/// <summary>Lua standing in: each look at a country's laws moves its press law one up; the days are counted.</summary>
	private sealed class StepUp : IAiScripts
	{
		public List<CCountryTag> Ticks { get; } = new();

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) => 0;

		public void RunLaws(CGameState state, CCountry country)
		{
			CLawGroup press = state.LawGroups[0];
			int at = press.Laws.IndexOf(country.Laws[press.Name]);
			if (at + 1 < press.Laws.Count)
			{
				state.Outbox.Add(new CChangeLawCommand(country.Tag, press.Laws[at + 1].Name, press.Name, payCost: false, postMessage: false));
			}
		}

		public void RunPolitics(CGameState state, CCountry country) => Ticks.Add(country.Tag);
	}

	private static CGameState World()
	{
		var fascism = new CIdeologyGroup("fascism", 0, 0);
		fascism.IdeologyOrder.AddRange(new[] { "national_socialist", "fascistic" });
		fascism.Ideologies.UnionWith(fascism.IdeologyOrder);
		var democracy = new CIdeologyGroup("democracy", 0, 0);
		democracy.IdeologyOrder.Add("social_democrat");
		democracy.Ideologies.UnionWith(democracy.IdeologyOrder);

		var press = new CLawGroup("press_laws", 1);
		int index = 1;
		foreach (string name in new[] { "free_press", "censored_press", "propaganda_press" })
		{
			press.Laws.Add(new CLaw(press.Name, new CModifier(name)) { Index = index++ });
		}

		CCountry germany = new() { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		CCountry italy = new() { Tag = Ita, Name = "Italy", DefinitionFile = "", Capital = 2 };
		foreach (CCountry country in new[] { germany, italy })
		{
			country.RulingIdeology = "fascistic";
			country.Laws[press.Name] = press.Laws[0];
		}
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Roma", isSea: false) { Owner = Ita, Controller = Ita },
			},
			new[] { germany, italy })
		{
			IdeologyGroups = new[] { fascism, democracy },
			GovernmentPositions = new[]
			{
				new CGovernmentPosition("head_of_state", 1, false), new CGovernmentPosition("foreign_minister", 2, true),
				new CGovernmentPosition("chief_of_army", 3, true),
			},
			LawGroups = new[] { press },
		};
	}

	private static CMinister Minister(CCountry country, int id, string ideology, params string[] posts)
	{
		var minister = new CMinister(id, $"m{id}") { Ideology = ideology };
		foreach (string post in posts)
		{
			minister.SetType(post, new CMinisterType(new CModifier("type")));
		}
		country.Ministers[id] = minister;
		country.PossibleMinisters.Add(minister);
		return minister;
	}

	[Fact]
	public void ANewGamesLawsMoveUntilTheyStopAndThePlayersStayAsTheHistoryGivesThem()
	{
		CGameState state = World();
		state.Played.Add(Ger);
		state.AiScripts = new StepUp();
		// Two passes that move Italy's press law and a third that posts nothing.
		Assert.Equal(3, CPoliticsAi.SetUpAtStart(state));
		Assert.Equal("propaganda_press", state.Country(Ita)!.Laws["press_laws"].Name);
		Assert.Equal("free_press", state.Country(Ger)!.Laws["press_laws"].Name);
		Assert.Empty(state.Outbox);
	}

	[Fact]
	public void OnlyACountryNobodyPlaysIsGivenAMinisterThatFirstWorksAfterOneToThreeHours()
	{
		CGameState state = World();
		state.Played.Add(Ger);
		CPoliticsAi.SetUpAtStart(state);
		Assert.Equal(0, state.Country(Ger)!.PoliticsCountdown);
		Assert.InRange(state.Country(Ita)!.PoliticsCountdown, 1, 3);
	}

	[Fact]
	public void AMinisterWorksOnceADayInItsCountrysOwnHour()
	{
		CGameState state = World();
		var scripts = new StepUp();
		state.AiScripts = scripts;
		CCountry italy = state.Country(Ita)!;
		italy.PoliticsCountdown = 1;
		// Italy is the second country, number 2: its hour is 19.
		Assert.Equal(19, CPoliticsAi.Hour(italy));
		for (int hour = 0; hour < 48; hour++)
		{
			CPoliticsAi.RunHourly(state);
			state.AdvanceOneHour();
		}
		Assert.Equal(new[] { Ita, Ita }, scripts.Ticks.FindAll(tag => tag == Ita));
	}

	[Fact]
	public void TheRefillPutsTheFirstOfTheRulingPartyElseTheLastOfItsGroupAndNeverAsksTheHeadOfState()
	{
		CGameState state = World();
		CCountry italy = state.Country(Ita)!;
		// Nobody's block names the head of state: the first of the ruling party takes it anyway.
		Minister(italy, 1, "social_democrat", "foreign_minister");
		Minister(italy, 2, "national_socialist", "foreign_minister");
		Minister(italy, 3, "national_socialist", "foreign_minister", "chief_of_army");
		Minister(italy, 4, "fascistic", "chief_of_army");
		italy.Government["chief_of_army"] = italy.Ministers[3];

		CMinisterPosts.FillVacant(state, italy);
		var posted = state.Outbox.ConvertAll(command => (CChangeMinisterCommand)command);
		Assert.Equal(2, posted.Count);
		// The head of state: the first of the ruling party, his block unasked.
		Assert.Equal(("head_of_state", 4), (posted[0].Position, posted[0].Minister));
		// The foreign minister: nobody of the ruling party may, so the last of its group who may.
		Assert.Equal(("foreign_minister", 3), (posted[1].Position, posted[1].Minister));
	}
}
