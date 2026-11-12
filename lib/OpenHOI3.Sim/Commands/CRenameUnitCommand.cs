using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit given a new name by the player, from the unit panel's name box: the old name given up from its owner's names in
/// use, the new one taken. It tests nothing but that the unit is still there - no owner, no name in use, no length - as
/// the original's does (<c>reversing/findings/FINDINGS-unitrename.md</c>, sections 1, 2 and 4; <c>docs/mechanics/unitpanel.md</c>,
/// section 3).
/// </summary>
public sealed class CRenameUnitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly string _name;

	public CRenameUnitCommand(CCountryTag owner, int unit, string name)
	{
		_owner = owner;
		_unit = unit;
		_name = name;
	}

	/// <summary>A unit that has left the game is renamed by nobody: the original drops the command.</summary>
	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		// The names are the unit's owner's, shared with its brigades, ships and wings; giving one up frees it whoever else
		// bears it, as the original's register takes every entry of it away.
		if (state.Country(unit.Owner) is { } owner)
		{
			owner.UnitNamesInUse.Remove(unit.Name);
			unit.Name = _name;
			owner.UnitNamesInUse.Add(_name);
		}
		else
		{
			unit.Name = _name;
		}
	}
}
