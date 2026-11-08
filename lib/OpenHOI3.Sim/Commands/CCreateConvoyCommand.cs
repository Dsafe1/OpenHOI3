using OpenHOI3.Sim.Convoys;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A convoy made by hand between two ports, for whoever holds the first: the original's <c>CCreateConvoyCommand</c>, which asks nothing. It starts with no ships and waits for the sharing
/// out. See <c>docs/mechanics/convoywindows.md</c>, section 5.
/// </summary>
public sealed class CCreateConvoyCommand : CCommand
{
	private readonly int _from;
	private readonly int _to;
	private readonly bool _supply;

	public CCreateConvoyCommand(int from, int to, bool supply)
	{
		_from = from;
		_to = to;
		_supply = supply;
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(state.Province(_from).Controller) is { } country)
		{
			CConvoyMaker.Create(state, country, _from, _to, _supply);
		}
	}
}
