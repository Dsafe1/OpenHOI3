namespace OpenHOI3.Sim.Movement;

/// <summary>
/// How a unit's way keeps clear of the enemy, short of its goal - a fleet or an air unit of enemy ships - the original's <c>CPathFind</c>,
/// <c>CSafePathFind</c> and <c>CVerySafePathFind</c>. See <c>docs/mechanics/movement.md</c>, section 3.
/// </summary>
public enum ERouteSafety
{
	/// <summary>Anywhere the unit may step.</summary>
	Plain,

	/// <summary>Not through a province where any hostile land or air unit stands.</summary>
	Safe,

	/// <summary>Not through a province held by a country at war with it, nor one with a land battle in it.</summary>
	VerySafe,
}
