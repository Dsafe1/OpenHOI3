using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A leader one rank up or one down, what the promotion buttons of the window a leader
/// is chosen in post. It costs nothing and changes nothing but the rank, and the rank
/// stays between 1 and 4 - except that **a general who commands a unit is taken off it**,
/// whichever way he went. An admiral or an air marshal keeps his. See
/// <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public sealed class CPromoteLeaderCommand : CCommand
{
	private const int LowestRank = 1;
	private const int HighestRank = 4;

	private readonly int _leader;
	private readonly bool _down;
	private readonly int _steps;

	public CPromoteLeaderCommand(int leader, bool down) : this(leader, down, 1)
	{
	}

	/// <summary>A move of several ranks at once, as the AI's promotion gives it (FINDINGS-liveread.md, section 80).</summary>
	public CPromoteLeaderCommand(int leader, bool down, int steps)
	{
		_leader = leader;
		_down = down;
		_steps = steps;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Leaders.TryGetValue(_leader, out CLeader? leader) && NewRank(leader) is >= LowestRank and <= HighestRank;
	}

	public override void Execute(CGameState state)
	{
		CLeader leader = state.Leaders[_leader];
		leader.Rank = NewRank(leader);
		if (leader.Type == ELeaderType.Land && leader.Unit is { } unit)
		{
			CLeaders.SetLeader(state, unit, null, false, false);
		}
	}

	private int NewRank(CLeader leader)
	{
		return _down ? leader.Rank - _steps : leader.Rank + _steps;
	}
}
