using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a country's modifier is made of beyond its laws, against
/// <c>docs/mechanics/modifiers.md</c>, sections 4 to 6: strategic resources and who
/// shares them, the provinces it controls, war and peace, dissent, and what a minister
/// does to how fast a country forgets.
/// </summary>
public class CountryModifierTests
{
	private static readonly CBuilding Infra = new(0, "infra", 10, "infrastructure", CFixedPoint.Parse("0.1"));
	private static readonly CBuilding Industry = new(1, "industry", 10, "ic", CFixedPoint.One);

	/// <summary>Not a building of the original's: something that takes half a province's infrastructure away.</summary>
	private static readonly CBuilding Ruin = new(2, "ruin", 1, "local_infrastructure", CFixedPoint.Parse("-0.5"));

	private static readonly CBuilding NavalBase = new(3, "naval_base", 10, "naval_base_efficiency", CFixedPoint.Zero);

	private static CModifier Modifier(string name, params (string Kind, string Value)[] values)
	{
		var modifier = new CModifier(name);
		foreach ((string kind, string value) in values)
		{
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return modifier;
	}

	private static CCountry Country(string tag, int capital, string faction = "", bool major = false)
	{
		return new CCountry
		{
			Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "", Capital = capital, Faction = faction, IsMajor = major,
		};
	}

	/// <summary>Land its holder owns, controls and has a claim to, with roads unless told otherwise.</summary>
	private static CProvince Land(int id, string holder, string resource = "", int infra = 5,
		string continent = "west", int industry = 0)
	{
		var tag = new CCountryTag(holder);
		var province = new CProvince(id, $"P{id}", isSea: false)
		{
			Owner = tag,
			Controller = tag,
			Continent = continent,
			StrategicResource = resource,
		};
		province.Cores.Add(tag);
		province.SetBuildingLevel(Infra, CFixedPoint.FromInt(infra));
		province.SetBuildingLevel(Industry, CFixedPoint.FromInt(industry));
		return province;
	}

	private static CGameState State(CCountry[] countries, CProvince[] provinces, params CWar[] wars)
	{
		return State(countries, provinces, System.Array.Empty<CRegion>(), System.Array.Empty<int>(), wars);
	}

	private static CGameState State(CCountry[] countries, CProvince[] provinces, CRegion[] regions, int[] coastlines,
		params CWar[] wars)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		all.AddRange(provinces);

		var statics = new[]
		{
			Modifier("base_values", ("ic", "5")),
			Modifier("war", ("org_regain", "0.05")),
			Modifier("peace", ("max_war_exhaustion", "100")),
			Modifier("dissent", ("global_ic", "-0.01")),
		};
		var defines = new CDefines();
		defines.Set("economy", "IC_TO_MONEY", CFixedPoint.Parse("0.05"));
		defines.Set("economy", "IC_TO_SUPPLIES", CFixedPoint.FromInt(7));
		defines.Set("military", "MAX_MANPOWER", CFixedPoint.FromInt(10));
		defines.Set("country", "BASE_TECH_DECAY", CFixedPoint.Parse("-0.025"));
		var resources = new[]
		{
			Modifier("gold", ("global_money", "0.33")),
			Modifier("black_soil", ("global_manpower_modifier", "0.1")),
		};
		var categories = new[] { new CTechnologyCategory("naval_engineering", true) };
		var state = new CGameState(new CDate(1936, 1, 10), all, countries, new[] { Infra, Industry, Ruin, NavalBase },
			staticModifiers: statics, defines: defines, wars: wars, strategicResources: resources,
			technologyCategories: categories)
		{
			Regions = regions,
			CoastlineRegions = coastlines,
		};
		// What the map loader does: each province is given the regions that list it, in their order.
		for (int index = 0; index < regions.Length; index++)
		{
			foreach (int id in regions[index].Provinces)
			{
				state.Provinces[id].Regions.Add(index);
			}
		}
		return state;
	}

	/// <summary>A sea province, and the land beside it.</summary>
	private static CProvince Sea(int id)
	{
		return new CProvince(id, $"S{id}", isSea: true);
	}

