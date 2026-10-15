using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the province view says: its figures and their gates, its weather strip, its building rows with their columns,
/// keys and pips, its underground and its tooltips, against <c>docs/mechanics/provinceview.md</c>, on
/// <see cref="RebelWorld"/>.
/// </summary>
public class ProvinceViewTextTests
{
	private static string Localise(string key) => key switch
	{
		"PROV_VIEW_MULTI" => "Multiple Selected($NUM$)",
		"PV_NB_HOSTILE_TERR" => "not yours",
		"PV_NB_MAX_SIZE" => "full",
		"PV_BUILD_ONLY_IN_PORT" => "coastal",
		"PV_BUILD_DESC" => "damaged $CURR$ of $MAX$",
		"PV_BUILD_W_DESC" => "built $MAX$",
		"PV_CURR_CONS_MORE" => "more",
		_ => key,
	};

	private static CGameState Seen()
	{
		CGameState state = RebelWorld.State();
		CProvinceIntel.Rebuild(state);
		return state;
	}

	[Fact]
	public void The_columns_split_the_files_buildings_behind_an_empty_first_place_and_drop_the_hidden_ones()
	{
		var buildings = new List<CBuilding>();
		foreach (string key in new[] { "air_base", "naval_base", "coastal_fort", "land_fort", "anti_air", "radar_station",
			"underground", "nuclear_reactor", "rocket_test", "industry", "infra" })
		{
			buildings.Add(new CBuilding(buildings.Count, key, 10, "", CFixedPoint.Zero) { ShowForProvince = key != "underground" });
		}
		(List<CBuilding> left, List<CBuilding> right) = CProvinceBuildingRows.Columns(buildings);
		Assert.Equal(new[] { "air_base", "naval_base", "coastal_fort", "land_fort", "anti_air" }, left.ConvertAll(b => b.Key));
		Assert.Equal(new[] { "radar_station", "nuclear_reactor", "rocket_test", "industry", "infra" }, right.ConvertAll(b => b.Key));
	}

	[Fact]
	public void A_rows_key_is_the_first_letter_no_row_before_took_matched_with_case()
	{
		List<(string Shortcut, string Label)> rows = CProvinceBuildingRows.Shortcuts(new[]
		{
			"Air Base", "Naval Base", "Coastal Fort", "Land Fort", "Anti-Aircraft Guns", "Radar Station", "Nuclear Reactor",
			"Rocket Test Site", "Industrial Capacity", "Infrastructure",
		});
		Assert.Equal(new[] { "A", "N", "C", "L", "n", "R", "u", "o", "I", "f" }, rows.ConvertAll(row => row.Shortcut));
		Assert.Equal("A§Yn§Wti-Aircraft Guns", rows[4].Label);
		Assert.Equal(("", "AN"), CProvinceBuildingRows.Shortcuts(new[] { "AN", "NA", "AN" })[2]);
	}

	[Fact]
	public void A_pip_is_green_working_orange_built_and_not_working_and_grey_queued()
	{
		CGameState state = Seen();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		CProvince province = state.Province(1);
		province.SetBuildingMaxLevel(RebelWorld.AirBase, CFixedPoint.FromInt(3));
		province.SetBuildingCurrentLevel(RebelWorld.AirBase, CFixedPoint.Parse("2.5"));
		germany.Constructions.Add(new CConstruction(RebelWorld.Ger) { Building = RebelWorld.AirBase, Location = 1, Size = 2 });
		int[] pips = CProvinceBuildingRows.Pips(state, germany, RebelWorld.AirBase, province);
		Assert.Equal(new[] { 1, 1, 2, 3, 3, 0, 0, 0, 0, 0 }, pips);
	}

	[Fact]
	public void Below_intelligence_six_a_pip_says_only_whether_anything_is_built()
	{
		CGameState state = Seen();
		CCountry poland = state.Country(RebelWorld.Pol)!;
		CProvince province = state.Province(1);                   // German, far from Poland: nothing known
		province.SetBuildingMaxLevel(RebelWorld.AirBase, CFixedPoint.FromInt(3));
		Assert.Equal(0, CProvinceIntel.Level(state, province, RebelWorld.Pol));
		int[] pips = CProvinceBuildingRows.Pips(state, poland, RebelWorld.AirBase, province);
		Assert.Equal(CProvinceBuildingRows.FrameOnly, pips[0]);
		Assert.All(pips[1..], pip => Assert.Equal(CProvinceBuildingRows.Hidden, pip));
		// Infrastructure under two levels, known not at all: nothing.
		province.SetBuildingMaxLevel(RebelWorld.Roads, CFixedPoint.One);
		Assert.All(CProvinceBuildingRows.Pips(state, poland, RebelWorld.Roads, province),
			pip => Assert.Equal(CProvinceBuildingRows.Hidden, pip));
	}

