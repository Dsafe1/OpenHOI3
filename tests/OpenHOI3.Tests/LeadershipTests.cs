using System.Collections.Generic;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>
/// A country's leadership and its four shares, against
/// <c>docs/mechanics/leadership.md</c>, and the two province modifiers that came with
/// them. The figures for East Germany are the original's own, out of its savegame.
/// </summary>
public class LeadershipTests
{
	private static readonly CBuilding Industry = new(0, "industry", 10, "ic", CFixedPoint.One);

	private static CModifier Modifier(string name, params (string Kind, string Value)[] values)
	{
		var modifier = new CModifier(name);
		foreach ((string kind, string value) in values)
		{
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return modifier;
	}

	/// <summary>A country whose one law holds the given modifier values.</summary>
	private static CCountry Country(string tag, int capital, params (string Kind, string Value)[] law)
	{
		var country = new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "", Capital = capital };
		if (law.Length > 0)
		{
			country.Laws["education_investment_law"] = new CLaw("education_investment_law", Modifier("a_law", law));
		}
		return country;
	}

	/// <summary>Land its holder owns, controls and has a claim to, unless told otherwise.</summary>
	private static CProvince Land(int id, string holder, string leadership = "0", string continent = "west",
		bool core = true, params int[] neighbours)
	{
		var tag = new CCountryTag(holder);
		var province = new CProvince(id, $"P{id}", isSea: false)
		{
			Owner = tag,
			Controller = tag,
			Continent = continent,
			Neighbours = neighbours,
			Leadership = CFixedPoint.Parse(leadership),
		};
		if (core)
		{
			province.Cores.Add(tag);
		}
		return province;
	}

	/// <summary>
	/// The original's own numbers: 3.5 leadership for every country from
	/// <c>base_values</c>, a tenth of it left overseas, a fifth on land that is not core.
	/// </summary>
	private static CGameState State(CCountry[] countries, params CProvince[] provinces)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		all.AddRange(provinces);

