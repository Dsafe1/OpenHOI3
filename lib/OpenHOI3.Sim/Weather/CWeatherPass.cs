using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
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

	/// <summary>Each province: whether it is night there, its weather, and the picture it shows.</summary>
	public static void RunHourly(CGameState state)
	{
		CWeatherDefines defines = CWeatherDefines.Of(state);
		ForEachProvince(state, province =>
		{
			province.IsNight = CDaylight.IsNight(state, province);
			CProvinceWeather.Update(state, province, defines);
			province.WeatherPicture = CProvinceWeather.Picture(province.Weather, defines);
		});
	}

	/// <summary>How many provinces one worker takes at a time.</summary>
	private const int ProvincesAChunk = 512;

	/// <summary>
	/// Every province but nought, side by side on the thread pool. A province's hour reads nothing but its own figures,
	/// the clock, the map's size, its terrain and the defines, and writes nothing but its own figures - its night, its
	/// weather, its picture and, the first time, its season - so the provinces may go in any order and on any thread, and
	/// the hour comes out the same as walking them in turn, which it was until 2026-11-19 (<c>docs/performance.md</c>,
	/// section 2). Whatever is added to it has to keep to that.
	/// </summary>
	private static void ForEachProvince(CGameState state, Action<CProvince> hour)
	{
		IReadOnlyList<CProvince> provinces = state.Provinces;
		Parallel.ForEach(Partitioner.Create(0, provinces.Count, ProvincesAChunk), range =>
		{
			for (int i = range.Item1; i < range.Item2; i++)
			{
				CProvince province = provinces[i];
				if (province.Id != 0)
				{
					hour(province);
				}
			}
		});
	}

	/// <summary>
	/// What entering a loaded game makes again of the weather: every province's season and its night for the saved hour.
	/// The save's weather is kept, and no picture is chosen and no front marked until the first hour - the original's
	/// too (<c>weather.md</c>, section 4; <c>reversing/findings/FINDINGS-weathersim.md</c>, A.6).
	/// </summary>
	public static void AtLoad(CGameState state)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			province.Climate = CClimate.Of(state, province);
			province.IsNight = CDaylight.IsNight(state, province);
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
			ForEachProvince(state, province => CProvinceWeather.Update(state, province, defines));
		}
	}
}
