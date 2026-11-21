using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A ship or a wing moved into another unit: refused where that unit is fighting; a receiving unit with no name is named
/// by its kind, numbered; it is given a leader where its country chooses its own and it has none; and a unit left empty,
/// or a land unit left with one brigade, loses its leader. A carrier's air groups ride on it. The AI's agents post it as they split a fleet or an air group
/// (<c>docs/mechanics/agents.md</c>, section 5.4).
/// </summary>
public sealed class CTransferSubUnitCommand : CCommand, INewUnitIds
{
	private readonly CCountryTag _owner;
	private readonly int _subUnit;
	private int _unit;

	public CTransferSubUnitCommand(CCountryTag owner, int subUnit, int unit)
	{
		_owner = owner;
		_subUnit = subUnit;
		_unit = unit;
	}

	public void GiveIds(System.Func<int, int> real)
	{
		_unit = real(_unit);
	}

	/// <summary>The original's eleven tests, the two units in one province (<c>docs/mechanics/reorganise.md</c>, section 4).</summary>
	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.FindSubUnit(state, _owner, _subUnit) is { Unit: not null } subUnit
			&& CUnitFinder.Find(state, _owner, _unit) is { } unit && state.Country(_owner) is { } country
			&& CSubUnitTransfer.MayTake(state, country, unit, subUnit, sameProvince: true);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_owner)!;
		CSubUnit subUnit = CUnitFinder.FindSubUnit(state, _owner, _subUnit)!;
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		CUnit source = subUnit.Unit!;
		CSubUnitTransfer.Move(state, country, subUnit, unit);
		// A land unit left with one brigade loses its leader too - the transfer's, which the split's move does not do.
		if (source.SubUnits.Count == 1 && Movement.CMovementRules.IsLandUnit(source))
		{
			Leaders.CLeaders.SetLeader(state, source, null, false, false);
		}
		// The player is told of his own (reorganise.md, section 4).
		if (state.Played.Contains(unit.Owner))
		{
			state.RaisePactMessage(new Agreements.CPactMessage("TRANSFEREDSUBUNIT", "", "", unit.Owner, unit.Owner)
			{
				Told = unit.Owner,
				Values = new[] { ("SUB", subUnit.Name), ("OLD", source.Name), ("NEW", unit.Name) },
			});
		}
		// A unit of one brigade now looks for a battle where it stands.
		if (unit.SubUnits.Count == 1)
		{
			Combat.CCombats.CheckForCombat(state, unit, unit.Location);
		}
	}
}
