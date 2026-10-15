namespace OpenHOI3.Sim.Weather;

/// <summary>
/// The weather defines a province's hour reads, taken once for a pass over every province rather than looked up for
/// each: thousandths, as <c>common/defines.lua</c>'s <c>weather</c> block gives them - the last six the limits of the
/// pictures.
/// </summary>
public readonly record struct CWeatherDefines(
	long PressureMin, long PressureMax, long PressureDefault, long PressureStep, long MinHumidity, long MaxHumidity,
	long HourlyFrozenIncrease, long HourlyThaw, long TemperatureChangeSpeed, long CloudTemperatureDrop,
	long SnowLimit, long SnowStormLimit, long RainLimit, long StormLimit, long CloudLimit, long PartialCloudLimit)
{
	public static CWeatherDefines Of(CGameState state)
	{
		CDefines defines = state.Defines;
		long Get(string name) => defines[CProvinceWeather.Section, name].Raw;
		return new CWeatherDefines(Get(CProvinceWeather.PressureMin), Get(CProvinceWeather.PressureMax),
			Get(CProvinceWeather.PressureDefault), Get(CProvinceWeather.PressureStep), Get(CProvinceWeather.MinHumidity),
			Get(CProvinceWeather.MaxHumidity), Get(CProvinceWeather.HourlyFrozenIncrease), Get(CProvinceWeather.HourlyThaw),
			Get(CProvinceWeather.TemperatureChangeSpeed), Get(CProvinceWeather.CloudTemperatureDrop),
			Get("GFX_SNOW_LIMIT"), Get("GFX_SNOW_STORM_LIMIT"), Get("GFX_RAIN_LIMIT"), Get("GFX_STORM_LIMIT"),
			Get("GFX_CLOUD_LIMIT"), Get("GFX_PARTIAL_CLOUD_LIMIT"));
	}
}
