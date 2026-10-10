namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What a battle is fought between. The original has six kinds - these two, the battle
/// in the air and three of bombing - and the rest are not built.
/// </summary>
public enum ECombatKind
{
	/// <summary>Land units, for a land province. See <c>docs/mechanics/landcombat.md</c>.</summary>
	Land,

	/// <summary>Fleets, in a sea province. See <c>docs/mechanics/navalcombat.md</c>.</summary>
	Naval,
}
