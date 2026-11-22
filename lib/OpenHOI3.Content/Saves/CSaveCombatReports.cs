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
/// <c>combatreports.md</c>): a key of ours at the top, once for each battle, ten words in a row - the hour it ended,
/// its kind's number, its province, the attacker's and the defender's tag (<c>---</c> for none), the attacker's losses
/// in thousandths and its men, the defender's the same, and who won: 0 nobody, 1 the attacker, 2 the defender. A
/// faithful game writes none, its save being the original's key for key.
/// </summary>
public static class CSaveCombatReports
{
	public const string Key = "openhoi_combat_report";

	private const string NoCountry = "---";

	public static void Write(CSaveTextWriter writer, CGameState state)
	{
		if (!state.Rules.Enhanced)
		{
			return;
		}
		foreach (CCombatReport report in state.CombatReports.Entries)
		{
			writer.Words(Key, new[]
			{
				Whole(report.Hour), Whole((int)report.Kind), Whole(report.Province),
				report.Attacker?.Value ?? NoCountry, report.Defender?.Value ?? NoCountry,
				Whole(report.AttackerLosses), Whole(report.AttackerMen), Whole(report.DefenderLosses), Whole(report.DefenderMen),
				Whole((int)report.Winner),
			});
		}
	}

	/// <summary>One report read back; one with fewer than ten words, or a number that is none, is reported and left out.</summary>
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

	private static string Whole(long value) => value.ToString(CultureInfo.InvariantCulture);

	private static CCountryTag? Tag(string word) => word == NoCountry || word.Length == 0 ? null : new CCountryTag(word);
}
