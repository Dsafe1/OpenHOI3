using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Weather;

/// <summary>
/// What the weather over a province does to what happens in it. See
/// <c>docs/mechanics/movement.md</c>, section 5.
/// </summary>
public static class CWeatherEffects
{
	public const string WeatherSection = "weather";
	public const string MuddynessMovementModifier = "MUDDYNESSMOVEMENTMODIFIER";
	public const string ColdMovementModifier = "COLDMOVEMENTMODIFIER";
	public const string LandLowTemperatureThreshold = "LANDLOWTEMPERATURETHRESHOLD";

	public const string LandRainImpactModifier = "LANDRAINIMPACTMODIFIER";
	public const string LandRainImpactCap = "LANDRAINIMPACTCAP";
	public const string LandLowTemperatureImpact = "LANDLOWTEMPERATUREIMPACT";
	public const string LandHighTemperatureThreshold = "LANDHIGHTEMPERATURETHRESHOLD";
	public const string LandHighTemperatureImpact = "LANDHIGHTEMPERATUREIMPACT";

	/// <summary>The country modifier that takes the edge off the cold.</summary>
	public const string WinterEffects = "winter_effects";

	/// <summary>The country modifier that takes the edge off the heat of humid ground.</summary>
	public const string JungleEffects = "jungle_effects";

	/// <summary>The most the weather may take off a unit's speed, in thousandths.</summary>
	public const long MovementFloor = -989;

	private const long Scale = CFixedPoint.Scale;

	/// <summary>
	/// What the weather adds to a land unit's speed in the province, in thousandths and
	/// never above nothing:
	///
	///     mud, where the ground is not frozen at all: muddyness times the define;
	///     cold, below the threshold: the define, less the share the country's
	///     winter_effects takes off it;
	///     and no lower than the floor.
	/// </summary>
	public static long Movement(CGameState state, CWeather weather, CCountry country)
	{
		long effect = 0;
		if (weather.Frozen == CFixedPoint.Zero)
		{
			effect = weather.Muddyness.Raw * state.Defines[WeatherSection, MuddynessMovementModifier].Raw / Scale;
		}
		if (weather.Temperature < state.Defines[WeatherSection, LandLowTemperatureThreshold])
		{
			effect += state.Defines[WeatherSection, ColdMovementModifier].Raw
				* (Scale - country.Modifier[WinterEffects].Raw) / Scale;
		}
		return effect < MovementFloor ? MovementFloor : effect;
	}

	/// <summary>
	/// What the weather takes off a land unit's attack and defence, in thousandths:
	///
	///     rain: precipitation, by the wind and five more, by the define - and no more
	///     than its cap;
	///     cold, below the threshold: a twentieth of the threshold and the temperature
	///     added and turned about, by the define, less the share the country's
	///     winter_effects takes off it;
	///     heat, above the other threshold: the define - less the country's
	///     jungle_effects where the ground has any humidity.
	///
	/// The cold is the original's own arithmetic, and jumps at the threshold.
	/// </summary>
	public static long LandCombat(CGameState state, CWeather weather, CTerrain? terrain, CCountry country)
	{
		CDefines defines = state.Defines;
		long rain = weather.Precipitation.Raw * (weather.WindSpeed.Raw + 5 * Scale) / Scale
			* defines[WeatherSection, LandRainImpactModifier].Raw / Scale;
		long effect = System.Math.Min(rain, defines[WeatherSection, LandRainImpactCap].Raw);

		long low = defines[WeatherSection, LandLowTemperatureThreshold].Raw;
		if (weather.Temperature.Raw < low)
		{
			effect += -(low + weather.Temperature.Raw) / 20
				* defines[WeatherSection, LandLowTemperatureImpact].Raw / Scale
				* (Scale - country.Modifier[WinterEffects].Raw) / Scale;
		}
		if (weather.Temperature.Raw > defines[WeatherSection, LandHighTemperatureThreshold].Raw)
		{
			long heat = defines[WeatherSection, LandHighTemperatureImpact].Raw;
			if (terrain is not null && terrain.Humidity > CFixedPoint.Zero)
			{
				heat = heat * (Scale - country.Modifier[JungleEffects].Raw) / Scale;
			}
			effect += heat;
		}
		return effect;
	}
}
