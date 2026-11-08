using System.Collections.Generic;

namespace OpenHOI3.Sim.Plans;

/// <summary>
/// A battle plan: a title, the country it is of and what is drawn on it. It is the player's own drawing, kept by the
/// interface as the original keeps it, and nothing in the simulation reads it - so it changes without commands. A plan
/// listed from a file and not loaded holds its header alone. See <c>docs/mechanics/battleplans.md</c>, section 6.
/// </summary>
public sealed class CBattlePlan
{
	public string Title { get; set; } = "?";

	public string Country { get; set; } = "---";

	/// <summary>Whether it is drawn on the map; a <c>.pln</c> does not hold it, a saved game does.</summary>
	public bool IsVisible { get; set; } = true;

	public bool IsShared { get; set; }

	/// <summary>A whole plan, as against a row of a file listed by its header.</summary>
	public bool IsLive { get; set; }

	public bool IsDirty { get; set; }

	/// <summary>Where it was listed from; empty for a plan of the game.</summary>
	public string FilePath { get; set; } = "";

	public bool HeaderGood { get; set; }

	public List<CPlanStroke> Arrows { get; } = new();

	public List<CPlanStroke> Borders { get; } = new();

	public List<CPlanIcon> Icons { get; } = new();

	public List<CPlanText> Texts { get; } = new();
}
