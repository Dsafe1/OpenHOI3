using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A ship or a wing moved into another unit: refused where that unit is fighting; a receiving unit with no name is named
/// by its kind, numbered; it is given a leader where its country chooses its own and it has none; and a unit left empty
/// loses its leader. A carrier's air groups ride on it. The AI's agents post it as they split a fleet or an air group
/// (<c>docs/mechanics/agents.md</c>, section 5.4).
/// </summary>
public sealed class CTransferSubUnitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _subUnit;
	private readonly int _unit;

	public CTransferSubUnitCommand(CCountryTag owner, int subUnit, int unit)
	{
		_owner = owner;
		_subUnit = subUnit;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.FindSubUnit(state, _owner, _subUnit) is { Unit: not null } subUnit
			&& CUnitFinder.Find(state, _owner, _unit) is { } unit && !unit.IsFighting
			&& !ReferenceEquals(subUnit.Unit, unit) && (unit.Kind == EUnitKind.Navy) == (subUnit.Unit.Kind == EUnitKind.Navy);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_owner)!;
		CSubUnit subUnit = CUnitFinder.FindSubUnit(state, _owner, _subUnit)!;
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		CUnit source = subUnit.Unit!;
		source.SubUnits.Remove(subUnit);
		unit.SubUnits.Add(subUnit);
		subUnit.Unit = unit;
		if (unit.Name.Length < 2)
		{
			unit.Name = CUnitNames.ForKind(state, country, unit.Kind, unit.SubUnits[0].Type);
			CUnitNames.Register(country, unit.Name);
		}
		if (country.AutoAssignLeaders && unit.Leader is null
			&& !(unit.Kind <= EUnitKind.Division && unit.SubUnits.Count == 1))
		{
			CLeaders.AssignBest(state, country, unit);
		}
		if (source.SubUnits.Count == 0)
		{
			CLeaders.SetLeader(state, source, null, false, false);
		}
		if (source.Kind == EUnitKind.Navy)
		{
			Transport.CTransport.CheckOverload(state, source);
		}
		state.ForgetPositions();
	}
}
