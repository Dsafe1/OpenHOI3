namespace OpenHOI3.Sim.Weather;

/// <summary>
/// The seven pictures of the weather the original chooses among (<c>CWeather::GraphicName</c>), numbered as the
/// province view's <c>weather_icon</c> frames are. See <c>docs/mechanics/weather.md</c>, section 6.
/// </summary>
public enum EWeatherPicture
{
	Clear = 1,
	PartlyCloudy = 2,
	Overcast = 3,
	Rain = 4,
	Storm = 5,
	Snow = 6,
	Snowstorm = 7,
}
