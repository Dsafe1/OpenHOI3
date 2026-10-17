using System.Collections.Generic;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What a country's AI keeps of its units: its agents and the flags of its hourly pass. The unit half of the original's
/// <c>CEU3AI</c>. See <c>docs/mechanics/agents.md</c>, section 2.
/// </summary>
public sealed class CUnitAi
{
	public List<CAgent> Agents { get; } = new();

	/// <summary>Whether the AI runs its units: cleared for the played country at its first pass.</summary>
	public bool RunsUnits { get; set; } = true;

	/// <summary>Waiting for every theatre to have a headquarters: the original's <c>theatres_changed</c>.</summary>
	public bool Waiting { get; set; } = true;

	/// <summary>Its pass has run to its end once: the original's <c>unit_pass_done</c>, which the balancing of divisions waits for.</summary>
	public bool UnitPassDone { get; set; }

	/// <summary>A whole stance pass has run: from then on the stances are asked once a day.</summary>
	public bool StancesSettled { get; set; }

	/// <summary>The provinces its friends ask it to take, at war: the original's +0x118.</summary>
	public List<int> PriorityProvinces { get; } = new();
}
