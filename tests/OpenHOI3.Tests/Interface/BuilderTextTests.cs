using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the windows a unit is ordered from write, against
/// <c>docs/mechanics/builders.md</c>, sections 3, 4 and 6.
/// </summary>
public class BuilderTextTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static string Localise(string key) => key switch { "DAYS" => "Days", "MONTHS" => "Months", _ => key };

	private static CSubUnit Brigade(string key, params (string Statistic, string Value)[] figures)
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		foreach ((string statistic, string value) in figures)
		{
			type.Stats.Values.Set(statistic, CFixedPoint.Parse(value));
		}
		return new CSubUnit(type);
	}

	private static CCountry Germany()
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		country.TotalIc = 100;
		country.ProductionShares[(int)EProductionCategory.Production] = CFixed15.FromThousandths(CFixedPoint.Parse("0.25"));
		return country;
	}

	// ---- numbers

	[Fact]
	public void AStrengthIsWrittenAsMenAHundredToThePoint()
	{
		Assert.Equal("900", CBuilderText.Men(CFixedPoint.FromInt(9)));
		Assert.Equal("3,000", CBuilderText.Men(CFixedPoint.FromInt(30)));
		Assert.Equal("7,000", CBuilderText.Men(CFixedPoint.FromInt(70)));
		Assert.Equal("12,050", CBuilderText.Men(CFixedPoint.Parse("120.5")));
		Assert.Equal("150K", CBuilderText.Men(CFixedPoint.FromInt(1500)));
		Assert.Equal("2.50M", CBuilderText.Men(CFixedPoint.FromInt(25000)));
	}

	[Fact]
	public void AListWritesThousandsWithALetterCutAndNotRounded()
	{
		Assert.Equal("999", CBuilderText.WithSuffix(CFixedPoint.FromInt(999)));
		Assert.Equal("1k", CBuilderText.WithSuffix(CFixedPoint.FromInt(1999)));
		Assert.Equal("3k", CBuilderText.WithSuffix(CFixedPoint.FromInt(3000)));
		Assert.Equal("2M", CBuilderText.WithSuffix(CFixedPoint.FromInt(2500000)));
	}

	[Fact]
	public void ASpeedIsWholeInAListUnlessItIsSlowAndInADesignUnlessItHasAFraction()
	{
		Assert.Equal("4", CBuilderText.ListSpeed(CFixedPoint.FromInt(4)));
		Assert.Equal("4", CBuilderText.ListSpeed(CFixedPoint.Parse("4.33")));
		Assert.Equal("1.50", CBuilderText.ListSpeed(CFixedPoint.Parse("1.5")));
		Assert.Equal("1", CBuilderText.ListSpeed(CFixedPoint.Parse("1.05")));           // too small a fraction to show

		Assert.Equal("4", CBuilderText.ExactSpeed(CFixedPoint.FromInt(4)));
		Assert.Equal("4.33", CBuilderText.ExactSpeed(CFixedPoint.Parse("4.33")));
	}

	[Fact]
	public void TheSecondWayOfWritingANumberDropsWhatItDoesNotNeed()
	{
		Assert.Equal("2", CNumberText.Trimmed(CFixedPoint.FromInt(2)));
		Assert.Equal("0.5", CNumberText.Trimmed(CFixedPoint.Parse("0.5")));
		Assert.Equal("1.25", CNumberText.Trimmed(CFixedPoint.Parse("1.25")));
		Assert.Equal("0.125", CNumberText.Trimmed(CFixedPoint.Parse("0.125")));
		Assert.Equal("0", CNumberText.Trimmed(CFixedPoint.Zero));
	}

	// ---- the order panel

	[Fact]
	public void IcIsForEachLineMenForEveryUnitAndDaysForOneLine()
	{
		Assert.Equal("16.06", CBuilderText.IcCost(CFixedPoint.Parse("8.034"), 2));
		Assert.Equal("47.94", CBuilderText.ManpowerCost(CFixedPoint.Parse("7.99"), 2, 3));
		Assert.Equal("95 Days", CBuilderText.BuildTime(Localise, CFixedPoint.FromInt(95), 1));
		Assert.Equal("365 Days", CBuilderText.BuildTime(Localise, CFixedPoint.FromInt(365), 1));
		Assert.Equal("12 Months", CBuilderText.BuildTime(Localise, CFixedPoint.FromInt(95), 4));       // 380 days
		Assert.Equal("0 Days", CBuilderText.BuildTime(Localise, CFixedPoint.Zero, 1));
	}

	[Fact]
	public void ACountStepsByOneOrTenAndNeverUnderOne()
	{
		Assert.Equal(2, CBuilderText.Stepped(1, 1, false));
		Assert.Equal(15, CBuilderText.Stepped(5, 1, true));
		Assert.Equal(1, CBuilderText.Stepped(1, -1, false));
		Assert.Equal(1, CBuilderText.Stepped(5, -1, true));
		Assert.False(CBuilderText.MayStepDown(1));
		Assert.True(CBuilderText.MayStepDown(2));
		Assert.True(CBuilderText.MayStepUp(98));
		Assert.False(CBuilderText.MayStepUp(99));
	}

	[Fact]
	public void TheLightIsGreenWithIcToSpareOrangeWithSomeAndRedWithNone()
	{
		// Twenty-five IC for production, seventeen of it taken by what is queued.
		CCountry country = Germany();
		country.Constructions.Add(new CConstruction(Ger) { Cost = CFixedPoint.FromInt(17) });

		Assert.Equal(EBuilderLight.Green, CBuilderText.Light(country, CFixedPoint.FromInt(8)));
		Assert.Equal(EBuilderLight.Orange, CBuilderText.Light(country, CFixedPoint.Parse("8.001")));
		country.Constructions.Add(new CConstruction(Ger) { Cost = CFixedPoint.FromInt(8) });
		Assert.Equal(EBuilderLight.Green, CBuilderText.Light(country, CFixedPoint.Zero));
		Assert.Equal(EBuilderLight.Red, CBuilderText.Light(country, CFixedPoint.Parse("0.001")));
	}

	// ---- the lists

	[Fact]
	public void AColumnWritesItsFigureTheWayItsKindDoes()
	{
		CCountry country = Germany();
		CSubUnit infantry = Brigade("infantry_brigade", ("max_strength", "30"), ("default_organisation", "40"),
			("softness", "1"), ("maximum_speed", "4"), ("supply_consumption", "0.67"), ("build_cost_ic", "1.97"),
			("build_cost_manpower", "3.33"), ("build_time", "90"), ("officers", "100"));
		string Of(string element) =>
			CBuilderLists.Text(CBuilderLists.Land.Single(column => column.Element == element), country, infantry, 0);

		Assert.Equal("3k", Of("str"));
		Assert.Equal("40", Of("org"));
		Assert.Equal("100%", Of("sof"));
		Assert.Equal("4", Of("speed"));
		Assert.Equal("0.67", Of("sc"));
		Assert.Equal("1.97", Of("ic"));
		Assert.Equal("3.33", Of("mp"));
		Assert.Equal("90", Of("time"));
		Assert.Equal("100", Of("off"));

		CSubUnit ship = Brigade("destroyer", ("positioning", "0.05"), ("distance", "0.14"), ("hull", "1.05"));
		string OfShip(string element) =>
			CBuilderLists.Text(CBuilderLists.Ship.Single(column => column.Element == element), country, ship, 0);
		Assert.Equal("5.0%", OfShip("pos"));
		Assert.Equal("14", OfShip("distance"));
		Assert.Equal("1.05", OfShip("hull"));
	}

	[Fact]
	public void ASortButtonIsItsColumnsAndPriorityHasOneOfItsOwn()
	{
		Assert.Equal("soft_attack", CBuilderLists.SortStatistic(CBuilderLists.Land, "sort_sa"));
		Assert.Equal("ap_attack", CBuilderLists.SortStatistic(CBuilderLists.Land, "sort_piercing"));
		Assert.Equal("build_cost_ic", CBuilderLists.SortStatistic(CBuilderLists.Land, "sort_ic"));
		Assert.Equal("priority", CBuilderLists.SortStatistic(CBuilderLists.Ship, "sort_prio"));
		Assert.Equal("positioning", CBuilderLists.SortStatistic(CBuilderLists.Ship, "sort_pos"));
		Assert.Equal("strategic_attack", CBuilderLists.SortStatistic(CBuilderLists.Air, "sort_strat_a"));
		Assert.Null(CBuilderLists.SortStatistic(CBuilderLists.Air, "sort_hull"));
	}

	[Fact]
	public void ASortPutsTheLargestFirstOrTheSmallestAndLeavesEqualsAsTheyCame()
	{
		var brigades = new List<CSubUnit>
		{
			Brigade("a", ("priority", "7")), Brigade("b", ("priority", "10")), Brigade("c", ("priority", "7")),
			Brigade("d", ("priority", "3")), Brigade("e", ("priority", "10")),
		};
		string Keys(bool smallestFirst) =>
			string.Concat(CBuilderLists.Sorted(brigades, "priority", smallestFirst).Select(brigade => brigade.Type.Key));

		Assert.Equal("beacd", Keys(false));
		Assert.Equal("dacbe", Keys(true));
	}

	// ---- a design

	[Fact]
	public void ADesignAddsUpAveragesOrganisationAndSoftnessAndTakesTheSlowestAndTheBest()
	{
		// The original's infantry, infantry and artillery, near enough.
		var brigades = new List<CSubUnit>
		{
			Brigade("infantry_brigade", ("max_strength", "30"), ("default_organisation", "40"), ("softness", "1"),
				("maximum_speed", "4"), ("soft_attack", "2"), ("ap_attack", "1"), ("armor_value", "0"),
				("supply_consumption", "0.67"), ("combat_width", "1")),
			Brigade("infantry_brigade", ("max_strength", "30"), ("default_organisation", "40"), ("softness", "1"),
				("maximum_speed", "4"), ("soft_attack", "2"), ("ap_attack", "1"), ("armor_value", "0"),
				("supply_consumption", "0.67"), ("combat_width", "1")),
			Brigade("artillery_brigade", ("max_strength", "10"), ("default_organisation", "35"), ("softness", "0.9"),
				("maximum_speed", "3.5"), ("soft_attack", "4"), ("ap_attack", "3"), ("armor_value", "2"),
				("supply_consumption", "1"), ("combat_width", "0")),
		};
		var totals = new CDesignTotals(brigades);
		Dictionary<string, string> figures = totals.Figures().ToDictionary(figure => figure.Element, figure => figure.Text);

		Assert.Equal("3", figures["size_value"]);
		Assert.Equal("7,000", figures["str_value"]);
		Assert.Equal("38", figures["org_value"]);              // 115 over 3, cut
		Assert.Equal("2", figures["cw_value"]);
		Assert.Equal("8", figures["sa_value"]);
		Assert.Equal("2.34", figures["sc_value"]);
		Assert.Equal("96%", figures["sof_value"]);             // 2.9 over 3 is 0.966, cut
		Assert.Equal("3.50", figures["speed_value"]);          // the slowest
		Assert.Equal("3", figures["piercing_value"]);          // the best
		Assert.Equal("2", figures["armored_value"]);

		Assert.Equal("0", new CDesignTotals(new List<CSubUnit>()).Figures().First(figure => figure.Element == "speed_value").Text);
		Assert.Equal("5%", CDesignTotals.CombinedArms(CFixedPoint.Parse("0.05")));
		Assert.Equal("4", CDesignTotals.Allowed(4));
	}

	[Fact]
	public void WhatBuildingOneTeachesIsWrittenWithAPlus()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Stats.Values.Set(CBuildQueue.CompletionSize, CFixedPoint.Parse("0.2"));
		Assert.Equal("+0.2", CBuilderText.OnCompletion(type));
	}
}