	[Fact]
	public void A_rows_button_is_enabled_where_any_selected_province_held_may_take_one()
	{
		CGameState state = Seen();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		germany.TechnologyLevels[RebelWorld.AirTech.Name] = 1;
		CProvince polish = state.Province(5);
		CProvince german = state.Province(1);
		Assert.False(CProvinceBuildingRows.Enabled(state, germany, RebelWorld.AirBase, polish, new[] { polish }));
		Assert.True(CProvinceBuildingRows.Enabled(state, germany, RebelWorld.AirBase, polish, new[] { polish, german }));
	}

	[Fact]
	public void The_buttons_tooltip_gives_the_first_reason_and_the_coastal_line_where_there_is_a_port()
	{
		CGameState state = Seen();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		germany.TechnologyLevels[RebelWorld.AirTech.Name] = 1;
		var naval = new CBuilding(9, "naval_base", 10, "", CFixedPoint.Zero) { IsPort = true };
		Assert.Equal("PV_NB_TECH\n (§R*) air_tech",
			CProvinceViewTooltips.Building(Localise, state, state.Country(RebelWorld.Pol)!, state.Province(5),
				RebelWorld.AirBase, "label").Delayed);
		CProvinceIntel.Raise(state.Province(5), germany.Index, 9);
		Assert.Equal("not yours", CProvinceViewTooltips.Building(Localise, state, germany, state.Province(5),
			RebelWorld.AirBase, "label").Delayed);
		state.Province(1).SetBuildingMaxLevel(RebelWorld.AirBase, CFixedPoint.FromInt(10));
		Assert.Equal("full", CProvinceViewTooltips.Building(Localise, state, germany, state.Province(1),
			RebelWorld.AirBase, "label").Delayed);
		// No port in the province: nothing said, though a port building may not go there.
		Assert.Equal("PV_NB_TECH", CProvinceViewTooltips.Building(Localise, state, germany, state.Province(2), naval, "l").Delayed);
	}

	[Fact]
	public void A_pips_tooltip_says_the_working_level_and_more_being_built_by_the_holder()
	{
		CGameState state = Seen();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		CProvince province = state.Province(1);
		province.SetBuildingMaxLevel(RebelWorld.AirBase, CFixedPoint.FromInt(2));
		province.SetBuildingCurrentLevel(RebelWorld.AirBase, CFixedPoint.Parse("1.5"));
		germany.Constructions.Add(new CConstruction(RebelWorld.Ger) { Building = RebelWorld.AirBase, Location = 1, Size = 1 });
		Assert.Equal("damaged 1.500 of 2\nmore", CProvinceViewTooltips.Pip(Localise, state, germany, province, RebelWorld.AirBase).Text);
	}

	[Fact]
	public void The_figures_need_full_intelligence_and_a_province_producing_nothing_reads_nought()
	{
		CGameState state = Seen();
		CProvince province = state.Province(1);
		Assert.Equal("?", CProvinceViewText.Manpower(state, province, RebelWorld.Pol));
		Assert.Equal("§Y2.50", CProvinceViewText.Manpower(state, province, RebelWorld.Ger));
		Assert.Equal("-", CProvinceViewText.Leadership(state, province, RebelWorld.Ger));
		Assert.Equal("§W0", CProvinceViewText.Resource(state, province, RebelWorld.Pol, EGoods.Metal));
		province.MaxProducing[EGoods.Metal] = CFixedPoint.FromInt(4);
		province.CurrentProducing[EGoods.Metal] = CFixedPoint.Parse("3.25");
		Assert.Equal("§Y?§W", CProvinceViewText.Resource(state, province, RebelWorld.Pol, EGoods.Metal));
		Assert.Equal("§R3.3", CProvinceViewText.Resource(state, province, RebelWorld.Ger, EGoods.Metal));
	}

