using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The four builder windows' tooltips, against <c>docs/mechanics/builders.md</c>, section 11,
/// with the original's own strings as Their Finest Hour's tables give them.
/// </summary>
public class BuilderTooltipTests
{
	private static string Localise(string key) => key switch
	{
		"BUILD_STR_IRO" => "Strength",
		"BUILD_STR_DRO" => "How many men each brigade is made up of.",
		"BUILD_ADD_REM_BUNDLE" => "§YCtrl + LMB§! to add or remove §G$NUM$§!",
		"BUILD_CA_BONUS_VALUE" => "Combined arms bonus for combat",
		"TC_BUILD_COST_AND_TIME" => "reduces building cost and time by",
		"infantry_practical" => "Infantry Practical",
		"infantry_unit_type" => "Infantry",
		"infantry_unit_type_desc" => "Infantry is the basis for combined arms.",
		"UA_ATTACK" => "Attack",
		"UA_DEFENCE" => "Defence",
		"UA_MOVEMENT" => "Movement",
		"UA_ATTRITION" => "Attrition",
		"RIVER_ADJUSTER" => "River",
		"NIGHT_ADJUSTER" => "Night",
		"FORT_ADJUSTER" => "Fort",
		"AMPH_ADJUSTER" => "Amphibious",
		"mountain" => "Mountain",
		_ => key,
	};

	[Fact]
	public void A_header_gives_its_name_and_then_what_it_means()
	{
		CTooltip tooltip = CBuilderTooltips.For(Localise, CBuilderTooltips.Brigade, "sort_str");

		Assert.Equal("Strength", tooltip.Text);
		Assert.Equal("How many men each brigade is made up of.", tooltip.Delayed);
	}

	[Theory]
	[InlineData("designer", "total_oc", "BUILD_FC_IRO", "BUILD_FC_DRO")]
	[InlineData("designer", "sort_speed", "BUILD_SPEED_DD_IRO", "BUILD_SPEED_DD_DRO")]
	[InlineData("ship", "sort_speed", "BUILD_SSPEED_IRO", "BUILD_SSPEED_DRO")]
	[InlineData("ship", "sort_visibi", "BUILD_VISI_IRO", "BUILD_VISI_DRO")]
	[InlineData("air", "sort_surf_d", "SURFACE_DEFENCE", "BUILD_SURF_D_DRO")]
	[InlineData("air", "sort_soft_a", "BUILD_SA_IRO", "BUILD_SA_DRO")]
	[InlineData("brigade", "total_armored", "BUILD_ARMOR_IRO", "BUILD_ARMOR_DRO")]
	[InlineData("brigade", "sort_off", "BUILD_OFF_DD_IRO", "BUILD_OFF_DD_DRO")]
	public void Each_window_has_its_own_keys(string window, string element, string text, string delayed)
	{
		var columns = window switch
		{
			"designer" => CBuilderTooltips.Designer,
			"ship" => CBuilderTooltips.Ship,
			"air" => CBuilderTooltips.Air,
			_ => CBuilderTooltips.Brigade,
		};

		CTooltip tooltip = CBuilderTooltips.For(key => key, columns, element);

		Assert.Equal(text, tooltip.Text);
		Assert.Equal(delayed, tooltip.Delayed);
	}

	[Theory]
	[InlineData("total_str")]
	[InlineData("str")]
	[InlineData("accept")]
	[InlineData("sort_hull")]
	public void Anything_else_in_the_brigade_builder_says_nothing(string element)
	{
		Assert.Equal(CTooltip.Empty, CBuilderTooltips.For(Localise, CBuilderTooltips.Brigade, element));
	}

	[Theory]
	[InlineData("serial_plus")]
	[InlineData("serial_minus")]
	[InlineData("parallell_plus")]
	[InlineData("parallel_minus")]
	public void A_step_button_says_ctrl_steps_by_ten(string element)
	{
		CTooltip tooltip = CBuilderTooltips.For(Localise, CBuilderTooltips.Ship, element);

		Assert.Equal("§YCtrl + LMB§! to add or remove §G10§!", tooltip.Text);
		Assert.Equal("", tooltip.Delayed);
	}

	[Fact]
	public void A_group_icon_names_its_group_counted_from_one()
	{
		var groups = new[]
		{
			new CCombinedArmsGroup("infantry_unit_type", isBase: true, CFixedPoint.Zero),
			new CCombinedArmsGroup("armor_unit_type", isBase: false, CFixedPoint.Parse("0.05")),
		};

		CTooltip first = CBuilderTooltips.ForDesigner(Localise, groups, () => "", "combined_arms_indicator_1");

		Assert.Equal("Infantry", first.Text);
		Assert.Equal("Infantry is the basis for combined arms.", first.Delayed);
		Assert.Equal("armor_unit_type", CBuilderTooltips.ForDesigner(Localise, groups, () => "", "combined_arms_indicator_2").Text);
		Assert.Equal(CTooltip.Empty, CBuilderTooltips.ForDesigner(Localise, groups, () => "", "combined_arms_indicator_0"));
		Assert.Equal(CTooltip.Empty, CBuilderTooltips.ForDesigner(Localise, groups, () => "", "combined_arms_indicator_3"));
		Assert.Equal(CTooltip.Empty, CBuilderTooltips.ForDesigner(Localise, groups, () => "", "combined_arms_indicator_x"));
	}

