using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What a leader says in a tooltip, wherever his name or his portrait is: his rank and name,
/// how many ships or wings he commands, an admiral's positioning, his skill, how far he is to
/// the next, his traits and how far he is towards those he may earn. See
/// <c>docs/mechanics/unitpanel.md</c>, section 10.5.
/// </summary>
public static class CLeaderTooltip
{
	private const string Line = "§W\n";
	private const string Yellow = " §Y";

	/// <summary>The rank tables, a general's, an admiral's and an air marshal's: <c>INACTIVE</c> at rank 0.</summary>
	private static readonly string[] RankPrefixes = { "LAND_RANK", "SEA_RANK", "AIR_RANK" };

	/// <summary>
	/// The thresholds a skill starts at, and after them the word that follows the table in the
	/// original, which is what skill 11 takes for the next.
	/// </summary>
	private const long AfterTheTable = 1;

	/// <summary>
	/// A leader's whole tooltip. How many he may command is said only where he commands a
	/// fleet or an air unit, and is measured against it - so a leader commanding nothing, in
	/// the window he is chosen in, does not say it.
	/// </summary>
	public static string For(Func<string, string> localise, CGameState state, CLeader leader)
	{
		var text = new StringBuilder();
		text.Append(RankName(localise, leader)).Append(Yellow).Append(leader.Name).Append(Line);
		if (leader.Unit is { Kind: EUnitKind.Navy or EUnitKind.Air } unit)
		{
			text.Append(localise("LEADER_COMMAND_SIZE"))
				.Append(CLeaders.HasRankFor(state, unit) ? " §G" : " §R")
				.Append(' ').Append(CLeaders.CommandSize(state, leader, unit).ToString(CultureInfo.InvariantCulture))
				.Append(' ').Append(localise(unit.Kind == EUnitKind.Navy ? "SHIPS_LABEL" : "PLANES_LABEL"))
				.Append(Line);
		}
		if (leader.Type == ELeaderType.Sea)
		{
			// His skill times a tenth, as a percentage.
			text.Append(localise("LEADER_POSITIONING")).Append(Yellow)
				.Append(CNumberText.Format(new CFixedPoint(leader.Skill * 100L * 100), 0)).Append('%').Append(Line);
		}
		text.Append(localise("LEADER_SKILL")).Append(Yellow).Append(leader.Skill.ToString(CultureInfo.InvariantCulture));
		if (leader.Skill >= leader.MaxSkill)
		{
			text.Append(" (").Append(localise("LEADER_SKILL_AT_MAX")).Append(')');
		}
		text.Append(Line);
		text.Append(localise("LEADER_XP")).Append(Yellow)
			.Append(CNumberText.Format(new CFixedPoint(ExperienceShare(leader) * 100), 2)).Append('%').Append(Line);
		if (leader.Traits.Count > 0)
		{
			text.Append('\n').Append(localise("ACTUAL_TRAITS_DESC"));
			foreach (CTrait trait in leader.Traits)
			{
				text.Append("\n- §Y").Append(localise(trait.Key)).Append("§!");
			}
		}
		string progress = Progress(localise, state, leader);
		if (progress.Length > 0)
		{
			text.Append('\n').Append(localise("TO_GAIN_TRAITS_DESC")).Append('\n').Append(progress);
		}
		return text.ToString();
	}

	/// <summary>His rank's name: <c>LAND_RANK1</c> to <c>4</c> and the others by his kind, <c>INACTIVE</c> at none.</summary>
	public static string RankName(Func<string, string> localise, CLeader leader)
	{
		string prefix = RankPrefixes[Math.Clamp((int)leader.Type, 0, RankPrefixes.Length - 1)];
		return localise(leader.Rank <= 0 ? "INACTIVE" : prefix + leader.Rank.ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// How far he is from his skill's threshold to the next, in thousandths, as the original
	/// works it in its fifteen binary places; nothing from skill 12. At skill 11 the next
	/// threshold is the stray word after the table, so the share comes out at nothing or less.
	/// </summary>
	public static long ExperienceShare(CLeader leader)
	{
		if (leader.Skill >= CLeaders.MostSkill)
		{
			return 0;
		}
		int skill = Math.Max(0, leader.Skill);
		long floor = CLeaders.SkillFloors[skill] << CFixed15.Places;
		long next = (skill + 1 < CLeaders.SkillFloors.Length ? CLeaders.SkillFloors[skill + 1] : AfterTheTable) << CFixed15.Places;
		long share = ((leader.Experience.Raw - floor) << CFixed15.Places) / (next - floor);
		return (share * (1000L << CFixed15.Places)) >> (2 * CFixed15.Places);
	}

	/// <summary>
	/// A line for each gainable trait he is part of the way to - above nothing and short of the whole -
	/// with the progress as a percentage to two places; a trait two definitions give, each its own line. The original
	/// walks a map keyed by where each definition lies in memory; here they come in the order of the traits file, a
	/// trait's definitions in the order he began them.
	/// </summary>
	public static string Progress(Func<string, string> localise, CGameState state, CLeader leader)
	{
		var lines = new List<string>();
		foreach ((CGainableTrait gainable, int progress) in Ordered(state, leader))
		{
			if (progress <= 0 || progress >= CTraitProgress.Whole)
			{
				continue;
			}
			lines.Add("- " + CTextTemplate.Fill(localise("TRAIT_GAIN_PROGRESS"),
				("PROG", CNumberText.Format(new CFixedPoint(progress), 2)), ("TRAIT", localise(gainable.Trait.Key))));
		}
		return string.Join("\n", lines);
	}

	private static IEnumerable<KeyValuePair<CGainableTrait, int>> Ordered(CGameState state, CLeader leader)
	{
		var order = new Dictionary<CTrait, int>();
		foreach (CTrait trait in state.Traits)
		{
			order.TryAdd(trait, order.Count);
		}
		// A stable sort: a trait the file does not have last, each trait's definitions in the order he began them.
		return leader.TraitProgress.Progress.OrderBy(entry => order.GetValueOrDefault(entry.Key.Trait, int.MaxValue));
	}
}
