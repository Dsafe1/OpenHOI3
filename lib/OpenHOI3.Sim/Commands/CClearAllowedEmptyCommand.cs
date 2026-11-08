using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit made empty for the reorganise window no longer allowed to stand so, once the window shuts with brigades in it:
/// if it is ever emptied again the hour will take it. The original writes this from the window; here it is a command. See
/// <c>docs/mechanics/reorganise.md</c>, section 5.
/// </summary>
public sealed class CClearAllowedEmptyCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;

	public CClearAllowedEmptyCommand(CCountryTag owner, int unit)
	{
		_owner = owner;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state) => CUnitFinder.Find(state, _owner, _unit) is not null;

	public override void Execute(CGameState state)
	{
		CUnitFinder.Find(state, _owner, _unit)!.IsAllowedEmpty = false;
	}
}
