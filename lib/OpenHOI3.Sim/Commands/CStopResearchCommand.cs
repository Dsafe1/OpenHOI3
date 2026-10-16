using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A technology taken off a country's queue, its progress kept: the original's <c>CStopResearchCommand</c>, which a
/// tech minister posts when a level of a technology of several levels is researched. Refused where the technology is
/// not queued. See <c>docs/mechanics/ministers.md</c>, section 2.
/// </summary>
public sealed class CStopResearchCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly string _technology;

	public CStopResearchCommand(CCountryTag country, string technology)
	{
		_country = country;
		_technology = technology;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && country.ResearchQueue.Contains(_technology);
	}

	public override void Execute(CGameState state)
	{
		state.Country(_country)!.ResearchQueue.Remove(_technology);
	}
}
