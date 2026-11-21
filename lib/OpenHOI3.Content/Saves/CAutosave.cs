using System;
using System.IO;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The autosave: due on the day the settings' frequency names, asked as each day begins, and written to the first of
/// three files that are shifted along at each save - <c>autosave.hoi3</c>, <c>oldautosave.hoi3</c>,
/// <c>olderautosave.hoi3</c>. See <c>docs/mechanics/savegame.md</c>, section 11.
/// </summary>
public static class CAutosave
{
	/// <summary>The three files, the newest first.</summary>
	public static readonly string[] Files = { "autosave.hoi3", "oldautosave.hoi3", "olderautosave.hoi3" };

	/// <summary>
	/// Whether a day is one the frequency saves on [exe]: never; weekly where the day's number less one is a whole number
	/// of weeks; the 1st of every month; the 1st of January and of July; the 1st of January; the 1st of January of a year
	/// a five divides. A name that is none of these - the settings' reader reports one - saves never.
	/// </summary>
	public static bool IsDue(string frequency, CDate date)
	{
		return frequency switch
		{
			"WEEKLY" => (date.DayNumber - 1) % 7 == 0,
			"MONTHLY" => date.Day == 1,
			"HALFYEAR" => date.Day == 1 && date.Month is 1 or 7,
			"YEARLY" => date.Day == 1 && date.Month == 1,
			"FIVE_YEAR" => date.Day == 1 && date.Month == 1 && date.Year % 5 == 0,
			_ => false,
		};
	}

	/// <summary>
	/// The autosave written: the oldest of the three files gone, the other two shifted along, and the game written to the
	/// first. Answers the path written.
	/// </summary>
	public static string Write(CGameState state, Func<string, string> localise, string folder)
	{
		return Write(state, localise, folder, Files);
	}

	/// <summary>The same rotation over another set of files, the newest first: an enhanced game's extra autosaves' (<see cref="CExtraAutosaves"/>).</summary>
	public static string Write(CGameState state, Func<string, string> localise, string folder, string[] files)
	{
		Directory.CreateDirectory(folder);
		string Path(int index) => System.IO.Path.Combine(folder, files[index]);
		File.Delete(Path(files.Length - 1));
		for (int index = files.Length - 2; index >= 0; index--)
		{
			if (File.Exists(Path(index)))
			{
				File.Move(Path(index), Path(index + 1));
			}
		}
		CSaveFiles.Write(state, localise, Path(0));
		return Path(0);
	}
}
