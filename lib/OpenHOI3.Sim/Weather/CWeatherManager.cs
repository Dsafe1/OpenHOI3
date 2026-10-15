using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Weather;

/// <summary>
/// The weather the game holds beside its provinces': the map's pressure zones, the live fronts in the order they tick,
/// and for each low pressure zone a cooldown and the heading its last front set off on. The original's
/// <c>CWeatherManager</c>; see <c>docs/mechanics/weather.md</c>, section 3.
/// </summary>
public sealed class CWeatherManager
{
	/// <summary><c>low_pressure_zones</c>, in the file's order: where fronts are born, and the climates they are born in.</summary>
	public IReadOnlyList<(int Province, int Mask)> LowPressureZones { get; set; } = Array.Empty<(int, int)>();

	/// <summary><c>high_pressure_zones</c>, in the file's order.</summary>
	public IReadOnlyList<int> HighPressureZones { get; set; } = Array.Empty<int>();

	/// <summary>The live fronts: the oldest first, a new one at the end.</summary>
	public List<CWeatherFront> Fronts { get; } = new();

	/// <summary>
	/// One a zone, hours until a front may be born there; it counts down without a floor. Empty until the first hour of
	/// weather fills it. Not saved, and never put back to nought in an ordinary game.
	/// </summary>
	public List<int> Cooldowns { get; } = new();

	/// <summary>One a zone, the direction its last front set off in, or nought for "draw one". Filled with the cooldowns.</summary>
	public List<int> Headings { get; } = new();
}
