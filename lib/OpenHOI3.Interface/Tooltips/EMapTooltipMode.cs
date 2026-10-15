namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The map modes a province's tooltip has a part for here: the original's modes 0 (and 13),
/// 1, 4 and 18. See <c>docs/mechanics/tooltips.md</c>, section 7.
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
}
