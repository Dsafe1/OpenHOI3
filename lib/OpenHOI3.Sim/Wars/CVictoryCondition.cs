using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// One of a faction's victory conditions: its name, its trigger, whether it is primary, whether it was chosen for the
/// game, whether it is met today and the hour it last began to be. See <c>docs/mechanics/factions.md</c>, section 8.
/// </summary>
public sealed class CVictoryCondition
{
	public CVictoryCondition(string name, CTrigger trigger, bool primary)
	{
		Name = name;
		Trigger = trigger;
		Primary = primary;
	}

	public string Name { get; }

	/// <summary>The conditions that must hold, asked with no country in scope.</summary>
	public CTrigger Trigger { get; }

	public bool Primary { get; }

	public bool Chosen { get; set; }

	/// <summary>Whether the trigger held at the last day's test.</summary>
	public bool Met { get; set; }

	/// <summary>The hour it last began to hold; none until it first does.</summary>
	public long? Date { get; set; }
}
