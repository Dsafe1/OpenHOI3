using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// <see cref="Influencer"/> influencing <see cref="Influenced"/> towards its faction, by the faction's
/// influence modifier, which the influenced country carries while it runs.
/// </summary>
public sealed record CInfluence(CCountryTag Influencer, CCountryTag Influenced, string Faction, long Start)
{
	/// <summary>Its number among the pacts (<see cref="CGameState.NextPact"/>).</summary>
	public long Made { get; init; }
}
