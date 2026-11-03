namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The outliner's nine sections, in the order the list shows them; the numbers are the original's, and what its
/// <c>settings.txt</c> key <c>outliner</c> lists. See <c>docs/mechanics/outliner.md</c>, section 2.
/// </summary>
public enum EOutlinerSection
{
	LandCombat = 1,
	NavalCombat = 2,
	AirCombat = 3,
	LandUnits = 4,
	NavalUnits = 5,
	AirUnits = 6,
	AlliedObjectives = 7,
	OurBombings = 8,
	HostileBombing = 9,
}
