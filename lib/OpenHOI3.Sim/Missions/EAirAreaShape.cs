namespace OpenHOI3.Sim.Missions;

/// <summary>
/// How the air orders window makes a mission's provinces, by the original's numbers for
/// them. Nothing makes a 1. See <c>docs/mechanics/aircombat.md</c>, section 2.
/// </summary>
public enum EAirAreaShape
{
	/// <summary>The window's province, and what shift adds.</summary>
	Point = 0,

	/// <summary>Every province joined to the window's within a range of it.</summary>
	Circle = 2,

	/// <summary>The circle cut to an arc of bearings from the window's province.</summary>
	Cone = 3,

	/// <summary>The window's province's first region.</summary>
	Region = 4,
}
