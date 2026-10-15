using System;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Weather;

/// <summary>
/// A province's weather made again for the hour, the temperature it is pulled toward, the ranges it is held to and the
/// picture it shows: the original's <c>CWeather::Update</c>, <c>BaseTemperature</c>, <c>Clamp</c> and
/// <c>GraphicName</c>, step for step and in its integer arithmetic. See <c>docs/mechanics/weather.md</c>, section 2.
/// </summary>
public static class CProvinceWeather
{
	public const string Section = CWeatherEffects.WeatherSection;
	public const string PressureMin = "PRESSUREMIN";
	public const string PressureMax = "PRESSUREMAX";
	public const string PressureDefault = "PRESSUREDEFAULT";
	public const string PressureStep = "PRESSURESTEP";
	public const string MaxHumidity = "MAXHUMIDITY";
	public const string MinHumidity = "MINHUMIDITY";
	public const string HourlyFrozenIncrease = "HOURLYFROZENINCREASE";
	public const string HourlyThaw = "HOURLYTHAW";
	public const string TemperatureChangeSpeed = "TEMPERATURECHANGESPEED";
	public const string CloudTemperatureDrop = "CLOUDCOVERAGETEMPERATUREDROP";

	private const long One = CFixedPoint.Scale;

	/// <summary>
	/// The hour's weather, in the original's order: the wind dies where no front is; humidity rises with warmth and
	/// low pressure, five times the terrain's own; cloud gathers where the air is wet for its warmth and thins where not;
	/// rain is twice the cloud over a bar, and without a front burns cloud and humidity off; on land, mud and frost; the
	/// temperature steps toward its target; pressure fills back toward its default; and only then is all of it held to
	/// its range - so what each step reads of the steps before is unclamped.
	/// </summary>
	public static void Update(CGameState state, CProvince province) => Update(state, province, CWeatherDefines.Of(state));

	/// <summary>The hour, with the defines taken once for a pass over every province.</summary>
	public static void Update(CGameState state, CProvince province, CWeatherDefines defines)
	{
		CWeather weather = province.Weather;
		bool land = !province.IsSea;
		bool front = province.WeatherFront is not null;
		long terrainHumidity = province.Terrain?.Humidity.Raw ?? 0;
		long terrainPrecipitation = province.Terrain?.Precipitation.Raw ?? 0;

		long wind = weather.WindSpeed.Raw;
		long humidity = weather.Humidity.Raw;
		long pressure = weather.Pressure.Raw;
		long temperature = weather.Temperature.Raw;
		long cloud = weather.CloudCoverage.Raw;
		long muddyness = weather.Muddyness.Raw;
		long frozen = weather.Frozen.Raw;

		if (!front)
		{
			wind = wind * 750 / One;
		}

		// Humidity.
		long warmth = Math.Max(temperature, 0);
		long cap;
		if (land)
		{
			cap = terrainHumidity * 5000 / One;
		}
		else
		{
			cap = 5000;
			warmth += 10000;
		}
		if (humidity >= One)
		{
			warmth = warmth * One / humidity;
		}
		long min = defines.PressureMin;
		long max = defines.PressureMax;
		long low = max == min ? 0 : (pressure - min) * One / (max - min);
		humidity += (One - low) * warmth / One * cap / One;

		// Cloud: this hour's humidity against last hour's temperature.
		long ratio = temperature > 0 ? humidity * One / temperature * 10 : humidity;
		long bar = 50000 - terrainPrecipitation - (land ? 30000 : 0);
		if (ratio >= bar && humidity > 0)
		{
			cloud += front ? 100 : 200;
		}
		else
		{
			cloud -= 50;
		}

		// Precipitation.
		long precipitation = 0;
		long rainBar = land ? (front ? 250 : 0) : 500;
		if (cloud > rainBar)
		{
			precipitation = 2000 * (cloud - rainBar) / One;
		}
		if (!front)
		{
			cloud -= precipitation * 200 / One;
			humidity -= 10000 * precipitation / One;
		}

		// Mud and frost, on land: last hour's temperature and frost, this hour's rain.
		if (land)
		{
			long infrastructure = CSupplyFigures.EffectiveInfrastructure(province);
			if (precipitation > 500 || (frozen > 0 && temperature >= -5000))
			{
				muddyness += (One - infrastructure) * 50 / One;
			}
			else
			{
				muddyness -= Math.Max(infrastructure * 5 / One, 2);
			}
			if (temperature > 15000)
			{
				muddyness -= 5;
			}
			else if (temperature < -5000)
			{
				muddyness = 0;
			}

			long freeze = defines.HourlyFrozenIncrease;
			if (temperature < 0 && precipitation > 250)
			{
				if (temperature < -5000)
				{
					frozen += freeze;
				}
			}
			else if (temperature < -10000 && frozen < 500)
			{
				frozen += 200 * freeze / One;
			}
			else if (temperature < -2000 && frozen < 250)
			{
				frozen += 100 * freeze / One;
			}
			else if (temperature > 0)
			{
				frozen -= defines.HourlyThaw;
			}
		}

		weather.WindSpeed = new CFixedPoint(wind);
		weather.Humidity = new CFixedPoint(humidity);
		weather.CloudCoverage = new CFixedPoint(cloud);
		weather.Precipitation = new CFixedPoint(precipitation);
		weather.Muddyness = new CFixedPoint(muddyness);
		weather.Frozen = new CFixedPoint(frozen);

		// Temperature, toward a target worked out from all of the above, unclamped.
		long step = land ? 2 : 1;
		long target = BaseTemperature(state, province, defines);
		if (target > temperature + 10000)
		{
			step *= 10;
		}
		if (target < temperature - 10000)
		{
			step *= 10;
		}
		else if (target < temperature - 5000)
		{
			step *= 5;
		}
		if (province.Climate == 0)
		{
			province.Climate = CClimate.Of(state, province);
		}
		long speed = defines.TemperatureChangeSpeed;
		if (speed > 0 && province.Climate == 3)
		{
			step *= 10;
		}
		if (speed < 0 && province.Climate == 5)
		{
			step *= 2;
		}
		long delta = step * One * speed / One;
		temperature += temperature > target ? -delta : delta;
		weather.Temperature = new CFixedPoint(temperature);

		// Pressure fills back toward its default, except at sea under a front.
		if (pressure < defines.PressureDefault && (land || province.WeatherFront is null))
		{
			pressure += defines.PressureStep;
		}
		weather.Pressure = new CFixedPoint(pressure);

		Clamp(weather, defines);
	}

