using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the weather map mode says of a province and how it colours one, against <c>docs/mechanics/weather.md</c>,
/// section 6.
/// </summary>
public class WeatherMapTextTests
{
	private static string Localise(string key) => key switch
	{
		"WEATHER_FRONT" => "This low pressure zone is moving §Y$DIR$§W.",
		"DIR_EAST" => "East",
		"KPH" => "KPH",
		"WEATHER_RAIN" => "Rain",
		_ => key,
	};

	[Fact]
	public void Eight_figures_cut_to_their_places_then_a_front_s_way_and_the_word_with_no_break_before_it()
	{
		var province = new CProvince(1, "Berlin", isSea: false) { WeatherPicture = EWeatherPicture.Rain };
		province.Weather.Temperature = CFixedPoint.Parse("-1.19");
		province.Weather.Pressure = CFixedPoint.Parse("1013");
		province.Weather.WindSpeed = CFixedPoint.Parse("12.345");
		province.Weather.Humidity = CFixedPoint.Parse("55.555");
		province.Weather.CloudCoverage = CFixedPoint.Parse("0.567");
		province.Weather.Precipitation = CFixedPoint.Parse("0.234");
		province.Weather.Muddyness = CFixedPoint.Parse("0.1");
		province.WeatherFront = new CWeatherFront(1, direction: 500, rotation: 0);

		Assert.Equal(
			"TEMPERATURE: -1.1C\nPRESSURE: 1013.0mb\nWINDSPEED: 12.3KPH\nHUMIDITY: 55.55%\nOVERCAST: 56.70%\n"
			+ "PRECIPITATION: 23.40%\nMUDDY: 10.00%\nFROZEN: 0.00%\nThis low pressure zone is moving §YEast§W.Rain",
			CWeatherMapText.Text(Localise, province));
	}

	[Fact]
	public void A_heading_names_its_compass_point_each_bound_taken_in()
	{
		Assert.Equal("DIR_WEST", CWeatherMapText.Direction(63));
		Assert.Equal("DIR_NORTHWEST", CWeatherMapText.Direction(64));
		Assert.Equal("DIR_NORTH", CWeatherMapText.Direction(250));
		Assert.Equal("DIR_EAST", CWeatherMapText.Direction(563));
		Assert.Equal("DIR_SOUTH", CWeatherMapText.Direction(750));
		Assert.Equal("DIR_WEST", CWeatherMapText.Direction(939));
		Assert.Equal("DIR_WEST", CWeatherMapText.Direction(1034));
	}

	[Fact]
	public void A_clear_sky_has_no_picture_and_a_storm_is_the_thunderstorm()
	{
		Assert.Null(CWeatherMapText.Billboard(EWeatherPicture.Clear));
		Assert.Equal("weather_thunderstorm", CWeatherMapText.Billboard(EWeatherPicture.Storm));
		Assert.Equal("WEATHER_CLEAR_SKY", CWeatherMapText.Word(EWeatherPicture.Clear));
	}

	[Fact]
	public void Frozen_ground_is_greyed_and_lightened_and_land_nobody_owns_is_grey_and_dim()
	{
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) },
			System.Array.Empty<OpenHOI3.Sim.Countries.CCountry>());
		var green = new CTerrain("forest", false, new CColor(0f, 0.5f, 0f));
		var owned = new CProvince(1, "a", isSea: false) { Terrain = green, Owner = new OpenHOI3.Sim.Countries.CCountryTag("GER") };
		Assert.Equal(new CColor(0f, 0.5f, 0f), CWeatherMapColours.Of(state, owned, player: null));

		// Wholly frozen: every channel the grey of 0.358, and 0.3 lighter.
		owned.Weather.Frozen = CFixedPoint.One;
		CColor frozen = CWeatherMapColours.Of(state, owned, player: null);
		Assert.Equal(0.5f * 0.715160f + 0.3f, frozen.R, 3);
		Assert.Equal(frozen.R, frozen.G, 3);

		var nobody = new CProvince(2, "b", isSea: false) { Terrain = green };
		CColor dim = CWeatherMapColours.Of(state, nobody, player: null);
		Assert.True(dim.G < 0.6f && dim.R > 0f);
	}
}
