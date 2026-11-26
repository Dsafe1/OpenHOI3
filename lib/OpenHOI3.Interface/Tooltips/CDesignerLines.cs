using System;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// Two things an enhanced game's division designer says that the original's does not (after BiceLib's
/// <c>activateParadropLine</c> and <c>activateCombinedArmsLine</c>; <c>docs/mechanics/builders.md</c>, section 11):
/// over the terrain summary, whether the design may be dropped by parachute, what it weighs and which brigades stop it;
/// and under a combined arms group's name, what the group adds and whether it is a base group. Every word is the
/// localisation's, by keys of ours: a mod that gives none sees the original's tooltips.
/// </summary>
public static class CDesignerLines
{
	/// <summary><c>$WEIGHT$</c>: the design's transport weight, whole.</summary>
	public const string ParadropYesKey = "OPENHOI_DESIGNER_PARADROP_YES";

	/// <summary><c>$WEIGHT$</c>; a line of <see cref="ParadropBlockerKey"/> follows for each type that stops it.</summary>
	public const string ParadropNoKey = "OPENHOI_DESIGNER_PARADROP_NO";

	/// <summary><c>$NAME$</c>: a brigade type that may not be dropped.</summary>
	public const string ParadropBlockerKey = "OPENHOI_DESIGNER_PARADROP_BLOCKER";

	/// <summary><c>$VALUE$</c>: what the group adds, a signed percentage.</summary>
	public const string GroupBonusKey = "OPENHOI_DESIGNER_CA_BONUS";

	/// <summary><c>$VALUE$</c> and <c>$TECH$</c>, the part of it that is the country's technology's; used where that is not nought.</summary>
	public const string GroupBonusTechKey = "OPENHOI_DESIGNER_CA_BONUS_TECH";

	public const string GroupBaseKey = "OPENHOI_DESIGNER_CA_BASE";
	public const string GroupNotBaseKey = "OPENHOI_DESIGNER_CA_NOT_BASE";

	/// <summary>
	/// The terrain summary's tooltip: in an enhanced game whose localisation has the lines, a first line saying whether
	/// every brigade of the design may be dropped, with the design's weight - its brigades' <c>transport_weight</c> as
	/// the player's country would build them today, summed and cut to a whole number - and, where it may not, a line
	/// for each type that stops it, once each in the order they come in the design; then a blank line and the summary.
	/// An empty design says nothing, as the original's. What a real boarding also asks - organisation, the planes' room
	/// - a design on paper has not.
	/// </summary>
	/// <param name="design">Each brigade of the design: its type, and its figures as the country would build it today.</param>
	public static string OverTerrainSummary(Func<string, string> localise, CGameState state,
		IReadOnlyList<(CSubUnitType Type, CSubUnitStats Stats)> design, string summary)
	{
		if (design.Count == 0 || !state.Rules.Enhanced || localise(ParadropYesKey) == ParadropYesKey)
		{
			return summary;
		}
		long weight = 0;
		var blockers = new List<string>();
		foreach ((CSubUnitType type, CSubUnitStats stats) in design)
		{
			weight += stats[CSubUnitType.TransportWeight].Raw;
			if (!type.Flag(CAirTransport.CanParadrop) && !blockers.Contains(type.Key))
			{
				blockers.Add(type.Key);
			}
		}
		(string, string) weighs = ("WEIGHT", (weight / CFixedPoint.Scale).ToString(System.Globalization.CultureInfo.InvariantCulture));
		var text = new StringBuilder();
		text.Append(CTextTemplate.Fill(localise(blockers.Count == 0 ? ParadropYesKey : ParadropNoKey), weighs));
		foreach (string blocker in blockers)
		{
			text.Append('\n').Append(CTextTemplate.Fill(localise(ParadropBlockerKey), ("NAME", localise(blocker))));
		}
		if (summary.Length != 0)
		{
			text.Append("\n\n").Append(summary);
		}
		return text.ToString();
	}

	/// <summary>
	/// What goes under a combined arms group's name: what it adds - its own value and what the country's technology
	/// adds to it, as <see cref="CCombinedArms.Bonus"/> counts them - and whether it is a base group, without one of
	/// which in the design no group counts. Nothing in a faithful game or where the localisation has no such lines.
	/// </summary>
	public static string UnderGroup(Func<string, string> localise, CGameState state, CCountry country, CCombinedArmsGroup group)
	{
		if (!state.Rules.Enhanced || localise(GroupBonusKey) == GroupBonusKey)
		{
			return "";
		}
		long technology = country.Technology[group.Name].Raw;
		string value = SignedPercent(group.Value.Raw + technology);
		string bonus = technology != 0
			? CTextTemplate.Fill(localise(GroupBonusTechKey), ("VALUE", value), ("TECH", SignedPercent(technology)))
			: CTextTemplate.Fill(localise(GroupBonusKey), ("VALUE", value));
		return bonus + "\n" + localise(group.IsBase ? GroupBaseKey : GroupNotBaseKey);
	}

	/// <summary>Thousandths as a percentage of one decimal with its sign always: 125 is +12.5%.</summary>
	public static string SignedPercent(long thousandths)
	{
		long size = Math.Abs(thousandths);
		return $"{(thousandths < 0 ? "-" : "+")}{size / 10}.{size % 10}%";
	}
}
