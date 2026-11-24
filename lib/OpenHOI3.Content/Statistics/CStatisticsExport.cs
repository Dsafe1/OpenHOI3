using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Statistics;

namespace OpenHOI3.Content.Statistics;

/// <summary>
/// The statistics record written out as files, in the layout BlackICE's collector wrote day by day and its plotting
/// tool reads: a folder a country, a file a series, <c>Date,name</c> and then a line a day of the day's number and the
/// value. See <c>docs/mechanics/statisticsrecord.md</c>, section 8.
/// </summary>
public static class CStatisticsExport
{
	/// <summary>The day number the files count from: 1 January 1936 on the game's calendar is day nought.</summary>
	public const int FirstDay = 365 * 1936;

	/// <summary>The folder under the player's own that the files go to, each export in a folder of its own.</summary>
	public const string Folder = "openhoi_statistics";

	/// <summary>A series' file: its header, and a line a day.</summary>
	public static string File(string name, int firstDayNumber, long[] thousandths)
	{
		var text = new StringBuilder("Date,").Append(name);
		for (int index = 0; index < thousandths.Length; index++)
		{
			text.Append('\n').Append((firstDayNumber + index - FirstDay).ToString(CultureInfo.InvariantCulture)).Append(',')
				.Append(Value(thousandths[index]));
		}
		return text.ToString();
	}

	/// <summary>A value as a file holds it: whole where it is whole, else to as many places as it has, at most three.</summary>
	public static string Value(long thousandths)
	{
		if (thousandths % 1000 == 0)
		{
			return (thousandths / 1000).ToString(CultureInfo.InvariantCulture);
		}
		return (thousandths / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Every country's every series written under a folder, a folder a country. Answers how many files were written.
	/// </summary>
	public static int Write(CGameState state, string folder)
	{
		CStatistics statistics = state.Statistics;
		CStatisticSeries series = statistics.Series(state);
		int written = 0;
		foreach (CCountryTag tag in statistics.Countries.Keys)
		{
			string own = Path.Combine(folder, tag.Value);
			Directory.CreateDirectory(own);
			foreach (string name in series.Names)
			{
				if (statistics.Read(state, tag, name) is not { } kept)
				{
					continue;
				}
				System.IO.File.WriteAllText(Path.Combine(own, FileName(name)), File(name, kept.FirstDayNumber, kept.Values), Encoding.ASCII);
				written++;
			}
		}
		return written;
	}

	/// <summary>A series' name as a file's: every letter a file may not be named with made an underscore.</summary>
	public static string FileName(string name)
	{
		var text = new StringBuilder(name.Length);
		HashSet<char> refused = new(Path.GetInvalidFileNameChars());
		foreach (char letter in name)
		{
			text.Append(refused.Contains(letter) ? '_' : letter);
		}
		return text.ToString();
	}

	/// <summary>The folder an export of a game goes to: the player's, the export's own name by the game's date and hour.</summary>
	public static string FolderFor(string userFolder, CGameState state)
	{
		return Path.Combine(userFolder, Folder, string.Create(CultureInfo.InvariantCulture,
			$"{state.Date.Year:0000}_{state.Date.Month:00}_{state.Date.Day:00}_{state.Hour:00}"));
	}
}