	[Fact]
	public void The_designer_answers_for_its_bonus_and_its_terrain_summary()
	{
		var groups = System.Array.Empty<CCombinedArmsGroup>();

		Assert.Equal("Combined arms bonus for combat",
			CBuilderTooltips.ForDesigner(Localise, groups, () => "", "combined_arms_bonus_value").Text);
		Assert.Equal("the summary", CBuilderTooltips.ForDesigner(Localise, groups, () => "the summary", "terrainbonus").Text);
		Assert.Equal("Strength", CBuilderTooltips.ForDesigner(Localise, groups, () => "", "total_str").Text);
	}

	[Theory]
	[InlineData("1", 1)]
	[InlineData(" 12abc", 12)]
	[InlineData("-3", -3)]
	[InlineData("x", 0)]
	[InlineData("", 0)]
	public void A_number_is_read_as_atoi_reads_it(string text, int expected)
	{
		Assert.Equal(expected, CBuilderTooltips.Atoi(text));
	}

	[Fact]
	public void An_adjuster_writes_three_figures_times_a_hundred_and_attrition_as_it_is()
	{
		Assert.Equal("Attack §R-47.5%§W, Defence §R-17.5%§W, Movement §R-10.0%§W",
			CBuilderTooltips.Describe(Localise, new long[] { -475, -175, -100, 0 }));
		Assert.Equal("Attack §G+5.0%§W", CBuilderTooltips.Describe(Localise, new long[] { 50, 0, 0, 0 }));
		// More attrition is red, and keeps its plus.
		Assert.Equal("Attrition §R+0.5%§W", CBuilderTooltips.Describe(Localise, new long[] { 0, 0, 0, 500 }));
		Assert.Equal("Attrition §G-0.5%§W", CBuilderTooltips.Describe(Localise, new long[] { 0, 0, 0, -500 }));
		Assert.Equal("", CBuilderTooltips.Describe(Localise, new long[] { 0, 0, 0, 0 }));
	}

	private static CSubUnitStats Brigade(params (string Adjuster, string Kind, string Value)[] figures)
	{
		var stats = new CSubUnitStats();
		foreach ((string adjuster, string kind, string value) in figures)
		{
			if (!stats.Adjusters.TryGetValue(adjuster, out CModifier? modifier))
			{
				stats.Adjusters[adjuster] = modifier = new CModifier(adjuster);
			}
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return stats;
	}

	[Fact]
	public void The_terrain_summary_averages_the_brigades_with_the_ground_and_the_defines_counted_once()
	{
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, "RIVER_CROSSING_PENALTY", CFixedPoint.Parse("-0.6"));
		defines.Set(CManpower.MilitarySection, "BASE_NIGHT_PENALTY", CFixedPoint.Parse("-0.5"));
		defines.Set(CManpower.MilitarySection, "BASE_FORT_PENALTY", CFixedPoint.Parse("-0.09"));
		defines.Set(CManpower.MilitarySection, "AMPHIBIOUS_LANDING_PENALTY", CFixedPoint.Parse("-0.7"));
		var terrains = new[]
		{
			new CTerrain("plains", isWater: false, default),
			new CTerrain("mountain", isWater: false, default) { Attack = CFixedPoint.Parse("-0.4"), Attrition = CFixedPoint.FromInt(2) },
		};
		// Three infantry brigades and an armoured one, by the base game's files.
		CSubUnitStats armour = Brigade(("mountain", "attack", "-0.30"), ("mountain", "movement", "-0.40"),
			("mountain", "defence", "-0.70"), ("river", "attack", "-0.05"));
		var brigades = new[] { new CSubUnitStats(), new CSubUnitStats(), new CSubUnitStats(), armour };

		string summary = CBuilderTooltips.TerrainSummary(Localise, defines, terrains, brigades);

		Assert.Equal(
			"Mountain: Attack §R-47.5%§W, Defence §R-17.5%§W, Movement §R-10.0%§W\n" +
			"River: Attack §R-61.2%§W\n" +
			"Night: Attack §R-50.0%§W\n" +
			"Fort: Attack §R-9.0%§W\n" +
			"Amphibious: Attack §R-70.0%§W\n",
			summary);
		Assert.Equal("", CBuilderTooltips.TerrainSummary(Localise, defines, terrains, System.Array.Empty<CSubUnitStats>()));
	}

	private static (CGameState State, CCountry Country) Practicals(string ability)
	{
		var italy = new CCountry { Tag = new CCountryTag("ITA"), Name = "Italy", DefinitionFile = "", Capital = 1 };
		italy.Abilities["infantry_practical"] = CFixedPoint.Parse(ability);
		var provinces = new[] { new CProvince(0, "", isSea: false) };
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { italy },
			technologyCategories: new[] { new CTechnologyCategory("infantry_practical", IsTheory: false) });
		return (state, italy);
	}

	[Theory]
	[InlineData("5.5", "2.5")]
	[InlineData("4", "-10.0")]
	public void The_practical_says_how_much_cheaper_and_quicker_it_builds(string ability, string percent)
	{
		(CGameState state, CCountry italy) = Practicals(ability);

		CTooltip tooltip = CBuilderTooltips.Practical(Localise, state, italy, "infantry_practical");

		Assert.Equal("Infantry Practical reduces building cost and time by §Y" + percent + "%§W", tooltip.Text);
	}

	[Fact]
	public void A_practical_the_game_has_no_category_of_says_nothing()
	{
		(CGameState state, CCountry italy) = Practicals("5");

		Assert.Equal(CTooltip.Empty, CBuilderTooltips.Practical(Localise, state, italy, "armour_practical"));
		Assert.Equal(CTooltip.Empty, CBuilderTooltips.Practical(Localise, state, italy, ""));
	}
}
