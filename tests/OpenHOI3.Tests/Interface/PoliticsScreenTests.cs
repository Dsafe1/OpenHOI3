using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the politics screen writes, against <c>docs/mechanics/politics.md</c>, section 11: the elections line, the pie's
/// slices, the choice of a minister and the Replace button, a law's price and whether it may be chosen, and the
/// tooltips' change factors and strategic-warfare figures. Three ideologies in one group; Germany a dictatorship, Britain
/// a democracy with elections every 48 months.
/// </summary>
public class PoliticsScreenTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Eng = new("ENG");

	private static readonly Dictionary<string, string> Texts = new()
	{
		["TERM_FOR_LIFE"] = "This government has no elections.",
		["TERM_DURATION"] = "Elections are held every §Y$YEARS$§W months.",
		["NEXT_ELECTION"] = "Next Election",
		["POL_PARTY_POP_FACTORS"] = "Internal Factors: ",
		["POL_EVENTS"] = "Events: ",
		["POL_SPIES"] = "Spies: ",
		["PO_NO_CHANGE"] = "No changes this month",
		["POL_SW_CON"] = "$EVIL$ attacks, §R$ESC$§W; $GOOD$ beaten off, §G+$GSC$§W.",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CGameState World()
	{
		var group = new CIdeologyGroup("all", 0, 0);
		group.IdeologyOrder.AddRange(new[] { "national_socialist", "social_conservative", "social_democrat" });
		group.Ideologies.UnionWith(group.IdeologyOrder);
		var dictatorship = new CGovernment("dictatorship");
		dictatorship.Accepted.UnionWith(new[] { "national_socialist" });
		var democracy = new CGovernment("social_democracy") { HasElections = true, Duration = 48 };
		democracy.Accepted.UnionWith(new[] { "social_conservative", "social_democrat" });

		CCountry germany = new() { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		germany.RulingIdeology = "national_socialist";
		germany.GovernmentType = "dictatorship";
		CCountry britain = new() { Tag = Eng, Name = "Britain", DefinitionFile = "" };
		britain.RulingIdeology = "social_democrat";
		britain.GovernmentType = "social_democracy";
		britain.LastElection = new CDate(1935, 11, 14);

		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger } },
			new[] { germany, britain })
		{
			IdeologyGroups = new[] { group },
			Governments = new Dictionary<string, CGovernment> { [dictatorship.Name] = dictatorship, [democracy.Name] = democracy },
			GovernmentPositions = new[]
			{
				new CGovernmentPosition("head_of_state", 1, false), new CGovernmentPosition("foreign_minister", 2, true),
			},
		};
		foreach (CCountry country in state.Countries)
		{
			country.Popularity = new CPartyFigures(4);
			country.Organisation = new CPartyFigures(4);
		}
		return state;
	}

	private static CMinister Minister(CCountry country, int id, string ideology, params string[] posts)
	{
		var minister = new CMinister(id, $"m{id}") { Ideology = ideology };
		foreach (string post in posts)
		{
			minister.SetType(post, new CMinisterType(new CModifier("type")));
		}
		country.PossibleMinisters.Add(minister);
		return minister;
	}

	[Fact]
	public void ALifelongGovernmentHasNoElectionsLine()
	{
		CGameState state = World();
		Assert.Equal("This government has no elections.\n", CPoliticsScreenText.ElectionsLine(Localise, state, state.Country(Ger)!));
	}

	[Fact]
	public void TheNextElectionRunsOnInYellowWithNoLineBreak()
	{
		CGameState state = World();
		string line = CPoliticsScreenText.ElectionsLine(Localise, state, state.Country(Eng)!);
		Assert.Equal("Elections are held every §Y48§W months.\nNext Election: §YNovember 14, 1939", line);
	}

	[Fact]
	public void ThePieIsWholePercentsRoundedHalfToEvenAndLeavesOutTheNullParty()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		// 12.5, 13.5 and 74 of a hundred; the null party's 50 counts for nothing.
		germany.Popularity.Values[0] = 50000;
		germany.Popularity.Values[1] = 12500;
		germany.Popularity.Values[2] = 13500;
		germany.Popularity.Values[3] = 74000;
		Assert.Equal(new[] { 12, 14, 74 }, CPoliticsScreenText.PieSlices(state, germany).Select(slice => slice.Percent));
	}

	[Fact]
	public void ThePieIsEmptyWhereTheTotalIsOneOrLess()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		germany.Popularity.Values[1] = 600;
		germany.Popularity.Values[2] = 400;
		Assert.Empty(CPoliticsScreenText.PieSlices(state, germany));
	}

	[Fact]
	public void TheChoiceListsTheHolderAndReplaceNeedsAnotherMan()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CMinister holder = Minister(germany, 1, "national_socialist", "foreign_minister", "head_of_state");
		Minister(germany, 2, "social_democrat", "foreign_minister");
		germany.Government["foreign_minister"] = holder;
		germany.Government["head_of_state"] = holder;
		CGovernmentPosition foreign = state.GovernmentPositions[1];

		// The democrat is not accepted by a dictatorship, so the holder alone is offered and Replace is greyed.
		Assert.Equal(new[] { holder }, CPoliticsScreenText.Choices(state, germany, "foreign_minister"));
		Assert.False(CPoliticsScreenText.MayReplace(state, germany, foreign));

		CMinister other = Minister(germany, 3, "national_socialist", "foreign_minister");
		Assert.Equal(new[] { holder, other }, CPoliticsScreenText.Choices(state, germany, "foreign_minister"));
		Assert.True(CPoliticsScreenText.MayReplace(state, germany, foreign));

		// A post that is not changeable never may be, whoever there is.
		Minister(germany, 4, "national_socialist", "head_of_state");
		Assert.False(CPoliticsScreenText.MayReplace(state, germany, state.GovernmentPositions[0]));
	}

	[Fact]
	public void ALawIsFreeForACountryNobodyPlaysAndMayNotBeTheOneInForce()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		var group = new CLawGroup("press_laws", 1);
		var free = new CLaw(group.Name, new CModifier("free_press")) { Cost = CFixedPoint.Parse("0.5") };
		var censored = new CLaw(group.Name, new CModifier("censored_press")) { Cost = CFixedPoint.Parse("0.5") };
		group.Laws.AddRange(new[] { free, censored });
		germany.Laws[group.Name] = free;

		Assert.Equal("censored_press (§G0.0§W)", CPoliticsScreenText.PossibleLawName(Localise, state, germany, censored));
		Assert.True(CPoliticsScreenText.MayChoose(state, germany, censored));
		Assert.False(CPoliticsScreenText.MayChoose(state, germany, free));
	}

	[Fact]
	public void EventsAndSpiesEndInAPlusAndOnlyInternalFactorsClosesItsColour()
	{
		var figures = new CPartyFigures(2);
		figures.LastChange[1] = 1660;
		figures.Tallies[1, CPartyFigures.EffectSlot] = 2000;
		figures.Tallies[1, CPartyFigures.SpySlot] = -500;
		Assert.Equal("Internal Factors: §G+1.66§W\nEvents: §G2+\nSpies: §R-0.5+\n", CPoliticsTooltips.Factors(Localise, figures, 1));
		Assert.Equal("No changes this month", CPoliticsTooltips.Factors(Localise, new CPartyFigures(2), 1));
	}

	[Fact]
	public void AStrategicWarfareFigureIsRedGreenOrYellow()
	{
		Assert.Equal("§R-1.25", CPoliticsScreenText.StrategicWarfare(-1250));
		Assert.Equal("§G+0.50", CPoliticsScreenText.StrategicWarfare(500));
		Assert.Equal("§Y0.00", CPoliticsScreenText.StrategicWarfare(0));
	}

	[Fact]
	public void AWarfareFigureCountsTheEntriesForAndAgainst()
	{
		var entries = new List<(long, long)> { (1, -500), (2, -250), (3, 400) };
		Assert.Equal("2 attacks, §R-0.75§W; 1 beaten off, §G+0.40§W.",
			CPoliticsTooltips.WarfareFigure(Localise, "POL_SW_CON", entries).Text);
	}
}
