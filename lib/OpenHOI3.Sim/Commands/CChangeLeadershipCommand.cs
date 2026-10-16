using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The AI's four leadership shares, the original's <c>CChangeLeadershipCommand</c>: officers, diplomacy, espionage and
/// research, made from the script's numbers as the Lua's constructor rounds them (<c>CLuaShares</c>) and written as
/// given - never refused, never normalised again, so they may sum a little over the whole. See <c>docs/mechanics/ministers.md</c>, section 3.
/// </summary>
public sealed class CChangeLeadershipCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly CFixed15[] _shares;

	public CChangeLeadershipCommand(CCountryTag country, System.ReadOnlySpan<CFixed15> shares)
	{
		_country = country;
		_shares = shares.ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && _shares.Length == CLeadership.CategoryCount;
	}

	public override void Execute(CGameState state)
	{
		_shares.CopyTo(state.Country(_country)!.LeadershipShares, 0);
	}
}
