using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Ticks or clears a played country's "Automate Trade", the diplomacy screen's checkbox. See
/// <c>docs/mechanics/trade.md</c>, section 12.
/// </summary>
public sealed class CSetAutomateTradeCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly bool _on;

	public CSetAutomateTradeCommand(CCountryTag country, bool on)
	{
		_country = country;
		_on = on;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null;
	}

	public override void Execute(CGameState state)
	{
		if (_on)
		{
			state.AutomateTrade.Add(_country);
		}
		else
		{
			state.AutomateTrade.Remove(_country);
		}
	}
}
