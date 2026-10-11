using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The tooltip of a province, against <c>docs/mechanics/tooltips.md</c>, section 7: Berlin and
/// Warsaw on plains, and the Baltic.
/// </summary>
public class ProvinceTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Warsaw = 2;
	private const int Baltic = 3;

	private static readonly CTerrain Plains = new("plains", isWater: false, CColor.Gray)
	{
		MovementCost = CFixedPoint.One,
		Defence = CFixedPoint.Parse("-0.2"),
	};

	private static string Localise(string key) => key switch
	{
		"PROV_DISTANCE_FROM" => "$DIST$ km from $FROM$",
		_ => $"[{key}]",
	};

	private static CGameState State()
	{
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, Terrain = Plains },
			new CProvince(Warsaw, "Warsaw", isSea: false) { Owner = Pol, Controller = Pol, Terrain = Plains },
			new CProvince(Baltic, "Baltic", isSea: true),
		};
		var countries = new[]
		{
			new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin },
			new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Warsaw },
		};
		return new CGameState(new CDate(1936, 1, 1), provinces, countries);
	}

	private static string Text(CGameState state, int province, EMapTooltipMode mode, CUnit? unit = null, int selected = 0)
	{
		return CProvinceTooltip.Build(Localise, state, state.Province(province), mode, Ger, unit, selected,
			() => CSupplyNetworks.Refresh(state)).Text;
	}

	[Fact]
	public void The_political_mode_gives_the_name_the_owner_its_flag_and_the_ground()
	{
		Assert.Equal("Berlin (Germany) - ( @GER)\n[plains]", Text(State(), Berlin, EMapTooltipMode.Political));
	}

	[Fact]
	public void Occupied_ground_shows_the_holders_flag_before_the_owners()
	{
		CGameState state = State();
		state.Province(Berlin).Controller = Pol;

		Assert.Equal("Berlin (Germany) - ( @POL/@GER)\n[plains]", Text(state, Berlin, EMapTooltipMode.Political));
	}

	[Fact]
	public void The_terrain_mode_adds_the_grounds_figures_the_movement_cost_always_and_the_rest_where_not_nothing()
	{
		Assert.Equal("Berlin (Germany) - ( @GER)\n[plains]\n"
			+ "[TERRAIN_MOVEMENT_COST]§Y1.00§W\n[TERRAIN_DEFENCE]§Y-0.20§W\n",
			Text(State(), Berlin, EMapTooltipMode.Terrain));
	}

	[Fact]
	public void The_sea_has_its_name_alone_in_the_political_mode()
	{
		Assert.Equal("Baltic", Text(State(), Baltic, EMapTooltipMode.Political));
	}

	[Fact]
	public void A_selected_army_is_told_it_may_not_go_onto_a_neutrals_ground()
	{
		CGameState state = State();
		var division = new CUnit(EUnitKind.Division, Ger) { Id = 1, Location = Berlin, Name = "1. Division" };

		Assert.StartsWith("\n[UTM_NO_ACCESS]\nWarsaw (Poland)", Text(state, Warsaw, EMapTooltipMode.Political, division));
		Assert.StartsWith("Berlin (Germany)", Text(state, Berlin, EMapTooltipMode.Political, division));
	}

	[Fact]
	public void A_selected_province_has_the_distance_to_it_said()
	{
		Assert.EndsWith("\n0 km from Berlin", Text(State(), Warsaw, EMapTooltipMode.Political, selected: Berlin));
		Assert.DoesNotContain("km from", Text(State(), Berlin, EMapTooltipMode.Political, selected: Berlin));
	}

	[Fact]
	public void A_headquarters_brigade_makes_an_army_a_headquarters()
	{
		var army = new CUnit(EUnitKind.Army, Ger);
		Assert.False(CProvinceTooltip.IsHeadquarters(army));
		army.SubUnits.Add(new CSubUnit(new CSubUnitType(CProvinceTooltip.HeadquartersBrigade, EUnitBranch.Land)));
		Assert.True(CProvinceTooltip.IsHeadquarters(army));
	}
}
