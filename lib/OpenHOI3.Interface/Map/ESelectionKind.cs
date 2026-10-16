namespace OpenHOI3.Interface.Map;

/// <summary>
/// The kinds of unit a selection may hold, one kind at a time: land units of every level, fleets, air units. See
/// <c>docs/mechanics/selection.md</c>, section 1.
/// </summary>
public enum ESelectionKind
{
	Land,
	Fleet,
	Air,
}
