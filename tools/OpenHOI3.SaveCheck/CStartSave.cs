using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;

/// <summary>
/// What a check that sets a new game beside a save of its first hour takes from the save before the game is made: its
/// date, which is the bookmark the save's game was started at - 1936.1.1 for the original's first, a later one's for the
/// rest (<c>docs/mechanics/bookmarks.md</c>). Read off the save's head, so the file is not parsed twice.
/// </summary>
public static class CStartSave
{
	public static CDate Date(string path)
	{
		long hours = CSaveHeader.Read(path).Hours
			?? throw new System.IO.InvalidDataException($"'{path}' has no readable date");
		return CDate.FromDayNumber((int)(hours / CGameState.HoursPerDay));
	}
}
