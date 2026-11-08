using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A brigade, a ship or a wing given a new name, the old one given up from its country's names in use and the new one
/// taken. A ship that has left the game keeps its number and may still be named. See
/// <c>docs/mechanics/brigadecard.md</c>, section 3.
/// </summary>
public sealed class CRenameSubUnitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _subUnit;
	private readonly string _name;

	public CRenameSubUnitCommand(CCountryTag owner, int subUnit, string name)
	{
		_owner = owner;
		_subUnit = subUnit;
		_name = name;
	}

	private CSubUnit? Find(CGameState state)
	{
		return CUnitFinder.FindSubUnit(state, _owner, _subUnit) ?? CSunkShips.Find(state, _subUnit);
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_owner) is not null && Find(state) is not null;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_owner)!;
		CSubUnit subUnit = Find(state)!;
		country.UnitNamesInUse.Remove(subUnit.Name);
		subUnit.Name = _name;
		country.UnitNamesInUse.Add(_name);
	}
}
