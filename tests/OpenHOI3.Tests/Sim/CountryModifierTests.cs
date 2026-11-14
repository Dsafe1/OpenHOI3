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
		return new CGameState(new CDate(1936, 1, 10), all, countries, new[] { Infra, Industry, Ruin },
			staticModifiers: statics, defines: defines, wars: wars, strategicResources: resources,
			technologyCategories: categories);
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
		// The original's one routine for the figure only raises it, and nothing was found
		// that takes it down again. Likely, not confirmed: modifiers.md, section 6.
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
	public void AMinisterAddsToTheDecayOfTheCategoryHisTypeNames()
	{
		var type = new CMinisterType(Modifier("naval_reformer"));
		type.Decay["naval_engineering"] = CFixedPoint.Parse("-0.25");
		var minister = new CMinister(1, "A Minister");
		minister.SetType("chief_of_navy", type);
		minister.SetType("chief_of_staff", new CMinisterType(Modifier("nobody_in_particular")));

		CCountry germany = Country("GER", 1);
		germany.Abilities["naval_engineering"] = CFixedPoint.FromInt(10);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER") });

		// In the seat he was not declared a reformer for, he changes nothing.
		germany.Government["chief_of_staff"] = minister;
		Assert.Equal(0, CResearch.DecayModifier(state, germany, "naval_engineering"));

		germany.Government.Clear();
		germany.Government["chief_of_navy"] = minister;
		Assert.Equal(-250, CResearch.DecayModifier(state, germany, "naval_engineering"));

		// 10 x (1 - 0.025 x 0.75), with what does not fit dropped at each step
		CResearch.DecayMonthly(state);
		Assert.Equal(CFixedPoint.Parse("9.82"), germany.Abilities["naval_engineering"]);
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
}
