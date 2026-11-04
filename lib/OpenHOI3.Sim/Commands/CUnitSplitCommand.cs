using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit split into one unit for each brigade but its last, the new-unit button's with Ctrl held
/// (<c>docs/mechanics/unitpanel.md</c>, section 7.1). As the original's, it asks nothing of the button's rules; the new
/// units take the next numbers as it runs. The selection that follows is the client's.
/// </summary>
public sealed class CUnitSplitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;

	public CUnitSplitCommand(CCountryTag owner, int unit)
	{
		_owner = owner;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is { IsRemoved: false } && state.Country(_owner) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnitSplit.Split(state, state.Country(_owner)!, CUnitFinder.Find(state, _owner, _unit)!);
	}
}
