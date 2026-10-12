using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// An event's <c>option</c>, or its <c>immediate</c>, which is read as one: a name, the AI's
/// chance of taking it, and its effects in file order. See <c>docs/mechanics/script.md</c>,
/// section 3.
/// </summary>
public sealed class CEventOption
{
	/// <summary>A localisation key; empty where none is written.</summary>
	public string Name { get; set; } = "";

	public CMeanTimeToHappen AiChance { get; } = new();

	public List<CEffect> Effects { get; } = new();
}
