using System;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The AI's promotions, once a day at the country's hour + 16: its units with no leader counted by level, divisions left
/// out; then from the top level down, one at a time, the first free land leader in service promoted to the rank a unit of
/// that level takes - (4 - his rank) - (the level - 1, or nought) ranks, where that is one or more - so that the leader pass
/// can then put him in command. Fleets and air units are never promoted for. The original's
/// <c>CEU3AI_PromoteLeadersForCommands</c>; Luxembourg's theatre, Force Publique, has Felix Bourbon-Parma raised from rank 2
/// to 4 on a new game's first day and given him on the second (<c>reversing/findings/FINDINGS-liveread.md</c>, section 80;
/// <c>docs/mechanics/moving.md</c>, section 13).
/// </summary>
public static class CLeaderPromotion
{
	private const int HighestRank = 4;

	public static void Run(CGameState state, CCountry country)
	{
		var leaderless = new int[4];
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.IsRemoved || unit.Leader is not null || Level(unit) is not { } level)
			{
				continue;
			}
			leaderless[level]++;
		}
		var promoted = new System.Collections.Generic.HashSet<CLeader>();
		for (int level = leaderless.Length - 1; level >= 0; level--)
		{
			if (leaderless[level] == 0)
			{
				continue;
			}
			CLeader? first = null;
			foreach (CLeader leader in country.ActiveLeaders)
			{
				if (leader.Unit is null && leader.Rank != 0 && leader.Type == ELeaderType.Land && !promoted.Contains(leader))
				{
					first = leader;
					break;
				}
			}
			if (first is null)
			{
				continue;
			}
			int steps = (HighestRank - first.Rank) - Math.Max(level - 1, 0);
			if (steps >= 1)
			{
				promoted.Add(first);
				state.Outbox.Add(new CPromoteLeaderCommand(first.Id, down: false, steps));
			}
		}
	}

	/// <summary>A headquarters' level, the theatre's nought; null for a division, a fleet or an air unit.</summary>
	private static int? Level(CUnit unit) => unit.Kind switch
	{
		EUnitKind.Theatre => 0,
		EUnitKind.ArmyGroup => 1,
		EUnitKind.Army => 2,
		EUnitKind.Corps => 3,
		_ => null,
	};
}
