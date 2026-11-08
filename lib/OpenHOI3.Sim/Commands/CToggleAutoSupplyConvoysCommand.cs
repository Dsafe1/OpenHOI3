using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>A country's making and unmaking supply convoys by itself turned the other way. See <c>docs/mechanics/convoywindows.md</c>, section 5.</summary>
public sealed class CToggleAutoSupplyConvoysCommand : CCommand
{
	private readonly CCountryTag _country;

	public CToggleAutoSupplyConvoysCommand(CCountryTag country)
	{
		_country = country;
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(_country) is { } country)
		{
			country.AutoSupplyConvoys = !country.AutoSupplyConvoys;
		}
	}
}
