using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The AI's six production shares, the original's <c>CChangeInvestmentCommand</c>: lend-lease, consumer goods,
/// production, supplies, reinforcement and upgrades, made from the script's numbers as the Lua's constructor rounds
/// them (<c>CLuaShares</c>) and written as given - never refused, never normalised again. See <c>docs/mechanics/ministers.md</c>, section 5.
/// </summary>
public sealed class CChangeInvestmentCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly CFixed15[] _shares;

	public CChangeInvestmentCommand(CCountryTag country, System.ReadOnlySpan<CFixed15> shares)
	{
		_country = country;
		_shares = shares.ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && _shares.Length == CProduction.CategoryCount;
	}

	public override void Execute(CGameState state)
	{
		_shares.CopyTo(state.Country(_country)!.ProductionShares, 0);
	}
}
