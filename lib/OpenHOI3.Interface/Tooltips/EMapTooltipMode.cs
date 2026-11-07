namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The map modes a province's tooltip has a part for here: the original's modes 0 (and 13),
/// 1, 4 and 18, and since 2026-11-07 the rest. See <c>docs/mechanics/tooltips.md</c>, section 7,
/// and <c>docs/mechanics/mapmodes.md</c>.
/// </summary>
public enum EMapTooltipMode
{
	Terrain,
	Political,
	Supply,
	Air,
	/// <summary>The revolt map mode, which adds nothing of its own to the province's tooltip.</summary>
	Revolt,
	/// <summary>The weather map mode, the original's 11, whose part takes the place of the province's name.</summary>
	Weather,
	Infrastructure,
	VictoryPoints,
	/// <summary>The strength map mode, which adds nothing of its own.</summary>
	Strength,
	Resources,
	/// <summary>The simplified terrain map mode, which adds the terrain mode's part.</summary>
	SimplifiedTerrain,
	Region,
	Diplomatic,
	Intel,
	Theatre,
	Naval,
}
