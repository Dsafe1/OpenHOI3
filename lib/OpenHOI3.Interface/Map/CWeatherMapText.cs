using System;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What the weather map mode says of a province, in place of its name and owner: eight figures of its weather, the way a
/// front over it is going, and the word for the picture its weather shows - written straight after the line before. And
/// the name of that picture on the map. See <c>docs/mechanics/weather.md</c>, section 6.
/// </summary>
public static class CWeatherMapText
{
	private static readonly (string Key, int Places, string Unit, Func<CWeather, CFixedPoint> Figure)[] Lines =
	{
		("TEMPERATURE", 1, "C", weather => weather.Temperature),
		("PRESSURE", 1, "mb", weather => weather.Pressure),
		("WINDSPEED", 1, "", weather => weather.WindSpeed),
		("HUMIDITY", 2, "%", weather => weather.Humidity),
		("OVERCAST", 2, "%", weather => Percent(weather.CloudCoverage)),
		("PRECIPITATION", 2, "%", weather => Percent(weather.Precipitation)),
		("MUDDY", 2, "%", weather => Percent(weather.Muddyness)),
		("FROZEN", 2, "%", weather => Percent(weather.Frozen)),
	};

	public static string Text(Func<string, string> localise, CProvince province)
	{
		var text = new StringBuilder();
		foreach ((string key, int places, string unit, Func<CWeather, CFixedPoint> figure) in Lines)
		{
			text.Append(localise(key)).Append(": ").Append(CNumberText.Format(figure(province.Weather), places))
				.Append(key == "WINDSPEED" ? localise("KPH") : unit).Append('\n');
		}
		if (province.WeatherFront is { } front)
		{
			text.Append(localise("WEATHER_FRONT").Replace("$DIR$", localise(Direction(front.Direction))));
		}
		text.Append(localise(Word(province.WeatherPicture)));
		return text.ToString();
	}

	/// <summary>The compass point of a heading in thousandths of a turn, each bound taken in: due west at 0 and 1000.</summary>
	public static string Direction(int heading) => heading switch
	{
		<= 63 => "DIR_WEST",
		<= 188 => "DIR_NORTHWEST",
		<= 313 => "DIR_NORTH",
		<= 438 => "DIR_NORTHEAST",
		<= 563 => "DIR_EAST",
		<= 688 => "DIR_SOUTHEAST",
		<= 813 => "DIR_SOUTH",
		<= 938 => "DIR_SOUTHWEST",
		_ => "DIR_WEST",
	};

	/// <summary>The word for a picture; a clear sky's is also what anything not named answers.</summary>
	public static string Word(EWeatherPicture picture) => picture switch
	{
		EWeatherPicture.Snowstorm => "WEATHER_SNOW_STORM",
		EWeatherPicture.Snow => "WEATHER_SNOW",
		EWeatherPicture.Storm => "WEATHER_STORM",
		EWeatherPicture.Rain => "WEATHER_RAIN",
		EWeatherPicture.Overcast => "WEATHER_CLOUD",
		EWeatherPicture.PartlyCloudy => "WEATHER_PARTIAL_CLOUD",
		_ => "WEATHER_CLEAR_SKY",
	};

	/// <summary>The <c>weatherBillboardType</c> a picture is drawn with on the map, or none for a clear sky.</summary>
	public static string? Billboard(EWeatherPicture picture) => picture switch
	{
		EWeatherPicture.Snowstorm => "weather_snow_storm",
		EWeatherPicture.Snow => "weather_snow",
		EWeatherPicture.Storm => "weather_thunderstorm",
		EWeatherPicture.Rain => "weather_rain",
		EWeatherPicture.Overcast => "weather_cloud",
		EWeatherPicture.PartlyCloudy => "weather_partialcloud",
		_ => null,
	};

	private static CFixedPoint Percent(CFixedPoint share) => new(share.Raw * 100000 / CFixedPoint.Scale);
}
