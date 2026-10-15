using System.Collections.Generic;

namespace OpenHOI3.Sim.Weather;

/// <summary>
/// A weather front: a patch of low pressure born at one of the map's <c>low_pressure_zones</c>, carrying humidity, low
/// pressure, wind and warmth across the provinces it steps into. The original's <c>CWeatherFront</c>; see
/// <c>docs/mechanics/weather.md</c>, section 3.
/// </summary>
public sealed class CWeatherFront
{
	public CWeatherFront(int home, int direction, int rotation)
	{
		Home = home;
		Direction = direction;
		Rotation = rotation;
		Provinces.Add(home);
	}

	/// <summary>The province it was born at.</summary>
	public int Home { get; }

	/// <summary>Which way it goes, in thousandths of a turn as a bearing is; never brought back into 0 to 1000.</summary>
	public int Direction { get; set; }

	/// <summary>What is added to its direction at each move, four fifths of it kept for the next.</summary>
	public int Rotation { get; set; }

	/// <summary>Hours to its next move: the original's <c>speed</c>. Nought when born, so it moves at once.</summary>
	public int MoveCountdown { get; set; }

	/// <summary>The provinces it covers, in the order it took them.</summary>
	public List<int> Provinces { get; } = new();
}
