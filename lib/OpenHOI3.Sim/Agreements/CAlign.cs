using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// <see cref="Aligner"/> aligning itself towards <see cref="Leader"/>'s faction, by the faction's
/// influence modifier, which the aligner carries while it runs.
/// </summary>
public sealed record CAlign(CCountryTag Aligner, CCountryTag Leader, string Faction, long Start)
{
	/// <summary>Its number among the pacts (<see cref="CGameState.NextPact"/>).</summary>
	public long Made { get; init; }
}
