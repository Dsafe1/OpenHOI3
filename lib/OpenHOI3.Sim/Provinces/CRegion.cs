using System.Collections.Generic;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A region of <c>map/region.txt</c>: a name and the provinces it lists, in the file's
/// order. A province may be in several, and its first is the one an air mission's
/// region shape takes. See <c>docs/mechanics/aircombat.md</c>, section 2.
/// </summary>
public sealed class CRegion
{
	public CRegion(string name, IReadOnlyList<int> provinces, bool useForPeace, bool aiPriority = false)
	{
		Name = name;
		Provinces = provinces;
		UseForPeace = useForPeace;
		AiPriority = aiPriority;
	}

	public string Name { get; }

	/// <summary>The ids of its provinces, in the order the file lists them.</summary>
	public IReadOnlyList<int> Provinces { get; }

	/// <summary>The word <c>peace</c> in its block. What reads it is not built.</summary>
	public bool UseForPeace { get; }

	/// <summary>
	/// The word <c>ai_prio</c> in its block: each province of it scores 100 more as a place in the AI's line
	/// (<c>docs/mechanics/moving.md</c>, section 3).
	/// </summary>
	public bool AiPriority { get; }
}
