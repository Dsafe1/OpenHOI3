using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>A country's sharing ships out among the convoys by itself turned the other way. See <c>docs/mechanics/convoywindows.md</c>, section 5.</summary>
public sealed class CToggleAutoMaintainConvoysCommand : CCommand
{
	private readonly CCountryTag _country;

	public CToggleAutoMaintainConvoysCommand(CCountryTag country)
	{
		_country = country;
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(_country) is { } country)
		{
			country.AutoMaintainConvoys = !country.AutoMaintainConvoys;
		}
	}
}
