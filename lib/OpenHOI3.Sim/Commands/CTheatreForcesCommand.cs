using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The order of battle browser's Theatre Forces: a country's whole order of battle sorted into the theatres its units
/// stand in, by the same sort the AI's pass runs. The original calls it straight from the button; here it is a command,
/// as every change to the game is. See <c>docs/mechanics/oobbrowser.md</c>, section 3.
/// </summary>
public sealed class CTheatreForcesCommand : CCommand
{
	private readonly CCountryTag _owner;

	public CTheatreForcesCommand(CCountryTag owner)
	{
		_owner = owner;
	}

	public override bool MayExecute(CGameState state) => state.Country(_owner) is not null;

	public override void Execute(CGameState state)
	{
		// By each unit's way alone: a command runs on every machine, and what the AI sent a unit to in its pass is only
		// the AI's machine's to know (docs/multiplayer.md, section 3).
		CTheatreSort.Sort(state, state.Country(_owner)!, byWayAlone: true);
	}
}
