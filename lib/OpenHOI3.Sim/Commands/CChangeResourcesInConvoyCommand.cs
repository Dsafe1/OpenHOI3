using System;
using OpenHOI3.Sim.Convoys;

namespace OpenHOI3.Sim.Commands;

/// <summary>Which goods a convoy carries, all seven at once. See <c>docs/mechanics/convoywindows.md</c>, section 5.</summary>
public sealed class CChangeResourcesInConvoyCommand : CCommand
{
	private readonly int _convoy;
	private readonly bool[] _carries;

	public CChangeResourcesInConvoyCommand(int convoy, bool[] carries)
	{
		_convoy = convoy;
		_carries = carries;
	}

	public override bool MayExecute(CGameState state) => CConvoyMaker.Find(state, _convoy) is not null;

	public override void Execute(CGameState state)
	{
		if (CConvoyMaker.Find(state, _convoy) is { } found)
		{
			Array.Copy(_carries, found.Convoy.Carries, Math.Min(_carries.Length, found.Convoy.Carries.Length));
		}
	}
}
