using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// A country's offers waiting for an answer cut down to those kept: what the AI's answering leaves of its list once it
/// has answered - a further trade its draw would have taken, kept for the next hour (<c>docs/mechanics/trade.md</c>,
/// section 2). The AI's step runs it at once (<see cref="CGameState.DoInStep"/>), so that a machine playing the step
/// back, which answers nothing itself, has its lists emptied as the answering machine's are
/// (<c>docs/multiplayer.md</c>, section 3).
/// </summary>
public sealed class CKeepOffersCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly List<COffer> _kept;

	public CKeepOffersCommand(CCountryTag country, List<COffer> kept)
	{
		_country = country;
		_kept = kept;
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(_country) is not { } country)
		{
			return;
		}
		country.PendingOffers.Clear();
		country.PendingOffers.AddRange(_kept);
	}
}
