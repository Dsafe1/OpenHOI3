using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Content.History;

/// <summary>
/// What <c>history/diplomacy</c> gives a new game besides its puppets: the alliances and guarantees
/// standing on the start date, and every <c>relation</c> entry, whose dates the simulation applies
/// (<c>docs/mechanics/diplomacy.md</c>, section 2).
/// </summary>
public sealed class CDiplomacyHistory
{
	public List<CAlliance> Alliances { get; } = new();

	public List<CGuarantee> Guarantees { get; } = new();

	public List<CHistoryRelation> Relations { get; } = new();
}
