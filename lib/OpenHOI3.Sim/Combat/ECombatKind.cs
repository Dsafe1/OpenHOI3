namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What a battle is fought between. The original has six kinds - these three and three
/// of bombing, which are not built.
/// </summary>
public enum ECombatKind
{
	/// <summary>Land units, for a land province. See <c>docs/mechanics/landcombat.md</c>.</summary>
	Land,

	/// <summary>Fleets, in a sea province. See <c>docs/mechanics/navalcombat.md</c>.</summary>
	Naval,

	/// <summary>Air units, over any province. See <c>docs/mechanics/aircombat.md</c>.</summary>
	Air,
}
