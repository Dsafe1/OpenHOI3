using OpenHOI3.Sim.Convoys;

namespace OpenHOI3.Sim.Commands;

/// <summary>A number of escorts moved between a convoy and the free pool; a negative amount gives them back. Refused where there are not that many to move. See <c>docs/mechanics/convoywindows.md</c>, section 5.</summary>
public sealed class CChangeEscortsInConvoyCommand : CCommand
{
	private readonly int _convoy;
	private readonly int _amount;

	public CChangeEscortsInConvoyCommand(int convoy, int amount)
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
		return _amount > 0 ? found.Owner.Escorts >= _amount : found.Convoy.Escorts >= -_amount;
	}

	public override void Execute(CGameState state)
	{
		if (CConvoyMaker.Find(state, _convoy) is { } found)
		{
			found.Convoy.Escorts += _amount;
			found.Owner.Escorts -= _amount;
		}
	}
}
