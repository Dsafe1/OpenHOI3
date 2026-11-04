using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Units merged into one, the merge button's: the unit chosen takes every other's brigades and the rest are taken out of
/// the game (<c>docs/mechanics/selection.md</c>, section 5.4). As the original's, it asks nothing of the merge rule - the
/// button is greyed by it - and a unit gone since is passed over; ours asks that they are the owner's.
/// </summary>
public sealed class CMergeCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int[] _units;

	public CMergeCommand(CCountryTag owner, IEnumerable<int> units)
	{
		_owner = owner;
		_units = new List<int>(units).ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return Units(state).Count >= 2;
	}

	public override void Execute(CGameState state)
	{
		List<CUnit> units = Units(state);
		if (units.Count > 0)
		{
			CUnitMerge.Merge(state, units);
		}
	}

	/// <summary>The command's units still in the game, in its order.</summary>
	private List<CUnit> Units(CGameState state)
	{
		var units = new List<CUnit>();
		foreach (int id in _units)
		{
			if (CUnitFinder.Find(state, _owner, id) is { IsRemoved: false } unit && !units.Contains(unit))
			{
				units.Add(unit);
			}
		}
		return units;
	}
}
