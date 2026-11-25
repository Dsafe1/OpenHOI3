using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// An enhanced game's record of finished battles in its save (<c>docs/mechanics/savegame.md</c>, section 22;
/// <c>combatreports.md</c>, section 3): a block of ours at the top, <c>openhoi_combat_reports</c>, holding a
/// <c>y</c> for each year that has a battle - the year, how many reports, and the reports packed
/// (<see cref="CCombatReportBlock"/>) as a bare word of letters, as the statistics' blocks are. Every figure is kept
/// exact. A faithful game writes none, its save being the original's key for key.
///
/// Until 2026-11-25 each battle was a key of its own, <c>openhoi_combat_report</c>, ten words in a row - the hour it
/// ended, its kind's number, its province, the attacker's and the defender's tag (<c>---</c> for none), the attacker's
/// losses in thousandths and its men, the defender's the same, and who won - some five times the size. A save
/// written so is still read.
/// </summary>
public static class CSaveCombatReports
{
	/// <summary>The key of one battle, as saves were written until 2026-11-25: read, and written no more.</summary>
	public const string Key = "openhoi_combat_report";

	public const string BlockKey = "openhoi_combat_reports";
	private const string YearKey = "y";

	/// <summary>The hours of a year, on the original's calendar of 365 days: an hour over it is the year the hour is in.</summary>
	public const long HoursInYear = 365L * CGameState.HoursPerDay;

	private const string NoCountry = "---";

	public static void Write(CSaveTextWriter writer, CGameState state)
	{
		if (!state.Rules.Enhanced)
		{
			return;
		}
		writer.Begin(BlockKey);
		foreach ((long year, List<CCombatReport> reports) in Years(state.CombatReports.Entries))
		{
			writer.Words(YearKey, new[] { Whole(year), Whole(reports.Count), CSaveStatistics.Letters(CCombatReportBlock.Pack(reports)) });
		}
		writer.End();
	}

	/// <summary>The reports by the year they ended in, the years in order and each year's in the record's.</summary>
	public static IEnumerable<(long Year, List<CCombatReport> Reports)> Years(IReadOnlyList<CCombatReport> entries)
	{
		var years = new SortedDictionary<long, List<CCombatReport>>();
		foreach (CCombatReport report in entries)
		{
			long year = report.Hour / HoursInYear;
			if (!years.TryGetValue(year, out List<CCombatReport>? list))
			{
				years[year] = list = new List<CCombatReport>();
			}
			list.Add(report);
		}
		return years.Select(pair => (pair.Key, pair.Value));
	}

	/// <summary>The block read back: a year whose letters are no block is reported and left out.</summary>
	public static void ReadBlock(CGameState state, ClausewitzNode node, CSaveReport report)
	{
		foreach (ClausewitzNode year in node.Children.Where(child => child.Key == YearKey && child.IsBlock))
		{
			List<string> words = year.Children.Select(child => child.Value ?? "").ToList();
			if (words.Count < 3 || CSaveStatistics.Bytes(words[2]) is not { } bytes || CCombatReportBlock.Unpack(bytes) is not { } reports)
			{
				report.Unread("top", BlockKey);
				continue;
			}
			foreach (CCombatReport one in reports)
			{
				state.CombatReports.Add(one);
			}
		}
	}

	/// <summary>One report of an older save read back; one with fewer than ten words, or a number that is none, is reported and left out.</summary>
	public static void Read(CGameState state, ClausewitzNode node, CSaveReport report)
	{
		List<string> words = node.Children.Select(child => child.Value ?? "").ToList();
		if (words.Count < 10
			|| !long.TryParse(words[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long hour)
			|| !int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int kind)
			|| !int.TryParse(words[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int province)
			|| !long.TryParse(words[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out long attackerLosses)
			|| !long.TryParse(words[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out long attackerMen)
			|| !long.TryParse(words[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out long defenderLosses)
			|| !long.TryParse(words[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out long defenderMen)
			|| !int.TryParse(words[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out int winner)
			|| kind < 0 || kind > (int)ECombatKind.NavalBombing || winner < 0 || winner > (int)ECombatWinner.Defender)
		{
			report.Unread("top", Key);
			return;
		}
		state.CombatReports.Add(new CCombatReport(hour, (ECombatKind)kind, province, Tag(words[3]), Tag(words[4]),
			attackerLosses, attackerMen, defenderLosses, defenderMen, (ECombatWinner)winner));
	}

	/// <summary>A report as the older saves' key of its own, for setting the two sizes beside each other.</summary>
	public static string OldText(CCombatReport report)
	{
		return Key + "=\n{\n" + string.Join(" ", new[]
		{
			Whole(report.Hour), Whole((int)report.Kind), Whole(report.Province),
			report.Attacker?.Value ?? NoCountry, report.Defender?.Value ?? NoCountry,
			Whole(report.AttackerLosses), Whole(report.AttackerMen), Whole(report.DefenderLosses), Whole(report.DefenderMen),
			Whole((int)report.Winner),
		}) + " }\n";
	}

	private static string Whole(long value) => value.ToString(CultureInfo.InvariantCulture);

	private static CCountryTag? Tag(string word) => word == NoCountry || word.Length == 0 ? null : new CCountryTag(word);
}
