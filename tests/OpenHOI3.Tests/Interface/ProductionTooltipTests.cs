using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The production screen's tooltips, against <c>docs/mechanics/productionscreen.md</c>,
/// section 8, with the original's own strings as Their Finest Hour's tables give them.
/// </summary>
public class ProductionTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static string Localise(string key) => key switch
	{
		"RES_CHANGE" => "Changes by $VAL$ every day.\n",
		"NO_NUKE_PRODUCTION" => "We do not produce any nuclear bombs, as we lack the technological power to do it.",
		"NUKE_PRODUCTION" => "We are producing about §Y$AMOUNT$§W nuclear bombs every month.",
		"LEDGER_ESCORTS" => "Escorts",
		"NEED" => "Need",
		"convoy_escorts" => "Convoy Escorts",
		"STRAT_DET_RES_TT" => "$NAME$\n$EFFECT$\n$FROM$",
		"rubber" => "Rubber",
		"PEN_CANCEL_PRODU" => "Cancel Production",
		"PEN_CANCEL_PRODU_DRO" => "Any effort currently invested in this project is lost permanently if you cancel it.",
		"CANCEL_MULTI_CONSTRUCTIONS_DESC" => "§GCTRL + LMB§! to cancel all constructions of the same type with progress equal to or less than the selected one.",
		"PEN_FULL_SPEED" => "This production is going ahead at full speed.",
		"PEN_PARTIAL_PROGRESS" => "This production is only progressing at §Y$PERC$%§W. You need to invest more IC for it to progress faster.",
		"PEN_NO_PROGRESS" => "There is currently no progress at all on this project.",
		"BRIGADE_MIL_CON_CUMULATED_EXP" => "Current unit experience §G$EXP$§!",
		"industry" => "Industry",
		_ => key,
	};

	private static (CGameState State, CCountry Germany) State(params CTechnology[] technologies)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		var berlin = new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, StrategicResource = "rubber" };
		berlin.Modifier.Set("infrastructure", CFixedPoint.One);
		var bremen = new CProvince(2, "Bremen", isSea: false) { Owner = Ger, Controller = Ger, StrategicResource = "rubber" };
		bremen.Modifier.Set("infrastructure", CFixedPoint.Parse("0.8"));
		bremen.Modifier.Set("local_infrastructure", CFixedPoint.Parse("-0.5"));
		var provinces = new[] { new CProvince(0, "", isSea: false), berlin, bremen };
		var rubber = new CModifier("rubber");
		rubber.Set("global_ic", CFixedPoint.Parse("0.05"));
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany }, technologies: technologies,
			strategicResources: new[] { rubber });
		return (state, germany);
	}

	[Fact]
	public void A_resource_icon_label_and_figure_give_its_ledger()
	{
		(CGameState _, CCountry germany) = State();
		Assert.StartsWith("Changes by", CProductionTooltips.For(Localise, germany, "metal_label").Text);
		Assert.StartsWith("Changes by", CProductionTooltips.For(Localise, germany, "rm_icon").Text);
		Assert.Equal("", CProductionTooltips.For(Localise, germany, "ic_value").Text);
	}

	[Fact]
	public void The_bombs_say_none_can_be_made_until_a_technology_makes_some()
	{
		(CGameState _, CCountry germany) = State();
		Assert.Equal("We do not produce any nuclear bombs, as we lack the technological power to do it.",
			CProductionTooltips.For(Localise, germany, "nukes_value").Text);
		germany.Technology.Set("nuclear_production", CFixedPoint.Parse("0.1"));
		Assert.Equal("We are producing about §Y0.10§W nuclear bombs every month.", CProductionTooltips.Nukes(Localise, germany));
	}

	[Fact]
	public void The_escort_button_names_the_first_technology_that_allows_escorts_until_one_does()
	{
		var effect = new CModifier("convoy_escorts");
		effect.Set(CTechnology.AllowEscorts, CFixedPoint.One);
		(CGameState state, CCountry germany) = State(
			new CTechnology("destroyer_design", 5, new CModifier("destroyer_design")),
			new CTechnology("convoy_escorts", 1, effect));
		Assert.Equal("Escorts\nNeed §YConvoy Escorts", CProductionTooltips.Escort(Localise, state, germany));

		germany.Technology.Set(CTechnology.AllowEscorts, CFixedPoint.One);
		Assert.True(CProductionTooltips.MayBuildEscorts(germany));
		Assert.Equal("Escorts", CProductionTooltips.Escort(Localise, state, germany));
	}

	[Fact]
	public void A_building_button_is_its_name_and_a_line_break()
	{
		Assert.Equal("Industry\n", CProductionTooltips.Building(Localise,
			new CBuilding(0, "industry", 10, "industrial_capacity", CFixedPoint.One) { IsCapital = true }));
	}

	[Fact]
	public void A_province_with_nothing_built_is_minus_a_tenth_of_a_percent()
	{
		var province = new CProvince(3, "Nowhere", isSea: false);
		Assert.Equal(-1, CProductionTooltips.InfrastructureShare(province));
		province.Modifier.Set("infrastructure", CFixedPoint.Parse("0.6"));
		province.Modifier.Set("local_infrastructure", CFixedPoint.Parse("-2"));
		// What stands is never counted as less than a hundredth.
		Assert.Equal(16, CProductionTooltips.InfrastructureShare(province));
	}

	[Fact]
	public void A_strategic_resource_names_itself_what_the_country_has_of_it_and_each_province()
	{
		(CGameState state, CCountry germany) = State();
		var copy = new CModifier("rubber");
		copy.Set("global_ic", CFixedPoint.Parse("0.05"));
		germany.ModifierSources.Add(new CModifierSource(copy));

		string text = CProductionTooltips.StrategicResource(Localise, state, germany, "rubber");
		Assert.StartsWith("Rubber\n", text);
		// Half of Bremen's stands: yellow, between a third and ninety-nine hundredths.
		Assert.EndsWith("\n\n@GER  Berlin - §G100.00%§W\n@GER  Bremen - §Y50.00%§W", text);
	}

	[Fact]
	public void The_cancel_button_says_what_is_lost_to_come()
	{
		CTooltip tooltip = CProductionTooltips.QueueLine(Localise, new CConstruction(Ger), "cancel");
		Assert.Equal("Cancel Production", tooltip.Text);
		Assert.Equal("Any effort currently invested in this project is lost permanently if you cancel it.\n\n"
			+ "§GCTRL + LMB§! to cancel all constructions of the same type with progress equal to or less than the selected one.",
			tooltip.Delayed);
	}

	[Fact]
	public void A_line_says_how_fast_it_went_cut_to_a_whole_percent()
	{
		var construction = new CConstruction(Ger) { Status = CFixedPoint.Parse("0.523") };
		Assert.Equal("This production is only progressing at §Y52%§W. You need to invest more IC for it to progress faster.",
			CProductionTooltips.QueueLine(Localise, construction, "unit_name").Text);
		construction.Status = CFixedPoint.One;
		Assert.Equal("This production is going ahead at full speed.", CProductionTooltips.QueueLine(Localise, construction, "eta_date").Text);
		construction.Status = CFixedPoint.Zero;
		Assert.Equal("There is currently no progress at all on this project.", CProductionTooltips.QueueLine(Localise, construction, "ic_cost").Text);
	}

	[Fact]
	public void A_line_s_counter_lists_its_brigades_by_counter_and_name()
	{
		var construction = new CConstruction(Ger) { AccumulatedExperience = CFixedPoint.Parse("1.5") };
		construction.Brigades.Add(new CBrigadeOrder(new CSubUnitType("infantry_brigade", EUnitBranch.Land)) { Name = "1. Infanterie" });
		construction.Brigades.Add(new CBrigadeOrder(new CSubUnitType("artillery_brigade", EUnitBranch.Land)) { Name = "1. Artillerie" });
		Assert.Equal("£infantry_brigade 1. Infanterie\n£artillery_brigade 1. Artillerie\nCurrent unit experience §G1.50§!",
			CProductionTooltips.QueueLine(Localise, construction, "counter_type").Text);
	}
}
