using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Puts a leader on a unit, or takes its leader off where none is named: what a row of
/// the window a leader is chosen in posts, and its "no leader" button. The leader is
/// first taken off whatever he commanded; the unit then loses the organisation a new
/// commander costs and he his hours of battle banked towards a trait. Nothing asks whose
/// unit it is. See <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public sealed class CAssignLeaderCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int? _leader;
	private readonly bool _onlyFree;

	/// <param name="leader">The leader's number, or null to leave the unit with none.</param>
	/// <param name="onlyFree">Whether a leader commanding another unit is refused; the window always asks it.</param>
	public CAssignLeaderCommand(CCountryTag owner, int unit, int? leader, bool onlyFree = true)
	{
		_owner = owner;
		_unit = unit;
		_leader = leader;
		_onlyFree = onlyFree;
	}

	/// <summary>
	/// The unit has to be there. Taking a leader off is always allowed; putting one on asks
	/// what the choosing asks of him, and an unknown number counts as none.
	/// </summary>
	public override bool MayExecute(CGameState state)
	{
		if (Find(state) is not { } unit)
		{
			return false;
		}
		return Leader(state) is not { } leader || CLeaders.MayCommand(leader, unit, _onlyFree);
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = Find(state)!;
		CLeader? leader = Leader(state);
		if (leader?.Unit is { } other && other != unit)
		{
			CLeaders.SetLeader(state, other, null, false, false);
		}
		CLeaders.SetLeader(state, unit, leader, true, true);
	}

	private CLeader? Leader(CGameState state)
	{
		return _leader is { } id && state.Leaders.TryGetValue(id, out CLeader? leader) ? leader : null;
	}

	private CUnit? Find(CGameState state)
	{
		if (state.Country(_owner) is not { } country)
		{
			return null;
		}
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == _unit)
			{
				return unit;
			}
		}
		return null;
	}
}
