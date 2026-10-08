namespace OpenHOI3.Sim;

/// <summary>
/// A country setting how its IC is divided between the six things it is spent on. The
/// six are given together, since moving one moves the others.
/// </summary>
public sealed class CSetProductionSharesCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly CFixed15[] _shares;

	public CSetProductionSharesCommand(CCountryTag country, System.ReadOnlySpan<CFixed15> shares)
	{
		_country = country;
		_shares = shares.ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && CProduction.AreValidShares(_shares);
	}

	public override void Execute(CGameState state)
	{
		_shares.CopyTo(state.Country(_country)!.ProductionShares, 0);
	}
}