	/// <summary>Gives a land province a naval base on the sea beside it.</summary>
	private static CProvince Port(CProvince land, int sea, int level = 1)
	{
		land.Edges = new[] { new CProvinceEdge(sea, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero) };
		land.SetBuildingMaxLevel(NavalBase, CFixedPoint.FromInt(level));
		land.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(level));
		return land;
	}

	private static CSimulation RunADay(CGameState state)
	{
		var simulation = new CSimulation(state);
		simulation.RunHours(24);
		return simulation;
	}

	// --- strategic resources ---

	[Fact]
	public void NobodyHasTheUseOfAResourceUntilADayHasBeenRun()
	{
		CCountry holland = Country("HOL", 1);
		CGameState state = State(new[] { holland }, new[] { Land(1, "HOL", "gold") });

		var simulation = new CSimulation(state);
		Assert.Equal(CFixedPoint.Zero, holland.Modifier["global_money"]);

		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.Parse("0.33"), holland.Modifier["global_money"]);
	}

	[Fact]
	public void AResourceIsWorthWhatIsLeftOfItsProvincesInfrastructure()
	{
		CCountry holland = Country("HOL", 1);
		CProvince goldfield = Land(1, "HOL", "gold");
		goldfield.SetBuildingLevel(Ruin, CFixedPoint.One);
		CGameState state = State(new[] { holland }, new[] { goldfield });
		RunADay(state);

		Assert.Equal(CFixedPoint.Parse("0.5"), holland.StrategicResources["gold"]);
		Assert.Equal(CFixedPoint.Parse("0.165"), holland.Modifier["global_money"]);
	}

	[Fact]
	public void AProvinceWithNoInfrastructureGivesNothing()
	{
		CCountry holland = Country("HOL", 1);
		CGameState state = State(new[] { holland }, new[] { Land(1, "HOL", "gold", infra: 0) });
		RunADay(state);

		Assert.Equal(CFixedPoint.Zero, holland.Modifier["global_money"]);
	}

	[Fact]
	public void TwoProvincesOfOneResourceCountOnceAtTheBetterOfTheTwo()
	{
		CCountry southAfrica = Country("SAF", 1);
		CProvince poor = Land(2, "SAF", "gold");
		poor.SetBuildingLevel(Ruin, CFixedPoint.One);
		CGameState state = State(new[] { southAfrica }, new[] { Land(1, "SAF", "gold"), poor });
		RunADay(state);

		Assert.Equal(CFixedPoint.Parse("0.33"), southAfrica.Modifier["global_money"]);
	}

	[Fact]
	public void AResourceOnceHadIsKeptWhenTheProvinceIsLost()
	{
		// The original's one routine for the figure only raises it, and nothing else in it
		// writes the figure but a new session's reset (modifiers.md, section 6).
		CCountry holland = Country("HOL", 1);
		CCountry japan = Country("JAP", 3);
		CProvince goldfield = Land(2, "HOL", "gold");
		CGameState state = State(new[] { holland, japan }, new[] { Land(1, "HOL"), goldfield, Land(3, "JAP") });
		CSimulation simulation = RunADay(state);

		Assert.Equal(CFixedPoint.Parse("0.33"), holland.Modifier["global_money"]);
		Assert.Equal(CFixedPoint.Zero, japan.Modifier["global_money"]);

		goldfield.Controller = japan.Tag;
		simulation.RunHours(24);

		Assert.Equal(CFixedPoint.Parse("0.33"), japan.Modifier["global_money"]);
		Assert.Equal(CFixedPoint.Parse("0.33"), holland.Modifier["global_money"]);
	}

	[Fact]
	public void AMajorIsGivenWhatItsFactionHoldsOnItsOwnContinent()
	{
		// France's black soil and the United Kingdom, in the original's savegame.
		CCountry france = Country("FRA", 1, "allies", major: true);
		CCountry britain = Country("ENG", 3, "allies", major: true);
		CCountry belgium = Country("BEL", 4, "allies");
		CCountry germany = Country("GER", 5, "axis", major: true);
		CGameState state = State(new[] { france, britain, belgium, germany }, new[]
		{
			Land(1, "FRA"), Land(2, "FRA", "black_soil", continent: "east"), Land(3, "ENG"), Land(4, "BEL"), Land(5, "GER"),
		});
		RunADay(state);

		CFixedPoint tenth = CFixedPoint.Parse("0.1");
		Assert.Equal(tenth, france.Modifier["global_manpower_modifier"]);
		Assert.Equal(tenth, britain.Modifier["global_manpower_modifier"]);
		Assert.Equal(CFixedPoint.Zero, belgium.Modifier["global_manpower_modifier"]);   // in the faction, and small
		Assert.Equal(CFixedPoint.Zero, germany.Modifier["global_manpower_modifier"]);   // a major, in another
	}

	[Fact]
	public void ABigIndustryCountsAsBeingAMajor()
	{
		CCountry france = Country("FRA", 1, "allies");
		CCountry canada = Country("CAN", 2, "allies");
		CGameState state = State(new[] { france, canada },
			new[] { Land(1, "FRA", "gold"), Land(2, "CAN", industry: 10), Land(3, "CAN", industry: 10) });

		// A base of 25 IC is not enough, whatever else is true.
		RunADay(state);
		Assert.Equal(CFixedPoint.Zero, canada.Modifier["global_money"]);

		var provinces = new List<CProvince> { Land(1, "FRA", "gold") };
		for (int id = 2; id <= 11; id++)
		{
			provinces.Add(Land(id, "CAN", industry: 10));
		}
		state = State(new[] { france = Country("FRA", 1, "allies"), canada = Country("CAN", 2, "allies") }, provinces.ToArray());
		RunADay(state);
		Assert.Equal(105, canada.BaseIc);
		Assert.Equal(CFixedPoint.Parse("0.33"), canada.Modifier["global_money"]);
	}

	[Fact]
	public void AFactionOnAnotherContinentKeepsItsResourcesToItself()
	{
		CCountry southAfrica = Country("SAF", 1, "allies");
		CCountry britain = Country("ENG", 2, "allies", major: true);
		CGameState state = State(new[] { southAfrica, britain },
			new[] { Land(1, "SAF", "gold", continent: "africa"), Land(2, "ENG") });
		RunADay(state);

		Assert.Equal(CFixedPoint.Zero, britain.Modifier["global_money"]);
	}

	/// <summary>
	/// South Africa's gold and the United Kingdom on another continent: a naval base of each beside the seas 4 and 5. The
	/// regions are 0, the seas' own, listing both, and 1 and 2, one sea each; <paramref name="coastlines"/> is which of
	/// them coastlines.txt names.
	/// </summary>
	private static (CCountry Britain, CGameState State) Coasts(int[] coastlines, string britainsBaseHolder = "ENG",
		int southAfricasBase = 1)
	{
		CCountry southAfrica = Country("SAF", 1, "allies");
		CCountry britain = Country("ENG", 2, "allies", major: true);
		CCountry france = Country("FRA", 6, "allies");
		CProvince britishBase = Port(Land(3, "ENG"), 5);
		britishBase.Controller = new CCountryTag(britainsBaseHolder);
		var regions = new[]
		{
			new CRegion("both_seas", new[] { 4, 5 }, false),
			new CRegion("atlantic_coast_region", new[] { 4 }, false),
			new CRegion("north_sea_region", new[] { 5 }, false),
			new CRegion("indian_coast_region", new[] { 4, 5 }, false),
		};
		CGameState state = State(new[] { southAfrica, britain, france }, new[]
		{
			Port(Land(1, "SAF", "gold", continent: "africa"), 4, southAfricasBase), Land(2, "ENG"), britishBase, Sea(4), Sea(5),
			Land(6, "FRA"),
		}, regions, coastlines);
		RunADay(state);
		return (britain, state);
	}

	[Fact]
	public void AFactionOnAnotherContinentSharesThroughNavalBasesOnOneOfTheCoastlines()
	{
		(CCountry britain, _) = Coasts(new[] { 3 });
		Assert.Equal(CFixedPoint.Parse("0.33"), britain.Modifier["global_money"]);
	}

	[Fact]
	public void TwoCoastlinesDoNotJoinThoughARegionOffTheListHoldsBothSeas()
	{
		// Regions 1 and 2 are each touched by one country only; region 0 by both, but coastlines.txt does not name it.
		(CCountry britain, _) = Coasts(new[] { 1, 2 });
		Assert.Equal(CFixedPoint.Zero, britain.Modifier["global_money"]);
	}

	[Fact]
	public void NoCoastlinesNoLinkBySea()
	{
		(CCountry britain, _) = Coasts(System.Array.Empty<int>());
		Assert.Equal(CFixedPoint.Zero, britain.Modifier["global_money"]);
	}

	[Fact]
	public void ANavalBaseCountsForWhoeverControlsIt()
	{
		// The British base held by France: France has a base on the coast, the United Kingdom none.
		(CCountry britain, _) = Coasts(new[] { 3 }, britainsBaseHolder: "FRA");
		Assert.Equal(CFixedPoint.Zero, britain.Modifier["global_money"]);
	}

	[Fact]
	public void ACoastWithNoNavalBaseBuiltLinksNothing()
	{
		(CCountry britain, _) = Coasts(new[] { 3 }, southAfricasBase: 0);
		Assert.Equal(CFixedPoint.Zero, britain.Modifier["global_money"]);
	}

	[Fact]
	public void ACountryInNoFactionSharesWithNobody()
	{
		CCountry holland = Country("HOL", 1);
		CCountry america = Country("USA", 2, major: true);
		CGameState state = State(new[] { holland, america }, new[] { Land(1, "HOL", "gold"), Land(2, "USA") });
		RunADay(state);

		Assert.Equal(CFixedPoint.Zero, america.Modifier["global_money"]);
	}

	// --- the rest of what a country's modifier takes in ---

	[Fact]
	public void ACountrysModifierTakesInEveryProvinceItControls()
	{
		CCountry germany = Country("GER", 1);
		CCountry poland = Country("POL", 3);
		CProvince taken = Land(2, "POL", industry: 3);
		taken.Controller = germany.Tag;
		CGameState state = State(new[] { germany, poland }, new[] { Land(1, "GER", industry: 4), taken, Land(3, "POL", industry: 2) });
		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.FromInt(12), germany.Modifier["ic"]);          // with the five every country has
		Assert.Equal(CFixedPoint.FromInt(7), poland.Modifier["ic"]);
		Assert.Equal(CFixedPoint.One, germany.Modifier["infrastructure"]);   // two provinces at 0.5
	}

	[Fact]
	public void ACountryHasTheModifierForWarOrTheOneForPeace()
	{
		CCountry italy = Country("ITA", 1);
		CCountry ethiopia = Country("ETH", 2);
		CCountry sweden = Country("SWE", 3);
		var war = new CWar("a war", new CDate(1935, 10, 3));
		war.Attackers.Add(italy.Tag);
		war.Defenders.Add(ethiopia.Tag);
		CGameState state = State(new[] { italy, ethiopia, sweden }, new[] { Land(1, "ITA"), Land(2, "ETH"), Land(3, "SWE") }, war);
		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("0.05"), italy.Modifier["org_regain"]);
		Assert.Equal(CFixedPoint.Zero, italy.Modifier["max_war_exhaustion"]);
		Assert.Equal(CFixedPoint.Zero, sweden.Modifier["org_regain"]);
		Assert.Equal(CFixedPoint.FromInt(100), sweden.Modifier["max_war_exhaustion"]);
	}

	[Fact]
	public void EachPointOfDissentCostsWhatTheFileSays()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 10), Land(2, "GER", industry: 10) });
		germany.Dissent = CFixedPoint.Parse("7.5");
		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("-0.075"), germany.Modifier["global_ic"]);
		Assert.Equal(23, germany.TotalIc);                                   // 25 x 0.925, with the rest dropped
	}

	// --- what the modifier was summed from (section 8) ---

	[Fact]
	public void EverySourceIsKeptInTheOrderItWasAddedTheScaledOnesAsTheirCopies()
	{
		CCountry germany = Country("GER", 1);
		germany.Laws["economic_law"] = new CLaw("economic_law", Modifier("war_economy", ("global_ic", "0.1")));
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 10), Land(2, "GER", industry: 10) });
		germany.Dissent = CFixedPoint.Parse("7.5");
		CTick.RunEntryPass(state);

		var names = new List<string>();
		foreach (CModifierSource source in germany.ModifierSources)
		{
			names.Add(source.Heading ?? source.Modifier.Name);
		}
		Assert.Equal(new[] { "base_values", "war_economy", "P1", "P2", "peace", "dissent" }, names);

		CModifierSource dissent = germany.ModifierSources[^1];
		Assert.Equal(CFixedPoint.Parse("-0.075"), dissent.Modifier["global_ic"]);    // its file's -0.01, 7.5 times
		Assert.Equal(CFixedPoint.Parse("0.025"), germany.Modifier["global_ic"]);   // the law's and dissent's together
	}

	// --- ministers and what a country forgets ---

	[Fact]
	public void AMinistersDecayIsAddedForTheCategoryHisTypeNamesAndThenStoredOver()
	{
		var type = new CMinisterType(Modifier("naval_reformer"));
		type.Decay["naval_engineering"] = CFixedPoint.Parse("-0.25");
		var minister = new CMinister(1, "A Minister");
		minister.SetType("chief_of_navy", type);
		minister.SetType("chief_of_staff", new CMinisterType(Modifier("nobody_in_particular")));

		CCountry germany = Country("GER", 1);
		germany.Abilities["naval_engineering"] = CFixedPoint.FromInt(10);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER") });

		// In the seat he was not declared a reformer for, he adds nothing.
		germany.Government["chief_of_staff"] = minister;
		Assert.Equal(0, CCountryModifiers.MinisterDecay(germany, "naval_engineering"));

		germany.Government.Clear();
		germany.Government["chief_of_navy"] = minister;
		Assert.Equal(-250, CCountryModifiers.MinisterDecay(germany, "naval_engineering"));

		// The rebuild stores the technologies' figure over what the ministers added - none here - so his decay does nothing.
		Assert.Equal(0, CResearch.DecayModifier(state, germany, "naval_engineering"));
		CResearch.DecayMonthly(state);
		Assert.Equal(CFixedPoint.Parse("9.75"), germany.Abilities["naval_engineering"]);
	}

	[Fact]
	public void TheMinistersComeAmongTheSourcesInTheirPostsOrderWhateverOrderTheyWereSeatedIn()
	{
		var posts = new[]
		{
			new OpenHOI3.Sim.Politics.CGovernmentPosition("head_of_state", 1, false),
			new OpenHOI3.Sim.Politics.CGovernmentPosition("armament_minister", 2, true),
			new OpenHOI3.Sim.Politics.CGovernmentPosition("chief_of_navy", 3, true),
		};
		CMinister Seated(int id, string post, string type)
		{
			var minister = new CMinister(id, "Minister " + id);
			minister.SetType(post, new CMinisterType(Modifier(type)));
			return minister;
		}

		// Seated the other way round, as a save's cabinet might seat them against the history's file.
		CCountry germany = Country("GER", 1);
		germany.Government["chief_of_navy"] = Seated(3, "chief_of_navy", "naval_type");
		germany.Government["armament_minister"] = Seated(2, "armament_minister", "armament_type");
		germany.Government["head_of_state"] = Seated(1, "head_of_state", "state_type");

		CCountryModifiers.Rebuild(germany, posts: posts);

		var names = new List<string>();
		foreach (CModifierSource source in germany.ModifierSources)
		{
			names.Add(source.Modifier.Name);
		}
		Assert.Equal(new[] { "state_type", "armament_type", "naval_type" }, names);
	}

	// --- the laws' order, a puppet's master's faction, a government in exile (section 4, steps 4, 15 and 17) ---

	/// <summary>A world with the Axis's corner, two law groups in laws.txt's order and the statics the steps ask for.</summary>
	private static CGameState Steps(params CCountry[] countries)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		for (int index = 0; index < countries.Length; index++)
		{
			all.Add(Land(index + 1, countries[index].Tag.Value));
		}
		var statics = new[]
		{
			Modifier("align_towards_axis", ("align_towards", "20")),
			Modifier("government_in_exile", ("global_ic", "-0.5")),
		};
		return new CGameState(new CDate(1936, 1, 10), all, countries, new[] { Infra, Industry, Ruin, NavalBase },
			staticModifiers: statics)
		{
			IdeologyGroups = new[] { new OpenHOI3.Sim.Diplomacy.CIdeologyGroup("fascism", 0, 0) { Faction = "axis", Influence = "align_towards_axis" } },
			LawGroups = new[] { new CLawGroup("civil_law", 0), new CLawGroup("economic_law", 1) },
		};
	}

	private static List<string> SourceNames(CCountry country)
	{
		var names = new List<string>();
		foreach (CModifierSource source in country.ModifierSources)
		{
			names.Add(source.Heading ?? source.Modifier.Name);
		}
		return names;
	}

	[Fact]
	public void TheLawsComeAmongTheSourcesInTheirGroupsOrderWhateverOrderTheHistoryGaveThem()
	{
		CCountry germany = Country("GER", 1);
		// Germany's history gives its economic law before its civil law; laws.txt has civil_law first.
		germany.Laws["economic_law"] = new CLaw("economic_law", Modifier("war_economy", ("global_ic", "0.1")));
		germany.Laws["civil_law"] = new CLaw("civil_law", Modifier("totalitarian_system", ("dissent", "-0.1")));
		CGameState state = Steps(germany);

		CCountryModifiers.RebuildOne(state, germany);

		Assert.Equal(new[] { "totalitarian_system", "war_economy" }, SourceNames(germany));
	}

	[Fact]
	public void APuppetInNoFactionTakesItsMastersFactionsInfluenceModifierAfterItsTriggeredModifiers()
	{
		CCountry germany = Country("GER", 1, faction: "axis");
		CCountry austria = Country("AUS", 2);
		austria.Overlord = germany.Tag;
		CGameState state = Steps(germany, austria);

		CCountryModifiers.RebuildOne(state, austria);

		Assert.Equal("align_towards_axis", SourceNames(austria)[^1]);
		Assert.Equal(CFixedPoint.FromInt(20), austria.Modifier["align_towards"]);
		Assert.Same(state.FindStaticModifier("align_towards_axis"), CCountryModifiers.SubjectInfluence(state, austria));
	}

	[Fact]
	public void APuppetTakesNothingFromItsMasterWhenEitherIsOutOfFactionsOrItHasTheModifierFromAnInfluence()
	{
		CCountry germany = Country("GER", 1);
		CCountry austria = Country("AUS", 2);
		austria.Overlord = germany.Tag;
		CGameState state = Steps(germany, austria);
		Assert.Null(CCountryModifiers.SubjectInfluence(state, austria));          // its master is in no faction

		germany.Faction = "axis";
		austria.Faction = "axis";
		Assert.Null(CCountryModifiers.SubjectInfluence(state, austria));          // it is in one itself

		austria.Faction = "";
		CModifier influence = state.FindStaticModifier("align_towards_axis")!;
		austria.EventModifiers.Add(CTimedModifier.Given(state, influence, 30));
		Assert.Null(CCountryModifiers.SubjectInfluence(state, austria));          // an influence has given it already
		CCountryModifiers.RebuildOne(state, austria);
		Assert.Equal(CFixedPoint.FromInt(20), austria.Modifier["align_towards"]); // once, not twice
	}

	[Fact]
	public void AnOverseasAreaWhosePortsAreAllBlockadedIsBlockadedButThePortSparesItself()
	{
		CCountry germany = Country("GER", 1);
		var tag = new CCountryTag("GER");
		var port = new CProvince(2, "P2", isSea: false) { Owner = tag, Controller = tag, Continent = "east", PortSea = 4 };
		port.Cores.Add(tag);
		port.SetBuildingMaxLevel(Infra, CFixedPoint.FromInt(5));
		port.SetBuildingLevel(Infra, CFixedPoint.FromInt(5));
		port.SetBuildingMaxLevel(NavalBase, CFixedPoint.One);
		port.SetBuildingLevel(NavalBase, CFixedPoint.One);
		CProvince inland = Land(3, "GER", continent: "east");
		inland.SetBuildingMaxLevel(Infra, CFixedPoint.FromInt(5));
		port.Edges = new[]
		{
			new CProvinceEdge(3, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero), new CProvinceEdge(4, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero),
		};
		inland.Edges = new[] { new CProvinceEdge(2, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero) };
		var all = new List<CProvince> { new(0, "", isSea: false), Land(1, "GER"), port, inland, Sea(4) };
		var state = new CGameState(new CDate(1936, 1, 10), all, new[] { germany }, new[] { Infra, Industry, Ruin, NavalBase },
			staticModifiers: new[] { Modifier("overseas", ("local_manpower_modifier", "-0.75")), Modifier("blockaded", ("local_ic", "-0.5")) });

		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Zero, inland.Modifier["local_ic"]);          // the area has an open port

		port.Blockaded = true;
		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Parse("-0.5"), inland.Modifier["local_ic"]); // its one port is blockaded
		// The port spares itself step 2's, and has step 8's for its own blockade.
		Assert.Equal(CFixedPoint.Parse("-0.5"), port.Modifier["local_ic"]);
		Assert.Single(port.ModifierSources, source => source.Modifier.Name == "blockaded");
	}

	[Fact]
	public void AGovernmentInExileHasItsStaticModifier()
	{
		CCountry poland = Country("POL", 1);
		CGameState state = Steps(poland);
		CCountryModifiers.RebuildOne(state, poland);
		Assert.DoesNotContain("government_in_exile", SourceNames(poland));

		poland.InExile = true;
		CCountryModifiers.RebuildOne(state, poland);
		Assert.Equal("government_in_exile", SourceNames(poland)[^1]);
		Assert.Equal(CFixedPoint.Parse("-0.5"), poland.Modifier["global_ic"]);
	}
}