	[Fact]
	public void The_weather_strip_reads_the_provinces_weather()
	{
		CGameState state = Seen();
		foreach ((string name, string value) in new[]
		{
			("GFX_RAIN_LIMIT", "0.1"), ("GFX_SNOW_LIMIT", "0.1"), ("GFX_STORM_LIMIT", "15"), ("GFX_SNOW_STORM_LIMIT", "15"),
			("GFX_PARTIAL_CLOUD_LIMIT", "0.3"), ("GFX_CLOUD_LIMIT", "0.8"),
		})
		{
			state.Defines.Set("weather", name, CFixedPoint.Parse(value));
		}
		var weather = new CWeather();
		Assert.Equal(1, CProvinceViewText.WeatherFrame(state, weather));
		Assert.Equal("§Y0.0C", CProvinceViewText.Temperature(weather));
		weather.Precipitation = CFixedPoint.Parse("0.5");
		weather.Temperature = CFixedPoint.FromInt(-6);
		weather.WindSpeed = CFixedPoint.FromInt(20);
		Assert.Equal(7, CProvinceViewText.WeatherFrame(state, weather));
		Assert.Equal("WEATHER_SNOW_STORM", CProvinceViewText.WeatherKey(7));
		Assert.Equal("§R-6.0C", CProvinceViewText.Temperature(weather));
		Assert.Equal(66, CProvinceViewText.WindPercent(weather));
		weather.WindSpeed = CFixedPoint.FromInt(45);
		Assert.Equal(100, CProvinceViewText.WindPercent(weather));
		weather.Muddyness = CFixedPoint.Parse("0.2");
		Assert.Equal(3, CProvinceViewText.GroundFrame(weather));
		weather.Frozen = CFixedPoint.Parse("0.1");
		Assert.Equal(2, CProvinceViewText.GroundFrame(weather));
	}

	[Fact]
	public void The_intelligence_texts_are_one_out_and_the_title_counts_the_selection()
	{
		Assert.Equal("INTEL_UNITS", CProvinceViewTooltips.IntelText(Localise, 2));
		Assert.Equal("INTEL_2", CProvinceViewTooltips.IntelText(Localise, 3));
		Assert.Equal("MAX_INTEL_LEVEL", CProvinceViewTooltips.IntelText(Localise, 9));
		Assert.Equal("Multiple Selected(3)", CProvinceViewText.MultiTitle(Localise, 3));
	}

	[Fact]
	public void A_way_is_drawn_and_said_by_its_kind_and_an_impassable_one_says_nothing()
	{
		CGameState state = Seen();
		var river = new CProvinceEdge(2, EProvinceEdgeKind.River, 0, CFixedPoint.One);
		var wall = new CProvinceEdge(2, EProvinceEdgeKind.Impassable, 0, CFixedPoint.One);
		var plain = new CProvinceEdge(2, EProvinceEdgeKind.Plain, 0, CFixedPoint.One);
		Assert.Equal("GFX_provinceview_river", CProvinceViewText.WaySprite(state, river));
		Assert.Equal("GFX_provinceview_passable", CProvinceViewText.WaySprite(state, plain));
		Assert.Equal("PV_RIVER_DESC", CProvinceViewTooltips.Way(Localise, state, river).Delayed);
		Assert.Equal("", CProvinceViewTooltips.Way(Localise, state, wall).Text);
	}

	[Fact]
	public void The_underground_window_shows_its_strength_twice_the_suppression_and_steps_its_mission()
	{
		CGameState state = Seen();
		CCountry poland = state.Country(RebelWorld.Pol)!;
		CProvince warsaw = state.Province(4);                     // Polish, held by Germany
		warsaw.SetBuildingMaxLevel(RebelWorld.Underground, CFixedPoint.One);
		warsaw.SetBuildingCurrentLevel(RebelWorld.Underground, CFixedPoint.Parse("0.335"));
		warsaw.UndergroundOwner = RebelWorld.Pol;
		warsaw.Suppression = CFixedPoint.Parse("0.6");
		Assert.Same(warsaw, CProvinceUndergroundText.About(state, poland, warsaw, new[] { warsaw }));
		Assert.Equal(34, CProvinceUndergroundText.Status(state, warsaw));
		Assert.Equal("§Y1%§W", CProvinceUndergroundText.Suppression(state, warsaw));
		Assert.Equal(3, CProvinceUndergroundText.MissionFrame(state, warsaw));
		Assert.Equal(1, CProvinceUndergroundText.Step(3, forward: true));
		Assert.Equal(2, CProvinceUndergroundText.Step(3, forward: false));
		Assert.Equal(3, CProvinceUndergroundText.Step(1, forward: false));
	}
}
