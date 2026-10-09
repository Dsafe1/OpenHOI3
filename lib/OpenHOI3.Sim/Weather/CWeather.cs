namespace OpenHOI3.Sim.Weather;

/// <summary>
/// The weather over one province: the eight figures the original keeps and writes into a
/// savegame under <c>weather</c>. Nothing changes them yet - a new game has every one at
/// nothing - and what is read from them is in <see cref="CWeatherEffects"/>. See
/// <c>docs/mechanics/movement.md</c>, section 5.
/// </summary>
public sealed class CWeather
{
	public CFixedPoint Humidity { get; set; }

	public CFixedPoint Pressure { get; set; }

	public CFixedPoint WindSpeed { get; set; }

	/// <summary>Degrees.</summary>
	public CFixedPoint Temperature { get; set; }

	public CFixedPoint Precipitation { get; set; }

	/// <summary>How muddy the ground is, from 0 to 1.</summary>
	public CFixedPoint Muddyness { get; set; }

	/// <summary>How frozen the ground is, from 0 to 1. Any at all and mud counts for nothing.</summary>
	public CFixedPoint Frozen { get; set; }

	public CFixedPoint CloudCoverage { get; set; }
}
