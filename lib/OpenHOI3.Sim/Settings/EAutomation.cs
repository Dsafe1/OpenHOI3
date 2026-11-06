namespace OpenHOI3.Sim.Settings;

/// <summary>
/// The player's areas the AI may be handed, in the order of the original's automation flags - a save's top-level
/// <c>ai</c> list. See <c>docs/mechanics/frontend.md</c>, section 4.
/// </summary>
public enum EAutomation
{
	Diplomacy = 0,
	Production = 1,
	Technology = 2,
	Politics = 3,
	Intelligence = 4,

	/// <summary>The Armies setting's place, whose list entry is the Armies level and which nothing reads in play.</summary>
	Armies = 5,

	/// <summary>Placing a finished unit by itself, the production screen's <c>auto_deployment_checkbox</c>.</summary>
	Deployment = 6,
}
