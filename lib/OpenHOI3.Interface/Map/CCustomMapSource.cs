using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// One thing a custom map mode shades every province by - an enhanced game's: a building's level, a resource's amount
/// or a supply figure. See <c>docs/mechanics/custommapmodes.md</c>, section 3.
/// </summary>
public sealed class CCustomMapSource
{
	public required ECustomMapKind Kind { get; init; }

	/// <summary>Its name among its kind's, which a setting or a switch names it by: a building's key, <c>energy</c>, <c>days_of_supplies</c>.</summary>
	public required string Key { get; init; }

	/// <summary>The localisation key of what the list and the legend call it.</summary>
	public required string Label { get; init; }

	/// <summary>The localisation key of what the legend says of it, or empty.</summary>
	public string Explanation { get; init; } = "";

	public required ECustomMapScale Scale { get; init; }

	public ECustomMapUnit Unit { get; init; } = ECustomMapUnit.Amount;

	/// <summary>The ten start values of a <see cref="ECustomMapScale.Bands"/> source, lowest first, in the value's own units.</summary>
	public long[] Bands { get; init; } = Array.Empty<long>();

	/// <summary>Whether the shades run the other way: the bottom shade is the one to worry about either way.</summary>
	public bool HigherIsWorse { get; init; }

	/// <summary>Whether nothing of it is shown of a province the player does not see into, not even that there is something.</summary>
	public bool HiddenUnseen { get; init; }

	/// <summary>A province's value, or none where it has nothing to show.</summary>
	public required Func<CGameState, CProvince, long?> Value { get; init; }
}
