namespace OpenHOI3.Sim.Script;

/// <summary>
/// Why a condition answers false without being asked, or an effect does nothing:
/// <c>docs/mechanics/triggers.md</c>, section 8, and <c>docs/mechanics/effects.md</c>.
/// </summary>
public enum EScriptGap
{
	/// <summary>It is about something the simulation does not hold yet.</summary>
	Unbuilt,

	/// <summary>What the original does was not read.</summary>
	Unread,

	/// <summary>It asks about a battle, and there is none.</summary>
	BattleOnly,
}
