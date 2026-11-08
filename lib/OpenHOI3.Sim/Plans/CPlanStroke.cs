using System.Collections.Generic;

namespace OpenHOI3.Sim.Plans;

/// <summary>
/// An arrow or a border of a battle plan: its points about its own origin, in map pixels. A plan is the player's drawing
/// and nothing in the simulation reads it. See <c>docs/mechanics/battleplans.md</c>, section 6.
/// </summary>
public sealed class CPlanStroke
{
	public string Type { get; set; } = "";

	public int Colour { get; set; }

	public float OriginX { get; set; }

	public float OriginY { get; set; }

	public List<(float X, float Y)> Points { get; } = new();

	/// <summary>The words along an arrow; a border's is never set.</summary>
	public string Label { get; set; } = "";

	/// <summary>How many of the points were taken from the arrow it branched off, less one; -1, none.</summary>
	public int SnapPrefix { get; set; } = -1;

	/// <summary>How many points the arrow it branched off had. Not saved.</summary>
	public int SnapParentPoints { get; set; }

	public bool IsSelected { get; set; }
}
