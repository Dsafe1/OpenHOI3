namespace OpenHOI3.Sim;

/// <summary>
/// A country setting how its leadership is divided between officers, diplomacy,
/// espionage and research. The four are given together, since moving one moves the others.
/// </summary>
public sealed class CSetLeadershipSharesCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly CFixed15[] _shares;

	public CSetLeadershipSharesCommand(CCountryTag country, System.ReadOnlySpan<CFixed15> shares)
	{
		_country = country;
		_shares = shares.ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && CLeadership.AreValidShares(_shares);
	}

	public override void Execute(CGameState state)
	{
		_shares.CopyTo(state.Country(_country)!.LeadershipShares, 0);
	}
}
