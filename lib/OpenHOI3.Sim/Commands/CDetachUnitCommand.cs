using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit taken out from under its commander to the top of its country's order of battle, keeping what is under it. It
/// asks nothing, and the player is told of his own. See <c>docs/mechanics/attaching.md</c>, section 3.
/// </summary>
public sealed class CDetachUnitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;

	public CDetachUnitCommand(CCountryTag owner, int unit)
	{
		_owner = owner;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state) => CUnitFinder.Find(state, _owner, _unit) is not null;

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		CAgentUnits.Detach(state, unit);
		if (state.Played.Contains(unit.Owner))
		{
			state.RaisePactMessage(new CPactMessage("DETACHEDUNIT", "", "", unit.Owner, unit.Owner)
			{
				Told = unit.Owner,
				Province = unit.Location,
				Values = new[] { ("UNIT", unit.Name) },
			});
		}
	}
}
