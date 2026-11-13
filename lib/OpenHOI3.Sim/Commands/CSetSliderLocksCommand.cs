using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The production screen's slider locks written into the country, as the original's screen writes its mirror each frame
/// it is open. Nothing in the game reads them; they are what the screens are made from and what a save keeps for the
/// played country (<c>docs/mechanics/sliders.md</c>, section 3). The original sends no command - its mirror is one
/// client's - but nothing changes the game here other than by one.
/// </summary>
public sealed class CSetSliderLocksCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int[] _locks;

	public CSetSliderLocksCommand(CCountryTag country, int[] locks)
	{
		_country = country;
		_locks = (int[])locks.Clone();
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null;
	}

	public override void Execute(CGameState state)
	{
		state.Country(_country)!.SliderLocks = (int[])_locks.Clone();
	}
}
