using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Sets the weights a giver shares its lend-lease out by, each receiver's as given and not made to sum to
/// anything: the lend-lease window's sliders, posted whole. See <c>docs/mechanics/aid.md</c>, section 6.
/// </summary>
public sealed class CChangeLendLeaseDistributionCommand : CCommand
{
	private readonly CCountryTag _giver;
	private readonly (CCountryTag Receiver, long Weight)[] _weights;

	public CChangeLendLeaseDistributionCommand(CCountryTag giver, IEnumerable<(CCountryTag Receiver, long Weight)> weights)
	{
		_giver = giver;
		_weights = weights.ToArray();
	}

	public override bool MayExecute(CGameState state) => state.Country(_giver) is not null;

	public override void Execute(CGameState state)
	{
		CCountry giver = state.Country(_giver)!;
		foreach ((CCountryTag receiver, long weight) in _weights)
		{
			if (giver.LendLeaseGiving.Find(g => g.Receiver == receiver) is { } gift)
			{
				gift.Weight = weight;
			}
		}
	}
}
