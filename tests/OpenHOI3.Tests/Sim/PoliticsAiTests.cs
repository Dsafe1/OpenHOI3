using System.Collections.Generic;
using System.Linq;
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
				// As the Lua's command class makes one: the two flags kept only while a game is under way.
				state.Outbox.Add(new CChangeLawCommand(country.Tag, press.Laws[at + 1].Name, press.Name,
					payCost: !state.IsBeingMade, postMessage: !state.IsBeingMade));
			}
		}

		public void RunPolitics(CGameState state, CCountry country) => Ticks.Add(country.Tag);
	}

	/// <summary>A script that writes down every country's IC as each country is asked about its laws.</summary>
	private sealed class SeesIc : IAiScripts
	{
		public List<(CCountryTag Asked, int[] Ic)> Seen { get; } = new();

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) => 0;

		public void RunLaws(CGameState state, CCountry country) =>
			Seen.Add((country.Tag, state.Countries.Select(each => each.BaseIc).ToArray()));

		public void RunPolitics(CGameState state, CCountry country)
		{
		}
	}

	[Fact]
	public void ANewGamesLookAtTheLawsSeesNoIcInACountryWhoseTurnIsStillToCome()
	{
		CGameState state = World();
		var scripts = new SeesIc();
		state.AiScripts = scripts;
		state.Country(Ger)!.BaseIc = 100;
		state.Country(Ita)!.BaseIc = 50;

		CPoliticsAi.SetUpAtStart(state);

		// Germany is asked first and sees no IC in Italy; Italy, asked second, sees Germany's (politics.md, section 13).
		Assert.Equal(new[] { 100, 0 }, scripts.Seen[0].Ic);
		Assert.Equal(new[] { 100, 50 }, scripts.Seen[1].Ic);
		Assert.Equal(50, state.Country(Ita)!.BaseIc);
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
	public void ANewGamesLawsMoveOneLookThePlayersToo()
	{
		CGameState state = World();
		state.Played.Add(Ger);
		state.AiScripts = new StepUp();
		// The look posts each country's first step and nothing changes until the days are run: the original's queue
		// runs what the script posted after them.
		CPoliticsAi.SetUpAtStart(state);
		Assert.Equal(2, state.NewGameLaws.Count);
		Assert.Equal("free_press", state.Country(Ita)!.Laws["press_laws"].Name);
		Assert.Equal("free_press", state.Country(Ger)!.Laws["press_laws"].Name);
		// Then the held changes, and no second look - the original asks the script once in each country's set-up, the
		// player's as the AI's (docs/compatibility.md, entry 6, reversed 2026-11-02; politics.md, section 12).
		Assert.Equal(1, CPoliticsAi.RunNewGameLaws(state));
		Assert.Equal("censored_press", state.Country(Ita)!.Laws["press_laws"].Name);
		Assert.Equal("censored_press", state.Country(Ger)!.Laws["press_laws"].Name);
		Assert.Empty(state.Outbox);
		Assert.Empty(state.NewGameLaws);
		Assert.Equal(0, CPoliticsAi.RunNewGameLaws(state));
	}

	[Fact]
	public void ANewGamesFirstFrameIsOneAiPassAtItsFirstHourBeforeAnyHourIsPlayed()
	{
		CGameState state = World();
		var scripts = new StepUp();
		state.AiScripts = scripts;
		// The game stands at the hour of the day that is Italy's politics minister's.
		CCountry germany = state.Country(Ger)!;
		CCountry italy = state.Country(Ita)!;
		while (state.TotalHours % CGameState.HoursPerDay != OpenHOI3.Sim.Research.CResearchAi.Hour(italy, 17))
		{
			state.AdvanceOneHour();
		}
		CDate stands = state.Date;
		long hours = state.TotalHours;
		italy.PoliticsCountdown = 1;
		germany.PoliticsCountdown = 3;

		_ = new CSimulation(state);

		// The minister drawn one hour away, whose hour of the day this is, has worked; the other has counted down once.
		Assert.Equal(new[] { Ita }, scripts.Ticks);
		Assert.Equal(1, italy.PoliticsCountdown);
		Assert.Equal(2, germany.PoliticsCountdown);
		Assert.Equal(stands, state.Date);
		Assert.Equal(hours, state.TotalHours);
	}

	[Fact]
	public void ANewGamesChangesOfLawAreMadeFreeAndSilentThoughRunOnceTheGameIsRunning()
	{
		CGameState state = World();
		state.Played.Add(Ger);
		state.AiScripts = new StepUp();
		int told = 0;
		state.PactMessaged += _ => told++;
		CPoliticsAi.SetUpAtStart(state);
		CPoliticsAi.RunNewGameLaws(state);
		// Every look is made with the game being made, so no command it posts tells the player, though they are run
		// with the game under way.
		Assert.False(state.IsBeingMade);
		Assert.Equal("censored_press", state.Country(Ger)!.Laws["press_laws"].Name);
		Assert.Equal(0, told);
	}

	[Fact]
	public void WhatTheDaysPostedRunsAfterTheLooksChangesInTheOrderItWasPosted()
	{
		CGameState state = World();
		state.AiScripts = new StepUp();
		CPoliticsAi.SetUpAtStart(state);
		// The two days post their own: Italy's press brought back to free, then on to propaganda. The original's queue holds
		// them behind the look's and runs them after it, each asked first whether it may run; one that may not is passed by.
		state.Outbox.Add(new CChangeLawCommand(Ita, "free_press", "press_laws", payCost: false, postMessage: false));
		state.Outbox.Add(new CChangeLawCommand(Ita, "no_such_law", "press_laws", payCost: false, postMessage: false));
		state.Outbox.Add(new CChangeLawCommand(Ita, "propaganda_press", "press_laws", payCost: false, postMessage: false));

		Assert.Equal(1, CPoliticsAi.RunNewGameLaws(state));

		Assert.Equal("propaganda_press", state.Country(Ita)!.Laws["press_laws"].Name);
		Assert.Equal("censored_press", state.Country(Ger)!.Laws["press_laws"].Name);
		Assert.Empty(state.Outbox);
	}

	[Fact]
	public void WhatTheDaysPostedRunsThoughTheLookPostedNothing()
	{
		CGameState state = World();
		state.Outbox.Add(new CChangeLawCommand(Ita, "censored_press", "press_laws", payCost: false, postMessage: false));

		// No script, so no look: none counted, and the days' change runs all the same.
		Assert.Equal(0, CPoliticsAi.RunNewGameLaws(state));
		Assert.Equal("censored_press", state.Country(Ita)!.Laws["press_laws"].Name);
		Assert.Empty(state.Outbox);
	}

	[Fact]
	public void AMinisterIsTheFirstTypeHisBlockGivesForAPost()
	{
		// Honduras's armament minister of 1936: a laissez-faire capitalist, then a resource industrialist.
		var minister = new CMinister(79004, "Roberto Michelleti Bain");
		var first = new CMinisterType(new CModifier("laissez_faires_capitalist"));
		minister.SetType("armament_minister", first);
		minister.SetType("armament_minister", new CMinisterType(new CModifier("resource_industrialist")));

		Assert.Same(first, minister.TypeIn("armament_minister"));
		Assert.True(minister.MayTake("armament_minister"));
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
