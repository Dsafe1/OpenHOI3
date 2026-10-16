using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The AI's research order, the original's <c>CStartResearchCommand</c>: a technology put at the end of a country's
/// queue. It is refused only where the technology is queued already - not where it may not be researched, nor for a
/// queue past the country's slots. See <c>docs/mechanics/ministers.md</c>, section 3.
/// </summary>
public sealed class CStartResearchCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly string _technology;

	public CStartResearchCommand(CCountryTag country, string technology)
	{
		_country = country;
		_technology = technology;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && state.Technologies.ContainsKey(_technology)
			&& !country.ResearchQueue.Contains(state.Technologies[_technology].Name);
	}

	public override void Execute(CGameState state)
	{
		state.Country(_country)!.ResearchQueue.Add(state.Technologies[_technology].Name);
	}
}
