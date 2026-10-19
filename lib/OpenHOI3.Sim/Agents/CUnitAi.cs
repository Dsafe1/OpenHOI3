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

	/// <summary>
	/// The game hour its last pass ran in. Ours: a game entered runs the entry pass twice at one hour - as its first days
	/// end and as the simulation starts - and the original's AI passes once.
	/// </summary>
	public long LastPassHour { get; set; } = -1;

	/// <summary>Its pass has run to its end once: the original's <c>unit_pass_done</c>, which the balancing of divisions waits for.</summary>
	public bool UnitPassDone { get; set; }

	/// <summary>A whole stance pass has run: from then on the stances are asked once a day.</summary>
	public bool StancesSettled { get; set; }

	/// <summary>The provinces its friends ask it to take, at war.</summary>
	public List<int> PriorityProvinces { get; } = new();

	/// <summary>The rebels hold a province it owns: the original's <c>provinces_in_revolt</c> above nought, at the pass's start.</summary>
	public bool InRevolt { get; set; }

	/// <summary>Its invasions.</summary>
	public List<CInvasion> Invasions { get; } = new();

	/// <summary>The areas it will not try to invade again this month.</summary>
	public List<Supply.CSupplyArea> FailedAreas { get; } = new();

	/// <summary>The areas it may invade or reinforce by sea, best first.</summary>
	public List<(Supply.CSupplyArea Area, long Score)> RankedAreas { get; } = new();
}
