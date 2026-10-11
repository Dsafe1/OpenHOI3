using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

/// <summary>
/// What a trial says of a unit's leaders once its battles are fought: each from the unit's
/// own up its chain of command, with his skill, his experience, the hours he has banked and
/// how far he is towards each trait a battle earns. See <c>docs/mechanics/leaders.md</c>,
/// section 11.
/// </summary>
public static class CLeaderGainText
{
	public static IEnumerable<string> Lines(CUnit unit)
	{
		for (CUnit? at = unit; at is not null; at = at.Parent)
		{
			if (at.Leader is { } leader)
			{
				yield return $"    {at.Name}: {Describe(leader)}";
			}
		}
	}

	public static string Describe(CLeader leader)
	{
		string text = $"{leader.Name}, skill {leader.Skill} of {leader.MaxSkill}, experience {leader.Experience.ToThousandths()}";
		if (leader.TraitProgress.Duration > 0)
		{
			text += $", {leader.TraitProgress.Duration} hours banked";
		}
		var towards = leader.TraitProgress.Progress
			.Where(pair => pair.Value > 0)
			.Select(pair => $"{pair.Key.Key} {new CFixedPoint(pair.Value)}%")
			.ToList();
		if (towards.Count > 0)
		{
			text += "; towards " + string.Join(", ", towards);
		}
		return text;
	}
}
