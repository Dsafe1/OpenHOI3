using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What a slider says in a tooltip, against <c>docs/mechanics/sliders.md</c>, section 6, with
/// the original's own strings as Their Finest Hour's tables give them.
/// </summary>
public class ShareTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static string Localise(string key) => key switch
	{
		"DCG_PRODUCTION" => "We can fund §Y$VAL$§W% of our production with this budget. We need §Y$NEED$§W IC allocated, and currently use §Y$USE$§W IC. ",
		"DCG_SUPPLIES" => "Funneling §Y$IC$§W% of IC into the production of supplies, we will get $VAL$ supplies produced every day.",
		"DCG_REINFORCE" => "With this budget, we can reinforce or repair §Y$VAL$§W% of our forces that need it.",
		"DCG_REINFORCE_BONUS" => "All reinforcements are multiplied by §G$VAL$§W from initial mobilization.",
		"DCG_UPGRADE" => "We need to devote §Y$IC$§W IC to upgrade the §Y$UNITS$§W units that require new equipment. ",
		"DCG_UPGRADE_OK" => "With our budget, this is more than enough.",
		"DCG_UPGRADE_BAD" => "We can only cover §R$VAL$§W% of our upgrades with the current budget.",
		"DCG_TOTAL_NEED" => "We need a total of §Y$PERC$%§W of our IC into consumer goods.",
		"DCG_TROOPS_GOODS" => "Our §Y$TROOPS$§W regular brigades and mobilized reserve brigades require that §Y$IC$%§W of IC be put into consumer goods when at peace.",
		"DCG_DISSENT" => "Availability of consumer goods affects dissent for $VAL$ every day.",
		"DCG_RESEARCH" => "We have §Y$COUNT$§W projects being researched, and with §Y$SPEED$§W leadership invested in it we can cover $PERCENT$ every day.",
		"DCG_ESPIONAGE" => "We will gain §Y$VAL$§W spies for our networks every day with this amount of leadership devoted to espionage.",
		"DCG_OFFICERS" => "With this budget, our leadership will provide §Y$VAL$§W officers each day.",
		"SLIDER_LOCKED_TOOLTIP" => "Locked, right click to unlock.",
		"SLIDER_UNLOCKED_TOOLTIP" => "Right click to lock.",
		"FROM_TECHNOLOGY" => "From Improved Technology",
		_ => key,
	};

	private static (CGameState State, CCountry Germany) State()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1, TotalIc = 100 };
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
		};
		var defines = new CDefines();
		defines.Set("economy", "IC_TO_SUPPLIES", CFixedPoint.FromInt(7));
		defines.Set("economy", "LEADERSHIP_TO_SPIES", CFixedPoint.Parse("0.2"));
		defines.Set("economy", "LEADERSHIP_TO_OFFICERS", CFixedPoint.FromInt(2));
		return (new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany }, defines: defines), germany);
	}

	private static CFixed15 Share(string fraction) => CFixed15.FromThousandths(CFixedPoint.Parse(fraction));

	[Fact]
	public void The_production_screen_divides_by_the_IC_and_rounds_to_a_32768th()
	{
		// 6.9 of 41 IC is 5514.73 32768ths.
		Assert.Equal(5515, CShareTooltips.ProductionShareAt(CFixedPoint.Parse("6.9"), 41).Raw);
	}

	[Fact]
	public void The_technology_screen_rounds_the_amount_first_and_then_divides_cut()
	{
		// 2.5 leadership is 81920 32768ths, over 7.3 leadership (239206 of them, cut).
		Assert.Equal(81920L * 32768 / 239206, CShareTooltips.LeadershipShareAt(CFixedPoint.Parse("2.5"), CFixedPoint.Parse("7.3")).Raw);
	}

	[Fact]
	public void The_knob_says_whether_a_right_click_locks_or_unlocks()
	{
		Assert.Equal("Locked, right click to unlock.", CShareTooltips.LockLine(Localise, locked: true));
		Assert.Equal("Right click to lock.", CShareTooltips.LockLine(Localise, locked: false));
	}

	[Fact]
	public void Lend_lease_says_nothing()
	{
		(CGameState state, CCountry germany) = State();
		Assert.Equal("", CShareTooltips.Production(Localise, state, germany, EProductionCategory.LendLease, Share("0.1")));
	}

	[Fact]
	public void Production_with_nothing_needed_is_a_hundred_times_the_IC_it_holds()
	{
		(CGameState state, CCountry germany) = State();
		string text = CShareTooltips.Production(Localise, state, germany, EProductionCategory.Production, Share("0.25"));
		Assert.StartsWith("We can fund §Y2500.00§W% of our production", text);
		Assert.Contains("We need §Y0.00§W IC allocated", text);
	}

	[Fact]
	public void Production_is_the_share_of_what_the_queue_needs()
	{
		(CGameState state, CCountry germany) = State();
		germany.ProductionCost = CFixedPoint.FromInt(50);
		germany.ProductionSpent = CFixedPoint.Parse("12.5");
		string text = CShareTooltips.Production(Localise, state, germany, EProductionCategory.Production, Share("0.25"));
		Assert.Equal("We can fund §Y50.00§W% of our production with this budget. We need §Y50.00§W IC allocated, and currently use §Y12.50§W IC. ", text);
	}

	[Fact]
	public void Supplies_run_straight_into_their_sources_and_end_in_the_technology()
	{
		(CGameState state, CCountry germany) = State();
		germany.Modifier.Set("global_supplies", CFixedPoint.Parse("0.1"));
		germany.ModifierSources.Add(new CModifierSource(Law("industrial_policy", ("global_supplies", "0.1"))));
		germany.Technology.Set("ic_to_supplies", CFixedPoint.Parse("0.05"));
		string text = CShareTooltips.Production(Localise, state, germany, EProductionCategory.Supplies, Share("0.1"));

		// A tenth of 100 IC, cut to 3276 32768ths, times 7 times 1.15, in the 48.15 kind, rounded to two places.
		Assert.StartsWith("Funneling §Y10.00§W% of IC into the production of supplies, we will get §G+80.48§W supplies produced every day.industrial_policy: ", text);
		Assert.EndsWith("\nFrom Improved Technology: §G+5.00%§W\n", text);
	}

	[Fact]
	public void The_reinforcement_bonus_is_a_multiplier_to_one_place()
	{
		(CGameState state, CCountry germany) = State();
		germany.ReinforcementCost = CFixedPoint.FromInt(20);
		germany.Modifier.Set("reinforcement_bonus", CFixedPoint.Parse("0.25"));
		string text = CShareTooltips.Production(Localise, state, germany, EProductionCategory.Reinforcement, Share("0.125"));
		Assert.Equal("With this budget, we can reinforce or repair §Y62.50§W% of our forces that need it. "
			+ "All reinforcements are multiplied by §G1.2§W from initial mobilization.", text);
	}

	[Fact]
	public void Upgrades_say_whether_the_share_covers_them_with_nothing_between()
	{
		(CGameState state, CCountry germany) = State();
		germany.UpgradeCost = CFixedPoint.FromInt(20);
		germany.SubUnitsUpgrading = 12;
		Assert.Equal("We need to devote §Y20.00§W IC to upgrade the §Y12§W units that require new equipment. With our budget, this is more than enough.",
			CShareTooltips.Production(Localise, state, germany, EProductionCategory.Upgrades, Share("0.25")));
		Assert.EndsWith("equipment. We can only cover §R62.50§W% of our upgrades with the current budget.",
			CShareTooltips.Production(Localise, state, germany, EProductionCategory.Upgrades, Share("0.125")));
	}

	[Fact]
	public void Consumer_goods_say_the_need_the_army_and_dissent_at_the_slider()
	{
		(CGameState state, CCountry germany) = State();
		germany.Modifier.Set("peace_consumer_goods_demand", CFixedPoint.Parse("0.25"));
		string text = CShareTooltips.Production(Localise, state, germany, EProductionCategory.ConsumerGoods, Share("0.2"));
		Assert.StartsWith("\nWe need a total of §Y25.00%§W of our IC into consumer goods.\nOur §Y0§W regular brigades", text);
		Assert.EndsWith("\nAvailability of consumer goods affects dissent for §R+0.05§W every day. ", text);
	}

	[Fact]
	public void Research_covers_the_projects_in_green_or_red()
	{
		(CGameState state, CCountry germany) = State();
		germany.TotalLeadership = CFixedPoint.FromInt(10);
		germany.ResearchQueue.AddRange(new[] { "a", "b", "c", "d" });
		Assert.Equal("We have §Y4§W projects being researched, and with §Y5.00§W leadership invested in it we can cover §G125.00%§W every day.",
			CShareTooltips.Leadership(Localise, state, germany, ELeadershipCategory.Research, Share("0.5")));
		Assert.Contains("cover §R75.00%§W every day",
			CShareTooltips.Leadership(Localise, state, germany, ELeadershipCategory.Research, Share("0.3")));
	}

	[Fact]
	public void Spies_and_officers_follow_their_defines()
	{
		(CGameState state, CCountry germany) = State();
		germany.TotalLeadership = CFixedPoint.FromInt(10);
		Assert.Contains("§Y0.50§W spies", CShareTooltips.Leadership(Localise, state, germany, ELeadershipCategory.Espionage, Share("0.25")));
		Assert.Contains("§Y5.0§W officers", CShareTooltips.Leadership(Localise, state, germany, ELeadershipCategory.Officers, Share("0.25")));
	}

	private static CModifier Law(string name, params (string Kind, string Value)[] values)
	{
		var modifier = new CModifier(name);
		foreach ((string kind, string value) in values)
		{
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return modifier;
	}
}