	/// <summary>
	/// The temperature a province is pulled toward: its base, and a season's term - colder still at a high latitude in
	/// winter, with a wind chill on frozen ground below -3 - and night's; then cloud's chill where it is over 10 degrees or
	/// at sea, and four degrees more where it is under 3 and anything falls.
	/// </summary>
	public static long BaseTemperature(CGameState state, CProvince province) =>
		BaseTemperature(state, province, CWeatherDefines.Of(state));

	public static long BaseTemperature(CGameState state, CProvince province, CWeatherDefines defines)
	{
		CWeather weather = province.Weather;
		long target = province.BaseTemperature.Raw;
		if (province.Climate == 0)
		{
			province.Climate = CClimate.Of(state, province);
		}
		long latitude = CDaylight.LatitudeNorth(state, province);
		switch (province.Climate)
		{
			case 3:
				target += 5000;
				target -= province.IsNight ? 2000 : 0;
				break;
			case 4:
			case 2:
				target += 1000;
				target -= province.IsNight ? 2000 : 0;
				break;
			case 1:
				target -= 1000;
				target -= latitude > 950 ? 1000 : 0;
				target -= latitude > 650 ? 1000 : 0;
				target -= province.IsNight ? 2000 : 0;
				break;
			case 5:
				target -= 4000;
				target -= latitude > 950 ? 2000 : 0;
				target -= latitude > 650 ? 4000 : 0;
				if (target < -3000 && weather.Frozen.Raw >= One)
				{
					target -= 125 * weather.WindSpeed.Raw / One + 5000;
				}
				target -= province.IsNight ? 5000 : 0;
				break;
		}
		if (target > 10000 || province.IsSea)
		{
			target -= weather.CloudCoverage.Raw * defines.CloudTemperatureDrop / One;
		}
		if (target < 3000 && weather.Precipitation.Raw > 0)
		{
			target -= 4000;
		}
		return target;
	}

	/// <summary>Each figure held to its range, in the original's order.</summary>
	public static void Clamp(CGameState state, CWeather weather) => Clamp(weather, CWeatherDefines.Of(state));

	public static void Clamp(CWeather weather, CWeatherDefines defines)
	{
		weather.WindSpeed = Within(weather.WindSpeed, 0, 200000);
		weather.Temperature = Within(weather.Temperature, -50000, 50000);
		weather.Humidity = Within(weather.Humidity, defines.MinHumidity, defines.MaxHumidity);
		weather.Muddyness = Within(weather.Muddyness, 0, One);
		weather.Frozen = Within(weather.Frozen, 0, One);
		weather.Precipitation = Within(weather.Precipitation, 0, One);
		weather.Pressure = Within(weather.Pressure, defines.PressureMin, defines.PressureMax);
		weather.CloudCoverage = Within(weather.CloudCoverage, 0, One);
	}

	/// <summary>
	/// The picture the weather shows: snow, or a snowstorm in the wind, where it falls and it is under -5; else rain, or
	/// a storm; else overcast, partly cloudy or clear by the cloud - against the six <c>GFX_*_LIMIT</c> defines.
	/// </summary>
	public static EWeatherPicture Picture(CGameState state, CWeather weather) => Picture(weather, CWeatherDefines.Of(state));

	public static EWeatherPicture Picture(CWeather weather, CWeatherDefines defines)
	{
		long rain = weather.Precipitation.Raw;
		long wind = weather.WindSpeed.Raw;
		long cloud = weather.CloudCoverage.Raw;
		if (rain > defines.SnowLimit && weather.Temperature.Raw < -5000)
		{
			return wind > defines.SnowStormLimit ? EWeatherPicture.Snowstorm : EWeatherPicture.Snow;
		}
		if (rain > defines.RainLimit)
		{
			return wind > defines.StormLimit ? EWeatherPicture.Storm : EWeatherPicture.Rain;
		}
		if (cloud > defines.CloudLimit)
		{
			return EWeatherPicture.Overcast;
		}
		return cloud > defines.PartialCloudLimit ? EWeatherPicture.PartlyCloudy : EWeatherPicture.Clear;
	}

	private static CFixedPoint Within(CFixedPoint value, long least, long most)
	{
		return new CFixedPoint(Math.Clamp(value.Raw, least, Math.Max(least, most)));
	}
}