		var statics = new[]
		{
			Modifier("base_values", ("global_leadership", "3.5"), ("ic", "5")),
			Modifier("overseas", ("local_leadership_modifier", "-0.9"), ("local_manpower_modifier", "-0.75")),
			Modifier("non_core", ("local_leadership_modifier", "-0.8"), ("local_ic", "-0.5"), ("local_resources", "-0.5")),
		};
		var defines = new CDefines();
		defines.Set("economy", "IC_TO_MONEY", CFixedPoint.Parse("0.05"));
		defines.Set("economy", "IC_TO_SUPPLIES", CFixedPoint.FromInt(7));
		defines.Set("economy", "LEADERSHIP_TO_OFFICERS", CFixedPoint.FromInt(4));
		defines.Set("economy", "LEADERSHIP_TO_DIPLOMACY", CFixedPoint.Parse("0.5"));
		defines.Set("economy", "LEADERSHIP_TO_SPIES", CFixedPoint.Parse("0.15"));
		var technologies = new[] { new CTechnology("education", 12, Modifier("education", ("leadership_gain", "0.05"))) };
		var resources = new[] { Modifier("gold", ("global_money", "0.33")) };
		return new CGameState(new CDate(1936, 1, 1), all, countries, new[] { Industry }, technologies,
			statics, defines, strategicResources: resources);
	}

	private static void Refresh(CGameState state)
	{
		CTick.RunEntryPass(state);
	}

	private static void RunADay(CGameState state)
	{
		new CSimulation(state).RunHours(24);
	}

	// --- how much there is ---

	[Fact]
	public void ACountryThatHoldsNothingHasWhatEveryCountryIsGivenScaledByItsLaws()
	{
		// East Germany in 1936: no provinces, and a law worth a fifth more.
		CCountry eastGermany = Country("DDR", 0, ("global_leadership_modifier", "0.2"));
		CGameState state = State(new[] { eastGermany });
		Refresh(state);

		Assert.Equal(CFixedPoint.Parse("4.2"), eastGermany.TotalLeadership);
	}

	[Fact]
	public void ProvincesAddWhatTheyGiveBeforeTheLawsScaleTheSum()
	{
		CCountry germany = Country("GER", 1, ("global_leadership_modifier", "0.1"));
		CGameState state = State(new[] { germany }, Land(1, "GER", "1.5", neighbours: 2), Land(2, "GER", "0.5", neighbours: 1));
		Refresh(state);

		// (1.5 + 0.5 + 3.5) x 1.1
		Assert.Equal(CFixedPoint.Parse("6.05"), germany.TotalLeadership);
	}

	[Fact]
	public void TechnologyAddsToTheLawsBonusRatherThanMultiplyingIt()
	{
		CCountry germany = Country("GER", 1, ("global_leadership_modifier", "0.1"));
		germany.TechnologyLevels["education"] = 2;
		CGameState state = State(new[] { germany }, Land(1, "GER", "6.5"));
		Refresh(state);

		// 10 x (1 + 0.1 + 2 x 0.05), not 10 x 1.1 x 1.1
		Assert.Equal(CFixedPoint.FromInt(12), germany.TotalLeadership);
	}

	[Fact]
	public void NoCountryHasLessThanOne()
	{
		CCountry tiny = Country("LUX", 0, ("global_leadership_modifier", "-0.9"));
		CGameState state = State(new[] { tiny });
		Refresh(state);

		Assert.Equal(CFixedPoint.One, tiny.TotalLeadership);   // 0.35 as worked out
	}

	[Fact]
	public void ItIsWhoeverControlsAProvinceThatItLeads()
	{
		CCountry germany = Country("GER", 1);
		CCountry france = Country("FRA", 2);
		CProvince taken = Land(2, "FRA", "2");
		taken.Controller = new CCountryTag("GER");
		CGameState state = State(new[] { germany, france }, Land(1, "GER", "1", neighbours: 2), taken);
		Refresh(state);

		Assert.Equal(CFixedPoint.Parse("6.5"), germany.TotalLeadership);
		Assert.Equal(CFixedPoint.Parse("3.5"), france.TotalLeadership);
	}

	// --- the two province modifiers ---

	[Fact]
	public void LandOnAnotherContinentThatCannotBeWalkedToIsOverseas()
	{
		CCountry britain = Country("ENG", 1);
		CGameState state = State(new[] { britain },
			Land(1, "ENG", "2"),
			Land(2, "ENG", "1", continent: "east"));
		Refresh(state);

		Assert.Equal(CFixedPoint.Parse("-0.9"), state.Province(2).Modifier["local_leadership_modifier"]);
		Assert.Equal(CFixedPoint.Zero, state.Province(1).Modifier["local_leadership_modifier"]);
		Assert.Equal(CFixedPoint.Parse("5.6"), britain.TotalLeadership);   // 2 + a tenth of 1 + 3.5
	}

	[Fact]
	public void LandOnAnotherContinentIsNotOverseasWhenItIsJoinedToTheCapital()
	{
		CCountry russia = Country("SOV", 1);
		CGameState state = State(new[] { russia },
			Land(1, "SOV", "2", neighbours: 2),
			Land(2, "SOV", "1", continent: "east", neighbours: 1));
		Refresh(state);

		Assert.Equal(CFixedPoint.Zero, state.Province(2).Modifier["local_leadership_modifier"]);
		Assert.Equal(CFixedPoint.Parse("6.5"), russia.TotalLeadership);
	}

	[Fact]
	public void AnIslandOnTheCapitalsOwnContinentIsNotOverseas()
	{
		CCountry italy = Country("ITA", 1);
		CGameState state = State(new[] { italy }, Land(1, "ITA", "2"), Land(2, "ITA", "1"));
		Refresh(state);

		Assert.Equal(CFixedPoint.Zero, state.Province(2).Modifier["local_leadership_modifier"]);
	}

	[Fact]
	public void LandItsOwnerHasNoClaimToIsWorthLess()
	{
		CCountry poland = Country("POL", 1);
		CProvince unclaimed = Land(2, "POL", "1", core: false, neighbours: 1);
		unclaimed.SetBuildingLevel(Industry, CFixedPoint.FromInt(4));
		unclaimed.CurrentProducing[EGoods.Metal] = CFixedPoint.FromInt(10);
		CGameState state = State(new[] { poland }, Land(1, "POL", "1", neighbours: 2), unclaimed);
		Refresh(state);

		Assert.Equal(CFixedPoint.Parse("4.7"), poland.TotalLeadership);    // 1 + a fifth of 1 + 3.5
		Assert.Equal(7, poland.BaseIc);                                    // half of four, and the five
		Assert.Equal(CFixedPoint.FromInt(10), poland.HomeProduced[EGoods.Metal]);   // half of ten, and five for the base
	}

	[Fact]
	public void LandSomebodyElseHoldsIsNotCountedAsUnclaimed()
	{
		CCountry germany = Country("GER", 1);
		CCountry france = Country("FRA", 0);
		CProvince taken = Land(2, "FRA", "1", core: false, neighbours: 1);
		taken.Controller = new CCountryTag("GER");
		CGameState state = State(new[] { germany, france }, Land(1, "GER", "1", neighbours: 2), taken);
		Refresh(state);

		// The occupier's policy is what would apply, and there is none yet.
		Assert.Equal(CFixedPoint.Zero, taken.Modifier["local_leadership_modifier"]);
	}

	// --- strategic resources ---

	[Fact]
	public void AStrategicResourceIsWorthItsModifierToWhoeverControlsTheProvince()
	{
		CCountry holland = Country("HOL", 1);
		CCountry japan = Country("JAP", 3);
		CProvince goldfield = Land(2, "HOL", continent: "east");
		goldfield.StrategicResource = "gold";
		CGameState state = State(new[] { holland, japan }, Land(1, "HOL"), goldfield, Land(3, "JAP"));
		Refresh(state);

		Assert.Equal(CFixedPoint.Parse("0.33"), holland.Modifier["global_money"]);
		Assert.Equal(CFixedPoint.Zero, japan.Modifier["global_money"]);

		goldfield.Controller = japan.Tag;
		Refresh(state);

		Assert.Equal(CFixedPoint.Zero, holland.Modifier["global_money"]);
		Assert.Equal(CFixedPoint.Parse("0.33"), japan.Modifier["global_money"]);
	}

	[Fact]
	public void TwoProvincesOfOneResourceCountOnce()
	{
		CCountry southAfrica = Country("SAF", 1);
		CProvince first = Land(1, "SAF");
		CProvince second = Land(2, "SAF");
		first.StrategicResource = "gold";
		second.StrategicResource = "gold";
		CGameState state = State(new[] { southAfrica }, first, second);
		Refresh(state);

		Assert.Equal(CFixedPoint.Parse("0.33"), southAfrica.Modifier["global_money"]);
	}

	// --- the four shares ---

	[Fact]
	public void ANewGameSplitsLeadershipInQuarters()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER"));
		CLeadership.SetAtStart(state);

		Assert.All(germany.LeadershipShares, share => Assert.Equal(8192, share.Raw));
	}

	[Fact]
	public void ANewGameGivesAPointOfInfluenceForEveryFiveBaseIcInWholePoints()
	{
		// Germany in 1936 has a base of 144 and starts with 28; a country with only the
		// five every country has starts with 1.
		CCountry germany = Country("GER", 1, ("global_ic", "0.5"));
		CCountry eastGermany = Country("DDR", 0);
		CProvince[] ruhr = new CProvince[14];
		for (int i = 0; i < ruhr.Length; i++)
		{
			ruhr[i] = Land(i + 1, "GER");
			ruhr[i].SetBuildingLevel(Industry, CFixedPoint.FromInt(i == 0 ? 9 : 10));
		}
		CGameState state = State(new[] { germany, eastGermany }, ruhr);
		CLeadership.SetAtStart(state);

		Assert.Equal(144, germany.BaseIc);
		Assert.Equal(CFixedPoint.FromInt(28), germany.DiplomaticInfluence);   // the law's half more IC is not counted
		Assert.Equal(CFixedPoint.One, eastGermany.DiplomaticInfluence);
	}

	[Fact]
	public void AQuarterOfEastGermanysLeadershipMakesWhatTheSavegameSays()
	{
		CCountry eastGermany = Country("DDR", 0, ("global_leadership_modifier", "0.2"), ("officer_recruitment", "-0.5"));
		CGameState state = State(new[] { eastGermany });
		CLeadership.SetAtStart(state);
		Refresh(state);

		// Two days of each is what the original's save holds: 4.198, 1 + 1.048 and 0.314.
		Assert.Equal(CFixedPoint.Parse("2.099"), CLeadership.OfficersMade(state, eastGermany));
		Assert.Equal(CFixedPoint.Parse("0.524"), CLeadership.InfluenceMade(state, eastGermany));
		Assert.Equal(CFixedPoint.Parse("0.157"), CLeadership.SpiesMade(state, eastGermany));
	}

	[Fact]
	public void TheConscriptionLawScalesTheOfficersAndNothingElse()
	{
		CCountry plain = Country("AAA", 0, ("global_leadership_modifier", "0.2"));
		CCountry drafted = Country("BBB", 0, ("global_leadership_modifier", "0.2"), ("officer_recruitment", "-0.25"));
		CGameState state = State(new[] { plain, drafted });
		CLeadership.SetAtStart(state);
		Refresh(state);

		Assert.Equal(CFixedPoint.Parse("4.199"), CLeadership.OfficersMade(state, plain));
		Assert.Equal(CFixedPoint.Parse("3.149"), CLeadership.OfficersMade(state, drafted));   // Nationalist Spain's
		Assert.Equal(CLeadership.InfluenceMade(state, plain), CLeadership.InfluenceMade(state, drafted));
		Assert.Equal(CLeadership.SpiesMade(state, plain), CLeadership.SpiesMade(state, drafted));
	}

	[Fact]
	public void ADayAddsWhatTheSharesMake()
	{
		CCountry eastGermany = Country("DDR", 0, ("global_leadership_modifier", "0.2"), ("officer_recruitment", "-0.5"));
		CGameState state = State(new[] { eastGermany });
		CLeadership.SetAtStart(state);
		RunADay(state);
		RunADay(state);

		Assert.Equal(CFixedPoint.Parse("4.198"), eastGermany.Officers);
		Assert.Equal(CFixedPoint.Parse("2.048"), eastGermany.DiplomaticInfluence);
		Assert.Equal(CFixedPoint.Parse("0.314"), eastGermany.SpiesInTraining);
	}

	[Fact]
	public void TheRebelsLeadNothing()
	{
		CCountry rebels = Country("REB", 0);
		CGameState state = State(new[] { rebels });
		CLeadership.SetAtStart(state);
		RunADay(state);

		Assert.Equal(CFixedPoint.Zero, rebels.Officers);
		Assert.Equal(CFixedPoint.One, rebels.DiplomaticInfluence);         // what it started with
		Assert.Equal(CFixedPoint.Zero, rebels.SpiesInTraining);
	}

	[Fact]
	public void AShareOfNothingMakesNothing()
	{
		CCountry germany = Country("GER", 0);
		CGameState state = State(new[] { germany });
		germany.LeadershipShares[(int)ELeadershipCategory.Research] = CFixed15.One;
		Refresh(state);

		Assert.Equal(CFixedPoint.Zero, CLeadership.OfficersMade(state, germany));
		Assert.Equal(CFixedPoint.Zero, CLeadership.InfluenceMade(state, germany));
		Assert.Equal(CFixedPoint.Zero, CLeadership.SpiesMade(state, germany));
	}

	[Theory]
	[InlineData("6.5", 2)]      // a quarter of 10 is 2.5, which goes to the even 2
	[InlineData("10.5", 4)]     // 3.5 goes to the even 4
	[InlineData("6.6", 3)]      // 2.525
	[InlineData("5.4", 2)]      // 2.225
	[InlineData("0", 1)]        // 0.875
	public void ResearchProjectsAreTheShareOfLeadershipToTheNearestWholeAndToTheEvenOneOnATie(string inProvinces, int projects)
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", inProvinces));
		CLeadership.SetAtStart(state);
		Refresh(state);

		Assert.Equal(projects, CLeadership.ResearchSlots(germany));
	}

	// --- setting the shares ---

	[Fact]
	public void TheSharesAreChangedByACommand()
	{
		CCountry germany = Country("GER", 0);
		CGameState state = State(new[] { germany });
		CLeadership.SetAtStart(state);
		var simulation = new CSimulation(state);
		var wanted = new[] { new CFixed15(16384), new CFixed15(0), new CFixed15(0), new CFixed15(16384) };
		simulation.Post(new CSetLeadershipSharesCommand(germany.Tag, wanted));
		simulation.RunHours(1);

		Assert.Equal(wanted, germany.LeadershipShares);
	}

	[Fact]
	public void SharesThatAddUpToMoreThanAllOfItAreRefused()
	{
		CCountry germany = Country("GER", 0);
		CGameState state = State(new[] { germany });
		CLeadership.SetAtStart(state);
		var simulation = new CSimulation(state);
		var tooMuch = new[] { new CFixed15(16384), new CFixed15(16384), new CFixed15(16384), new CFixed15(1) };
		var negative = new[] { new CFixed15(-1), new CFixed15(16384), new CFixed15(0), new CFixed15(0) };
		var three = new[] { new CFixed15(8192), new CFixed15(8192), new CFixed15(8192) };
		simulation.Post(new CSetLeadershipSharesCommand(germany.Tag, tooMuch));
		simulation.Post(new CSetLeadershipSharesCommand(germany.Tag, negative));
		simulation.Post(new CSetLeadershipSharesCommand(germany.Tag, three));
		simulation.Post(new CSetLeadershipSharesCommand(new CCountryTag("XXX"), three));
		simulation.RunHours(1);

		Assert.All(germany.LeadershipShares, share => Assert.Equal(8192, share.Raw));
	}
}
