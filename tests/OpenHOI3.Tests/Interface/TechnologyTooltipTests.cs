using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The technology screen's tooltips, against <c>docs/mechanics/technologyscreen.md</c>,
/// section 9, with the original's own strings as Their Finest Hour's tables give them.
/// </summary>
public class TechnologyTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static string Localise(string key) => key switch
	{
		"BASE" => "Base:",
		"FROM_TECHNOLOGY" => "From Improved Technology",
		"TTE_ALREADY" => "This technology is already being researched.",
		"ALREADY_RESEARCHED" => "We already know this technology.",
		"TECH_DC_TO_START" => "§WDouble click to start research.",
		"TECH_DC_TO_STOP" => "§WDouble click to cancel research.",
		"TECH_TREE_INFO" => "We are currently researching level §Y$LEVEL$§W of §Y$NAME$§W.\n",
		"TECH_SUSPEND" => "Suspend Research",
		"PEN_MAX_PRIO" => "Set this at maximum priority.",
		"MODIFIER_RESEARCH_EFFICIENCY" => "Research Efficiency",
		"TECH_ICON_SPEED_MOD" => "We have a §Y$SKILL$§W skill in §Y$NAME$§W, which impacts this research with §Y$PERCENT$%§W, and improves the research speed by $SPEED$%§W.",
		"START_RESEARCH" => "Start Research. Fully funded, this will take us about §Y$DAYS$§W days to research.",
		"ICON_FINISH" => "Researching this technology will increase §Y$NAME$§W by §G+1§W at completion.",
		"small_arms" => "Small Arms",
		"agriculture" => "Agriculture",
		"infantry_theory" => "Infantry Theory",
		_ => key,
	};

	private static CTechnology SmallArms(int startYear = 1936) =>
		new("small_arms", 12, new CModifier("small_arms"))
		{
			StartYear = startYear, FirstOffset = startYear + 1, AdditionalOffset = 2, OnCompletion = "infantry_theory",
		};

	private static CTechnology Agriculture() => new("agriculture", 1, new CModifier("agriculture")) { StartYear = 1936 };

	private static (CGameState State, CCountry Germany) State(CDate date, params CTechnology[] technologies)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		var berlin = new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, Leadership = CFixedPoint.FromInt(2) };
		berlin.Modifier.Set("local_leadership_modifier", CFixedPoint.Parse("0.5"));
		var provinces = new[] { new CProvince(0, "", isSea: false), berlin };
		var defines = new CDefines();
		defines.Set("country", "TECHNOLOGY_YEAR_IMPACT", CFixedPoint.Parse("0.2"));
		return (new CGameState(date, provinces, new[] { germany }, technologies: technologies, defines: defines), germany);
	}

	[Fact]
	public void The_leadership_label_is_the_provinces_base_and_what_technology_adds()
	{
		(CGameState state, CCountry germany) = State(new CDate(1936, 1, 1));
		germany.Technology.Set("leadership_gain", CFixedPoint.Parse("0.05"));
		Assert.Equal("Base: §Y3.00§W\n\nFrom Improved Technology: §G+5.00%§W\n",
			CTechnologyTooltips.LeadershipLabel(Localise, state, germany));
	}

	[Fact]
	public void A_tree_entry_names_the_level_it_would_be_and_to_come_the_double_click()
	{
		CTechnology smallArms = SmallArms();
		(CGameState state, CCountry germany) = State(new CDate(1936, 1, 1), smallArms);
		germany.TechnologyLevels["small_arms"] = 2;
		CTooltip tooltip = CTechnologyTooltips.TreeEntry(Localise, state, germany, smallArms);
		Assert.Equal("Small Arms (3)\n\n", tooltip.Text);
		Assert.Equal("§WDouble click to start research.", tooltip.DelayedExtra);

		germany.ResearchQueue.Add("small_arms");
		Assert.Equal("§WDouble click to cancel research.", CTechnologyTooltips.TreeEntry(Localise, state, germany, smallArms).DelayedExtra);
	}

	[Fact]
	public void A_technology_of_one_level_had_already_says_so_at_level_1()
	{
		CTechnology agriculture = Agriculture();
		(CGameState state, CCountry germany) = State(new CDate(1936, 1, 1), agriculture);
		germany.TechnologyLevels["agriculture"] = 1;
		Assert.Equal("Agriculture (1)\n\nWe already know this technology.\n",
			CTechnologyTooltips.TreeEntry(Localise, state, germany, agriculture).Text);
	}

	[Fact]
	public void A_queue_line_says_the_level_being_researched_and_what_efficiency_is_made_of()
	{
		CTechnology smallArms = SmallArms();
		(CGameState _, CCountry germany) = State(new CDate(1936, 1, 1), smallArms);
		germany.TechnologyLevels["small_arms"] = 1;
		Assert.Equal("We are currently researching level §Y2§W of §YSmall Arms§W.\n",
			CTechnologyTooltips.QueueLine(Localise, germany, smallArms, "name"));

		germany.Technology.Set("research_efficiency", CFixedPoint.Parse("0.1"));
		Assert.Equal("Set this at maximum priority.\nResearch Efficiency:\nFrom Improved Technology: §G+10.00%§W\n",
			CTechnologyTooltips.QueueLine(Localise, germany, smallArms, "tech_prio_maxup"));
	}

	[Fact]
	public void A_technology_of_one_level_on_the_queue_says_nothing_of_its_level()
	{
		CTechnology agriculture = Agriculture();
		(CGameState _, CCountry germany) = State(new CDate(1936, 1, 1), agriculture);
		Assert.Equal("", CTechnologyTooltips.QueueLine(Localise, germany, agriculture, "data"));
		Assert.Equal("Suspend Research", CTechnologyTooltips.QueueLine(Localise, germany, agriculture, "cancel"));
	}

	[Fact]
	public void A_category_icon_says_the_skill_its_weight_and_what_it_does_to_the_speed()
	{
		(CGameState _, CCountry germany) = State(new CDate(1936, 1, 1));
		germany.Abilities["infantry_theory"] = CFixedPoint.FromInt(9);
		// Nine is four past the five expected; its square root, 2, is worth a fifth.
		Assert.Equal("We have a §Y9.0§W skill in §YInfantry Theory§W, which impacts this research with §Y50%§W, and improves the research speed by §G+20%§W.",
			CTechnologyTooltips.CategoryIcon(Localise, germany, "infantry_theory", CFixedPoint.Parse("0.5")));
	}

	[Fact]
	public void The_start_button_counts_the_days_at_one_leadership_from_nothing()
	{
		CTechnology agriculture = Agriculture();
		(CGameState state, CCountry germany) = State(new CDate(1936, 1, 1), agriculture);
		// A technology of no difficulty costs 125 leadership-days.
		Assert.Equal("Start Research. Fully funded, this will take us about §Y125§W days to research.",
			CTechnologyTooltips.Start(Localise, state, germany, agriculture));
	}

	[Fact]
	public void Days_past_the_year_s_end_go_at_the_next_year_s_penalty()
	{
		// Due in 1937 and started on 1 November 1936: 61 days at a fifth more, then the rest at none.
		CTechnology early = new("early", 1, new CModifier("early")) { StartYear = 1937 };
		(CGameState state, CCountry germany) = State(new CDate(1936, 11, 1), early);
		long work = 125000 - 61000 * 1000 / 1200;
		Assert.Equal(61000 + work, CTechnologyTooltips.EstimatedDays(state, germany, early));
	}

	[Fact]
	public void The_picture_of_what_a_level_adds_to_names_it()
	{
		Assert.Equal("Researching this technology will increase §YInfantry Theory§W by §G+1§W at completion.",
			CTechnologyTooltips.IconFinish(Localise, SmallArms()));
	}
}
