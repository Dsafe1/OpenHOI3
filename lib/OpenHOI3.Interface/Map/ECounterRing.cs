namespace OpenHOI3.Interface.Map;

/// <summary>
/// The ring a counter wears, by what its unit is to the one selected. See
/// <c>docs/mechanics/mapunits.md</c>, section 8.
/// </summary>
public enum ECounterRing
{
	None,

	/// <summary>The selected unit itself: white.</summary>
	Selected,

	/// <summary>The selected unit, where an AI commands it or a unit above it: the counter type's <c>color1</c>, green.</summary>
	SelectedUnderAi,

	/// <summary>A unit under the selected one, at any depth: the counter type's <c>color2</c>.</summary>
	Under,
}
