using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// A country's front in one block of its land: a run of provinces facing one enemy, or all of them at war. The original's
/// <c>CAreaBorder</c>, saved as a theatre's <c>front</c>. See <c>docs/mechanics/theatres.md</c>, section 3.
/// </summary>
public sealed class CFront
{
	/// <summary>Its provinces, in the order they were put on it.</summary>
	public List<int> Provinces { get; } = new();

	/// <summary>Whom its provinces face: the original's <c>local_enemy</c>. Null where they face nobody.</summary>
	public CCountryTag? Enemy { get; set; }

	/// <summary>The country is at war across it: the original's <c>front</c>.</summary>
	public bool IsHot { get; set; }

	/// <summary>For a hot front, the area it faces; null otherwise, and where it faces none.</summary>
	public CSupplyArea? Faces { get; set; }

	public override string ToString()
	{
		return $"{Enemy?.Value ?? "---"}{(IsHot ? " hot" : "")}: {string.Join(" ", Provinces)}";
	}
}
