using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Organized Revolt, the politics screen's <c>revolt</c>: partisans from each of a government in exile's undergrounds
/// in its own land that is ready. Valid only for an exile. See <c>docs/mechanics/undergrounds.md</c>, section 6.
/// </summary>
public sealed class CSpawnFullRevoltCommand : CCommand
{
	public CSpawnFullRevoltCommand(CCountryTag country)
	{
		Country = country;
	}

	public CCountryTag Country { get; }

	public override bool MayExecute(CGameState state) => state.Country(Country) is { InExile: true };

	public override void Execute(CGameState state)
	{
		if (state.Country(Country) is { } country)
		{
			CUndergrounds.OrganizedRevolt(state, country);
		}
	}
}
