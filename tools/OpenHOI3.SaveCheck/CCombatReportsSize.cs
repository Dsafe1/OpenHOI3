using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;

/// <summary>
/// How large a game's record of finished battles is in a save, for the headless run's <c>reports</c>: for each year
/// of the game how many battles ended, what they came to as the text saves held until 2026-11-25, and what they come
/// to packed. A look.
/// </summary>
public static class CCombatReportsSize
{
	public static void Report(CGameState state)
	{
		Console.WriteLine();
		Console.WriteLine($"Record of finished battles: {state.CombatReports.Entries.Count} reports.");
		Console.WriteLine("  year    battles     as text      packed   a battle   of the text");
		long allText = 0;
		long allPacked = 0;
		foreach ((long year, List<CCombatReport> reports) in CSaveCombatReports.Years(state.CombatReports.Entries))
		{
			long text = 0;
			foreach (CCombatReport report in reports)
			{
				text += CSaveCombatReports.OldText(report).Length;
			}
			// The letters, with the key, the year and the count before them.
			long packed = CSaveStatistics.Letters(CCombatReportBlock.Pack(reports)).Length + 24;
			allText += text;
			allPacked += packed;
			Line(year.ToString(CultureInfo.InvariantCulture), reports.Count, text, packed);
		}
		Line("all", state.CombatReports.Entries.Count, allText, allPacked);
	}

	private static void Line(string name, int battles, long text, long packed)
	{
		Console.WriteLine($"  {name,-5} {battles,9} {Size(text),11} {Size(packed),11} {(battles > 0 ? (double)packed / battles : 0),8:0.0} B {(text > 0 ? 100.0 * packed / text : 0),9:0}%");
	}

	private static string Size(long bytes) => bytes >= 1024 * 1024
		? (bytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture) + " MiB"
		: (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KiB";
}
