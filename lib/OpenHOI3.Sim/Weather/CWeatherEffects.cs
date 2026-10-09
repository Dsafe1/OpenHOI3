using OpenHOI3.Sim.Countries;

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

	/// <summary>The country modifier that takes the edge off the cold.</summary>
	public const string WinterEffects = "winter_effects";

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
}
