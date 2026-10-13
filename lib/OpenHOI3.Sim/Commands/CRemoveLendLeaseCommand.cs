using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Ends a giver's lend-lease to a receiver, posted when the rules no longer allow it: the receiver is told
/// the giver "revoked" it. See <c>docs/mechanics/aid.md</c>, section 3.
/// </summary>
public sealed class CRemoveLendLeaseCommand : CCommand
{
	private readonly CCountryTag _giver;
	private readonly CCountryTag _receiver;

	public CRemoveLendLeaseCommand(CCountryTag giver, CCountryTag receiver)
	{
		_giver = giver;
		_receiver = receiver;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_giver) is { } giver && state.Country(_receiver) is { } receiver
			&& Aid.CLendLease.Gives(giver, receiver);
	}

	public override void Execute(CGameState state)
	{
		CCountry giver = state.Country(_giver)!;
		CCountry receiver = state.Country(_receiver)!;
		Aid.CLendLease.Remove(giver, receiver);
		Agreements.CPacts.ToldTurnedRound(state, giver, receiver, "LENDREVOKE");
	}
}
