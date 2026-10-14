using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// For trying things out: sets the money in a country's stockpile, there and then - so that a law may be paid for on a
/// new game, where none can be (<c>docs/mechanics/politics.md</c>, section 11). Nothing in a game posts it.
/// </summary>
public sealed class CSetMoneyCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly CFixedPoint _money;

	public CSetMoneyCommand(CCountryTag country, CFixedPoint money)
	{
		_country = country;
		_money = money;
	}

	public override bool MayExecute(CGameState state) => state.Country(_country) is { } country && state.Stockpile(country) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(_country) is { } country && state.Stockpile(country) is { } pool)
		{
			pool[EGoods.Money] = _money;
		}
	}
}
