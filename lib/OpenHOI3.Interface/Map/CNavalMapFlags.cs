namespace OpenHOI3.Interface.Map;

/// <summary>
/// The naval map mode's four icons of a province, each whether the past week holds a naval battle of its kind there - never
/// how many. See <c>docs/mechanics/mapmodes.md</c>, section 11.
/// </summary>
/// <param name="ConvoysSunk">A convoy raid the player's side made.</param>
/// <param name="ConvoysRaided">A raid on the player's side's convoys.</param>
/// <param name="RaiderDefended">Any other naval battle, in which the player's side was attacked.</param>
/// <param name="RaiderAttacked">Any other naval battle, in which the player's side attacked.</param>
public readonly record struct CNavalMapFlags(bool ConvoysSunk, bool ConvoysRaided, bool RaiderDefended, bool RaiderAttacked);
