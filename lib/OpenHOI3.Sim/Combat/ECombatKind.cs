namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What a battle is fought between. The original has these six kinds, in this order from 1.
/// </summary>
public enum ECombatKind
{
	/// <summary>Land units, for a land province. See <c>docs/mechanics/landcombat.md</c>.</summary>
	Land,

	/// <summary>Fleets, in a sea province. See <c>docs/mechanics/navalcombat.md</c>.</summary>
	Naval,

	/// <summary>Air units, over any province. See <c>docs/mechanics/aircombat.md</c>.</summary>
	Air,

	/// <summary>
	/// Bombers against the ground of a land province an enemy holds: its buildings, and
	/// the aircraft at their base in it. See <c>docs/mechanics/bombing.md</c>.
	/// </summary>
	GroundBombing,

	/// <summary>Bombers against land units.</summary>
	LandBombing,

	/// <summary>Bombers against ships.</summary>
	NavalBombing,
}
