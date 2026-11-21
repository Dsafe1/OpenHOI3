namespace OpenHOI3.Interface.Map;

/// <summary>
/// The ring a counter wears, by what its unit is to the one selected. The original has
/// one more, in the counter type's <c>color1</c>, for a selected unit an AI commands,
/// which is not drawn here. See <c>docs/mechanics/mapunits.md</c>, section 8.
/// </summary>
public enum ECounterRing
{
	None,

	/// <summary>The selected unit itself: white.</summary>
	Selected,

	/// <summary>A unit under the selected one, at any depth: the counter type's <c>color2</c>.</summary>
	Under,
}
