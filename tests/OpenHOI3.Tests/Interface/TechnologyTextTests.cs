using System.Collections.Generic;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What a technology's tooltip says of its <c>allow</c> block and of what its levels do,
/// against <c>docs/mechanics/technologyscreen.md</c>, section 9, with Their Finest Hour's
/// own strings where the test needs their shape.
/// </summary>
public class TechnologyTextTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static readonly Dictionary<string, string> Texts = new()
	{
		["TECHNOLOGY_HIGHER_THAN"] = " at least ",
		["TECHNOLOGY_LOWER_THAN"] = " lower than ",
		["HAVE_MORE_PORTS_THAN"] = "Number of home ports at least ",
		["HAVE_LESS_PORTS_THAN"] = "Have less home ports than ",
		["AND_TRIGGER_STARTS"] = "All of the below",
		["OR_TRIGGER_STARTS"] = "One of the following must be true",
		["ANY_PROVINCE_STARTS"] = "Any owned province",
		["HAVE_A_X_INY"] = "Have a $X$ in $Y$",
		["DONT_HAVE_A_X_INY"] = "Do NOT have a $X$ in $Y$",
		["infantry_activation"] = "Infantry Activation",
		["smallarms_technology"] = "Small Arms",
		["infantry_folder"] = "Infantry",
		["nuclear_reactor"] = "Nuclear Reactor",
		["ENABLE_UNIT_TECH"] = "Allow construction of",
		["ENABLE_BUILDING_TECH"] = "Allow construction of",
		["infantry_brigade"] = "Infantry",
		["infantry_brigade_short"] = "Inf",
		["SOFT_ATTACK"] = "Soft Attack",
		["HARD_ATTACK"] = "Hard Attack",
		["BUILD_TIME"] = "Build Time",
		["DAYS"] = "days",
		["FORT_ADJUSTER"] = "Fort",
		["UA_ATTACK"] = "Attack",
		["SUPPLY_THROUGHPUT_TECH"] = "Supply Throughput",
		["SUPPLY_TRANSFER_COST_TECH"] = "Supply Transfer Cost",
		["ATTACK_DELAY_FROM_TECH"] = "Attack Delay",
		["AD_HOURS"] = "hours",
		["MAX_ATTRITION_TECH"] = "Maximum Attrition",
		["ALLOW_ESCORTS_TECH"] = "Allows construction of convoy escorts",
		["tactic_assault"] = "Assault",
		["ORDER_CONVOY_RAID"] = "Convoy Raid",
		["ORD_EFF"] = " Efficiency",
		["ORD_RED"] = " Organisation Penalty",
		["naval_engineering"] = "Naval Engineering",
		["DECAY"] = "Decay",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CTechnology Technology(string name) =>
		new(name, 10, new CModifier(name)) { Folder = "infantry_folder", StartYear = 1936, FirstOffset = 1937, AdditionalOffset = 2 };

	private static (CGameState State, CCountry Germany) State(CTechnology[] technologies, CBuilding[]? buildings = null,
		CSubUnitType[]? types = null)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
		};
		var typeTable = new Dictionary<string, CSubUnitType>();
		foreach (CSubUnitType type in types ?? System.Array.Empty<CSubUnitType>())
		{
			typeTable[type.Key] = type;
		}
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany }, buildings: buildings,
			technologies: technologies, technologyCategories: new[] { new CTechnologyCategory("naval_engineering", true) })
		{
			SubUnitTypes = typeTable,
			Tactics = new[] { new CCombatTactic("tactic_assault", 1) },
			Terrains = new[] { new CTerrain("forest", false, default) },
		};
		return (state, germany);
	}

	private static CResearchCondition Level(string technology, int level) =>
		new(EResearchCondition.TechnologyLevel, technology, CFixedPoint.FromInt(level));

	private static CResearchCondition Group(EResearchCondition kind, params CResearchCondition[] children)
	{
		var group = new CResearchCondition(kind);
		group.Children.AddRange(children);
		return group;
	}

	// ---- the allow block

	[Fact]
	public void At_the_top_each_condition_is_a_line_with_its_asterisk_and_no_header()
	{
		(CGameState state, CCountry germany) = State(new[] { Technology("infantry_activation"), Technology("smallarms_technology") });
		germany.TechnologyLevels["infantry_activation"] = 1;
		CResearchCondition allow = Group(EResearchCondition.All, Level("infantry_activation", 1), Level("smallarms_technology", 3));

		Assert.Equal(
			"(§G*§W)§YInfantry Activation (Infantry)§W at least §Y1§W\n"
			+ "(§R*§W)§YSmall Arms (Infantry)§W at least §Y3§W\n",
			CRequirementText.For(Localise, state, germany, allow));
	}

	[Fact]
	public void A_group_inside_a_group_is_indented_a_level_deeper_than_its_header()
	{
		(CGameState state, CCountry germany) = State(new[] { Technology("infantry_activation"), Technology("smallarms_technology") });
		germany.TechnologyLevels["infantry_activation"] = 1;
		CResearchCondition allow = Group(EResearchCondition.All,
			Group(EResearchCondition.Any,
				Level("infantry_activation", 1),
				Group(EResearchCondition.All, Level("smallarms_technology", 3), Level("infantry_activation", 2))));

		Assert.Equal(
			"(§G*§W)One of the following must be true:\n"
			+ "   (§G*§W)§YInfantry Activation (Infantry)§W at least §Y1§W\n"
			+ "   (§R*§W)All of the below:\n"
			+ "      (§R*§W)§YSmall Arms (Infantry)§W at least §Y3§W\n"
			+ "      (§R*§W)§YInfantry Activation (Infantry)§W at least §Y2§W\n",
			CRequirementText.For(Localise, state, germany, allow));
	}

	[Fact]
	public void A_none_of_says_the_opposite_green_where_it_does_not_hold_and_sits_on_its_parents_line()
	{
		(CGameState state, CCountry germany) = State(new[] { Technology("infantry_activation"), Technology("smallarms_technology") });
		germany.TechnologyLevels["smallarms_technology"] = 4;
		CResearchCondition allow = Group(EResearchCondition.All,
			Group(EResearchCondition.None, Level("infantry_activation", 1)),
			Group(EResearchCondition.Any, Level("infantry_activation", 2), Group(EResearchCondition.None, Level("smallarms_technology", 3))));

		Assert.Equal(
			"(§G*§W)§YInfantry Activation (Infantry)§W lower than §Y1§W\n"
			+ "(§R*§W)One of the following must be true:\n"
			+ "   (§R*§W)§YInfantry Activation (Infantry)§W at least §Y2§W\n"
			+ "   (§R*§W)§YSmall Arms (Infantry)§W lower than §Y3§W\n",
			CRequirementText.For(Localise, state, germany, allow));
	}

	[Fact]
	public void Any_owned_province_names_the_country_and_its_building_has_no_asterisk()
	{
		var reactor = new CBuilding(0, "nuclear_reactor", 10, "", default);
		(CGameState state, CCountry germany) = State(System.Array.Empty<CTechnology>(), new[] { reactor });
		CResearchCondition allow = Group(EResearchCondition.All,
			new CResearchCondition(EResearchCondition.OwnsProvinceWithBuilding, "nuclear_reactor"),
			new CResearchCondition(EResearchCondition.Ports, "", CFixedPoint.FromInt(1)));

		Assert.Equal(
			"(§R*§W)Any owned province (Germany):\n"
			+ "   Have a §YNuclear Reactor§W in §Y§W\n"
			+ "(§R*§W)Number of home ports at least §Y1§W\n",
			CRequirementText.For(Localise, state, germany, allow));
	}

	// ---- what levels do

	[Fact]
	public void The_first_level_says_what_it_allows_and_a_unit_types_figures_go_on_one_line()
	{
		var infantry = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		CTechnology technology = new("infantry_activation", 1, new CModifier("infantry_activation"))
		{
			ActivatesUnits = new[] { "infantry_brigade" }, ActivatesBuilding = "nuclear_reactor",
		};
		var effect = new CSubUnitStats();
		effect.Values.Set("soft_attack", CFixedPoint.Parse("0.5"));
		effect.Values.Set("hard_attack", CFixedPoint.Parse("-0.2"));
		effect.Values.Set("build_time", CFixedPoint.FromInt(-5));
		var fort = new CModifier();
		fort.Set("attack", CFixedPoint.Parse("0.1"));
		effect.Adjusters["fort"] = fort;
		technology.UnitEffects["infantry_brigade"] = effect;
		var reactor = new CBuilding(0, "nuclear_reactor", 10, "", default);
		(CGameState state, CCountry germany) = State(new[] { technology }, new[] { reactor }, new[] { infantry });

		Assert.Equal(
			"Allow construction of §YInfantry§W\n"
			+ "Allow construction of §YNuclear Reactor§W\n"
			+ "Inf: Build Time §G-5§W days, Soft Attack §G+0.50§W , Hard Attack §R-0.20§W , Fort: Attack §G+10.0%§W\n",
			CTechnologyEffectsText.For(Localise, state, germany, technology, 1));
	}

	[Fact]
	public void Later_levels_allow_nothing_and_every_figure_is_times_the_levels()
	{
		var infantry = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		var technology = new CTechnology("smallarms_technology", 10, new CModifier("smallarms_technology"))
		{
			ActivatesUnits = new[] { "infantry_brigade" },
		};
		var effect = new CSubUnitStats();
		effect.Values.Set("soft_attack", CFixedPoint.Parse("0.5"));
		technology.UnitEffects["infantry_brigade"] = effect;
		(CGameState state, CCountry germany) = State(new[] { technology }, null, new[] { infantry });

		Assert.Equal("Inf: Soft Attack §G+1.50§W\n", CTechnologyEffectsText.For(Localise, state, germany, technology, 3));
	}

	[Fact]
	public void The_countrys_figures_have_each_its_own_colour_scale_and_ending()
	{
		CTechnology technology = Technology("logistics");
		CModifier effect = technology.EffectPerLevel;
		effect.Set("supply_throughput", CFixedPoint.Parse("0.05"));
		effect.Set("supply_transfer_cost", CFixedPoint.Parse("-0.015"));
		effect.Set("attack_delay", CFixedPoint.FromInt(2));
		effect.Set("maximum_attrition", CFixedPoint.Parse("0.1"));
		effect.Set(CTechnology.AllowEscorts, CFixedPoint.One);
		effect.Set("tactic_assault", CFixedPoint.FromInt(-100));
		technology.MissionEfficiency["convoy_raid"] = CFixedPoint.Parse("0.1");
		technology.MissionReduction["convoy_raid"] = CFixedPoint.Parse("-0.1");
		(CGameState state, CCountry _) = State(new[] { technology });

		Assert.Equal(
			"Attack Delay: §G4 hours§W\n"
			+ "Supply Transfer Cost: §G-0.03§W\n"
			+ "Supply Throughput: §G+10.00%§W\n"
			+ "Allows construction of convoy escorts§W\n"
			+ "Maximum Attrition: §R+20.00%§W\n"
			+ "Assault: §G+-200%§!\n"
			+ "Convoy Raid Efficiency §G+20.0%§!\n"
			+ "Convoy Raid Organisation Penalty §G-20.0%§!\n",
			CTechnologyEffectsText.CountryFigures(Localise, state, technology, 2));
	}

	[Fact]
	public void Decay_comes_last_after_a_line_break()
	{
		CTechnology technology = new("naval_theory", 1, new CModifier("naval_theory"))
		{
			Decay = new[] { ("naval_engineering", CFixedPoint.Parse("-0.1")) },
		};
		technology.EffectPerLevel.Set("supply_throughput", CFixedPoint.Parse("0.05"));
		(CGameState state, CCountry _) = State(new[] { technology });

		Assert.Equal(
			"Supply Throughput: §G+5.00%§W\n\nNaval Engineering Decay: §G-10.0%§W\n",
			CTechnologyEffectsText.CountryFigures(Localise, state, technology, 1));
	}
}
