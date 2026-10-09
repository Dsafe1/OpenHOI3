namespace OpenHOI3.Interface.Map;

/// <summary>
/// Which of a province's five stacks a unit's counter is in: the original sorts the
/// units in a province by what they are doing and stands each kind in its own place.
/// See <c>docs/mechanics/mapunits.md</c>, section 3.
/// </summary>
public enum EMapStack
{
	Standing,
	Moving,
	Attacking,
	Defending,
	Air,
}
