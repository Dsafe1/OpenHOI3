using OpenHOI3.Sim.Convoys;

namespace OpenHOI3.Sim.Commands;

/// <summary>A number of transports moved between a convoy and the free pool; a negative amount gives them back. Refused where there are not that many to move. See <c>docs/mechanics/convoywindows.md</c>, section 5.</summary>
public sealed class CChangeTransportsInConvoyCommand : CCommand
{
	private readonly int _convoy;
	private readonly int _amount;

	public CChangeTransportsInConvoyCommand(int convoy, int amount)
	{
		_convoy = convoy;
		_amount = amount;
	}

	public override bool MayExecute(CGameState state)
	{
		if (CConvoyMaker.Find(state, _convoy) is not { } found)
		{
			return false;
		}
		return _amount > 0 ? found.Owner.Transports >= _amount : found.Convoy.Transports >= -_amount;
	}

	public override void Execute(CGameState state)
	{
		if (CConvoyMaker.Find(state, _convoy) is { } found)
		{
			found.Convoy.Transports += _amount;
			found.Owner.Transports -= _amount;
		}
	}
}
