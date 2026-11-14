using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The top bar's tooltips, against <c>docs/mechanics/topbar.md</c>, section 14, with the
/// original's own strings as Their Finest Hour's tables give them.
/// </summary>
public class TopBarTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static string Localise(string key) => key switch
	{
		"ENERGY_IRO" => "Energy",
		"ENERGY_DRO" => "Energy production covers various sources of energy, but is mainly coal.",
		"RES_CHANGE" => "Changes by $VAL$ every day.\n",
		"RES_PROD" => "$VAL$ is produced in home territory.\n",
		"RES_CONVERTED" => "$VAL$ converted.\n",
		"RES_USED" => "$VAL$ used.\n",
		"RES_INTO_NET" => "$VAL$ into network.\n",
		"RES_INC_DEBT" => "$VAL$ income from debt.\n",
		"RES_FROM_PUPPETS" => "$VAL$ from puppets.\n",
		"RES_REPAID_AWAY" => "$VAL$ repaid.\n",
		"RES_SENT_TO_MASTER" => "$VAL$ sent to master.\n",
		"IC_NUMBER_IRO" => "Wasted IC: §Y$WASTED$§W. Base IC: §Y$BASE$§W. Available IC: §Y$AVAILABLE$§W.\n",
		"FROM_TECHNOLOGY" => "From Improved Technology",
		"DISSENTV_IRO" => "With the current investment in consumer goods, dissent will change by $CHANGE$ every day.",
		"DISSENTV_IRO3" => "Our current level of dissent gives these penalties.\n",
		"DIPDETAILS_IRO" => "Every day our pool of influence change by $DAILY$§W.\n§G+$PRODUCED$§W from leadership and §R-$UPKEEP$§W lost from influencing other nations.",
		"NO_CURR_EFFECTS_ON_NU" => "Currently nothing is affecting the national unity.",
		"STRMONTHLY_CHANGE" => "Monthly Change:",
		"OFFICER_NUMBER2_IRO" => "We have §Y$OFFICERS$§W officers covering §Y$DIVISIONS$§W brigades. We need §Y$NEED$§W. Every day we are educating §Y$DAILY$§W new officers.",
		"MANPOWER_DETAILS_IRO" => "Our army needs §Y$NEED$§W manpower to reinforce, and we use §Y$USED$§W every day.",
		"MANPOWER_DETAILS_DRO" => "Monthly gain: §Y$MONTHLY$§W\nBase: §Y$BASE$§W\n",
		"FREE_SPIES" => "Free Spies",
		"base_values" => "Base Value",
		"war_economy" => "War Economy",
		"MODIFIER_GLOBAL_IC" => "IC",
		_ => key,
	};

	private static CModifier Modifier(string name, params (string Kind, string Value)[] values)
	{
		var modifier = new CModifier(name);
		foreach ((string kind, string value) in values)
		{
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return modifier;
	}

	private static (CGameState State, CCountry Germany) State()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
		};
		var defines = new CDefines();
		defines.Set("economy", "LEADERSHIP_TO_DIPLOMACY", CFixedPoint.One);
		defines.Set("economy", "LEADERSHIP_TO_OFFICERS", CFixedPoint.FromInt(2));
		var statics = new[] { Modifier("dissent", ("global_ic", "-0.01")) };
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany }, staticModifiers: statics, defines: defines);
		return (state, germany);
	}

	private static CTooltip For(CGameState state, CCountry country, string element)
	{
		return CTopBarTooltips.For(Localise, state, country, element);
	}

	[Fact]
	public void A_picture_gives_its_heading_and_its_description_to_come()
	{
		(CGameState state, CCountry germany) = State();
		CTooltip tooltip = For(state, germany, "icon_energy_small");

		Assert.Equal("Energy", tooltip.Text);
		Assert.Equal("Energy production covers various sources of energy, but is mainly coal.", tooltip.Delayed);
	}

	[Fact]
	public void A_ledger_is_the_change_then_a_line_for_each_thing_that_moved_the_good()
	{
		(CGameState state, CCountry germany) = State();
		germany.HomeProduced[EGoods.Energy] = CFixedPoint.Parse("120.5");
		germany.ConversionUsed[EGoods.Energy] = CFixedPoint.FromInt(10);
		germany.Usage[EGoods.Energy] = CFixedPoint.FromInt(100);

		Assert.Equal(
			"Changes by §G+10.50§W every day.\n"
			+ "§G+120.50§W is produced in home territory.\n"
			+ "§R-10.00§W converted.\n"
			+ "§R-100.00§W used.\n"
			+ "\n",
			For(state, germany, "energy_number").Text);
	}

	[Fact]
	public void A_loss_on_the_day_is_red_and_into_the_network_is_both_of_our_pools()
	{
		(CGameState state, CCountry germany) = State();
		germany.UnitDrawn[EGoods.Supplies] = CFixedPoint.FromInt(3);
		germany.SentTo[EGoods.Supplies] = CFixedPoint.FromInt(2);

		Assert.Equal("Changes by §R-5.00§W every day.\n§R-5.00§W into network.\n\n", For(state, germany, "supplies_number").Text);
	}

	[Fact]
	public void Debt_and_tribute_have_lines_of_their_own_and_tribute_sent_is_green()
	{
		(CGameState state, CCountry germany) = State();
		germany.IncomeFromDebt = CFixedPoint.FromInt(4);
		germany.RepaidAway = CFixedPoint.FromInt(1);
		germany.TributeReceived[EGoods.Money] = CFixedPoint.FromInt(2);
		germany.TributeSent[EGoods.Money] = CFixedPoint.FromInt(-3);

		// In 4 + 2, out - 1 + 3: the repayment taken away from what goes out, as the original sums it.
		Assert.Equal(
			"Changes by §G+4.00§W every day.\n"
			+ "§G+4.00§W income from debt.\n"
			+ "§G+2.00§W from puppets.\n"
			+ "§R-1.00§W repaid.\n"
			+ "§G+3.00§W sent to master.\n"
			+ "\n",
			For(state, germany, "money_number").Text);
	}

	[Fact]
	public void What_exiles_hand_over_is_in_the_change_and_has_no_line_of_its_own()
	{
		(CGameState state, CCountry germany) = State();
		germany.ReceivedFromExiles[EGoods.Fuel] = CFixedPoint.FromInt(2);
		germany.Usage[EGoods.Fuel] = CFixedPoint.FromInt(1);

		// The original counts the pool in the day's change and writes no line for it.
		Assert.Equal("Changes by §G+1.00§W every day.\n§R-1.00§W used.\n\n", For(state, germany, "fuel_number").Text);
	}

	[Fact]
	public void A_government_in_exile_says_nothing_of_its_supplies_and_fuel()
	{
		(CGameState state, CCountry germany) = State();
		germany.InExile = true;

		Assert.Equal(CTooltip.Empty, For(state, germany, "supplies_number"));
		Assert.Equal(CTooltip.Empty, For(state, germany, "fuel_number"));
		Assert.NotEqual(CTooltip.Empty, For(state, germany, "money_number"));
	}

	[Fact]
	public void IC_gives_the_bars_three_figures_what_technology_adds_and_where_global_IC_comes_from()
	{
		(CGameState state, CCountry germany) = State();
		germany.TotalIc = 21;
		germany.MaxIc = 23;
		germany.Technology.Set("ic_modifier", CFixedPoint.Parse("0.12"));
		germany.ModifierSources.Add(new CModifierSource(Modifier("war_economy", ("global_ic", "0.1"))));

		Assert.Equal(
			"Wasted IC: §Y0§W. Base IC: §Y23§W. Available IC: §Y21§W.\n"
			+ "From Improved Technology: §G+12.00%§W\n"
			+ "War Economy: §G+10.0%§W",
			For(state, germany, "ic_number").Text);
	}

	[Fact]
	public void Dissent_says_which_way_it_goes_and_what_the_dissent_there_is_costs()
	{
		(CGameState state, CCountry germany) = State();
		germany.Dissent = CFixedPoint.FromInt(2);
		germany.ProductionShares[(int)EProductionCategory.ConsumerGoods] =
			CFixed15.FromThousandths(CProduction.ConsumerGoodsNeeded(state, germany));

		Assert.Equal(
			"With the current investment in consumer goods, dissent will change by §Y0.00§W every day.\n"
			+ "Our current level of dissent gives these penalties.\n"
			+ "IC: §R-2.0%§W",
			For(state, germany, "dissent_number").Text);
	}

	[Fact]
	public void Dissents_change_is_rounded_where_every_other_figure_is_cut()
	{
		var change = new CFixed15(CFixed15.Scale * 57 / 10000);    // 0.0057

		Assert.Equal("0.01", CTopBarTooltips.Rounded(change, 2));
	}

	[Fact]
	public void Diplomacy_is_what_leadership_makes_less_what_influencing_costs()
	{
		(CGameState state, CCountry germany) = State();
		germany.TotalLeadership = CFixedPoint.FromInt(10);
		germany.LeadershipShares[(int)ELeadershipCategory.Diplomacy] = new CFixed15(CFixed15.Scale / 2);

		Assert.Equal(
			"Every day our pool of influence change by §G+5.00§W.\n§G+5.00§W from leadership and §R-0.00§W lost from influencing other nations.",
			For(state, germany, "diplomacy_number").Text);
	}

	[Fact]
	public void National_unity_with_nothing_moving_it_says_so()
	{
		(CGameState state, CCountry germany) = State();
		germany.NationalUnity = CFixedPoint.FromInt(80);

		Assert.Equal("§Y80.000%§W\nCurrently nothing is affecting the national unity.", For(state, germany, "national_unity_number").Text);
	}

	[Fact]
	public void Officers_are_counted_and_a_day_trains_its_share_of_leadership_worth()
	{
		(CGameState state, CCountry germany) = State();
		germany.Officers = CFixedPoint.FromInt(1200);
		germany.OfficersNeeded = CFixedPoint.FromInt(1000);
		germany.TotalLeadership = CFixedPoint.FromInt(10);
		germany.LeadershipShares[(int)ELeadershipCategory.Officers] = new CFixed15(CFixed15.Scale / 4);
		germany.Modifier.Set("officer_recruitment", CFixedPoint.Parse("0.1"));

		// A quarter of 10 leadership, twice over, and a tenth more.
		Assert.Equal(
			"We have §Y1200§W officers covering §Y0§W brigades. We need §Y1000§W. Every day we are educating §Y5.5§W new officers.",
			For(state, germany, "leadership_number").Text);
	}

	[Fact]
	public void Manpower_tells_reinforcement_at_once_and_a_months_gain_and_the_base_to_come()
	{
		(CGameState state, CCountry germany) = State();
		germany.ReinforcementManpowerNeeded = CFixedPoint.Parse("12.345");
		germany.ReinforcementManpowerUsed = CFixedPoint.Parse("0.5");
		germany.MonthlyManpower = CFixedPoint.FromInt(20);
		germany.MaxManpower = CFixedPoint.FromInt(100);
		germany.Modifier.Set("global_manpower_modifier", CFixedPoint.Parse("0.1"));
		germany.ModifierSources.Add(new CModifierSource(Modifier("base_values", ("global_manpower", "4"))));

		CTooltip tooltip = For(state, germany, "manpower_number");
		Assert.Equal("Our army needs §Y12.3§W manpower to reinforce, and we use §Y0.5§W every day.", tooltip.Text);
		Assert.Equal("Monthly gain: §Y20.0§W\nBase: §Y45.455§W\nBase Value: §G+4.00§W", tooltip.Delayed);
	}

	[Fact]
	public void A_small_need_for_manpower_is_written_to_two_places()
	{
		(CGameState state, CCountry germany) = State();
		germany.ReinforcementManpowerNeeded = CFixedPoint.Parse("9.876");

		Assert.StartsWith("Our army needs §Y9.87§W", For(state, germany, "manpower_number").Text);
	}

	[Fact]
	public void Spies_say_only_free_spies_and_an_element_nothing_answers_for_says_nothing()
	{
		(CGameState state, CCountry germany) = State();

		Assert.Equal("Free Spies", For(state, germany, "espionage_number").Text);
		Assert.Same(CTooltip.Empty, For(state, germany, "DateText"));
		Assert.Same(CTooltip.Empty, For(state, germany, "topbarbutton_diplo"));
	}
}
