using OpenHOI3.Sim.Convoys;

namespace OpenHOI3.Sim.Commands;

/// <summary>A convoy cancelled: off the country's list, its ships back in the free pools. See <c>docs/mechanics/convoywindows.md</c>, section 5.</summary>
public sealed class CCancelConvoyCommand : CCommand
{
	private readonly int _convoy;

	public CCancelConvoyCommand(int convoy)
	{
		_convoy = convoy;
	}

	public override bool MayExecute(CGameState state) => CConvoyMaker.Find(state, _convoy) is not null;

	public override void Execute(CGameState state)
	{
		if (CConvoyMaker.Find(state, _convoy) is { } found)
		{
			CConvoyMaker.Remove(found.Owner, found.Convoy);
		}
	}
}
