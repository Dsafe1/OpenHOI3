using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Weather;

/// <summary>
/// When weather is made: every province's in the hour, after the units have moved and before the battles; and a new
/// game's start - every province's temperature set to its target and ninety-six hours of weather run with the clock
/// standing still. See <c>docs/mechanics/weather.md</c>, section 4.
/// </summary>
public static class CWeatherPass
{
	public const string HoursAhead = "INITIAL_SIMULATION_HOURS_AHEAD";

	/// <summary>Each province in turn: whether it is night there, its weather, and the picture it shows.</summary>
	public static void RunHourly(CGameState state)
	{
		CWeatherDefines defines = CWeatherDefines.Of(state);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			province.IsNight = CDaylight.IsNight(state, province);
			CProvinceWeather.Update(state, province, defines);
			province.WeatherPicture = CProvinceWeather.Picture(province.Weather, defines);
		}
	}

	/// <summary>
	/// What a new game does with the weather before its first day: every province reset - nothing, and the default
	/// pressure - its season and its night for the hour the game starts at, and its temperature set to the one it is
	/// pulled toward; then <c>INITIAL_SIMULATION_HOURS_AHEAD</c> hours of the fronts and every province's weather, with
	/// the clock, the night and the season standing still and no picture chosen.
	/// </summary>
	public static void AtNewGame(CGameState state)
	{
		state.Weather.Fronts.Clear();
		CFixedPoint pressure = state.Defines[CProvinceWeather.Section, CProvinceWeather.PressureDefault];
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			CWeather weather = province.Weather;
			weather.Humidity = weather.WindSpeed = weather.Temperature = weather.Precipitation = CFixedPoint.Zero;
			weather.Muddyness = weather.Frozen = weather.CloudCoverage = CFixedPoint.Zero;
			weather.Pressure = pressure;
			province.WeatherFront = null;
			province.Climate = CClimate.Of(state, province);
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			province.IsNight = CDaylight.IsNight(state, province);
			province.Weather.Temperature = new CFixedPoint(CProvinceWeather.BaseTemperature(state, province));
		}

		long hours = state.Defines[CProvinceWeather.Section, HoursAhead].Raw / CFixedPoint.Scale;
		CWeatherDefines defines = CWeatherDefines.Of(state);
		for (long hour = 0; hour < hours; hour++)
		{
			CWeatherFronts.RunHourly(state);
			foreach (CProvince province in state.Provinces)
			{
				if (province.Id != 0)
				{
					CProvinceWeather.Update(state, province, defines);
				}
			}
		}
	}
}
