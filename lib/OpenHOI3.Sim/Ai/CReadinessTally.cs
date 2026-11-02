using System.Collections.Generic;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// What the readiness test counts on a country's fronts facing a target: the front provinces with the strength of its own
/// fit land units on each, how many stand empty, and the enemy's land strength beside them, each province counted once.
/// See <c>docs/mechanics/foreignminister.md</c>, section 7.
/// </summary>
public sealed class CReadinessTally
{
	/// <summary>Each front province, with the strength of the country's own fit land units in it, in order met.</summary>
	public List<(int Province, long Ours)> Fronts { get; } = new();

	/// <summary>The target's provinces beside the front, each once, in order met.</summary>
	public List<int> EnemyProvinces { get; } = new();

	public int Empty { get; set; }

	public long Enemy { get; set; }

	public long Ours { get; set; }
}
