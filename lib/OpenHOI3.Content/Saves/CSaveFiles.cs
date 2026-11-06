using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The save dialog's files, as the original's names and lists them: the folder, its <c>.hoi3</c> files newest first, a
/// row's text, the name a save is offered under and the name a typed one is saved as. See
/// <c>docs/mechanics/ingamemenu.md</c>, section 6.
/// </summary>
public static class CSaveFiles
{
	/// <summary>The folder saves are written to, under the player's folder - and listed from under the install's too.</summary>
	public const string FolderName = "save games";

	/// <summary>The extension a save is written with, and the one a name is tested for, case and all.</summary>
	public const string Extension = ".hoi3";

	/// <summary>
	/// Every save in the folders, the player's first: each name ending in <c>hoi3</c>, case and all and with no dot needed,
	/// but a name with a character above 127, which the original leaves out; a name in two folders is the first one's.
	/// Newest first by the file's time in whole seconds - where two are of one second, the original's sort leaves them in
	/// no fixed order, and here the name settles it.
	/// </summary>
	public static IReadOnlyList<string> List(IEnumerable<string> folders)
	{
		var found = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (string folder in folders)
		{
			if (!Directory.Exists(folder))
			{
				continue;
			}
			foreach (string path in Directory.EnumerateFiles(folder))
			{
				string name = Path.GetFileName(path);
				if (!name.EndsWith("hoi3", StringComparison.Ordinal) || name.Any(c => c > 127) || found.ContainsKey(name))
				{
					continue;
				}
				found[name] = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
			}
		}
		return found.OrderByDescending(file => file.Value).ThenBy(file => file.Key, StringComparer.Ordinal)
			.Select(file => file.Key).ToList();
	}

	/// <summary>Where a listed save is: the first of the folders holding it, as <see cref="List"/> takes the first one's.</summary>
	public static string? Find(IEnumerable<string> folders, string fileName)
	{
		return folders.Select(folder => Path.Combine(folder, fileName)).FirstOrDefault(File.Exists);
	}

	/// <summary>A row's text: the name cut at its first <c>.hoi3</c> - where <c>save</c> tests the last dot.</summary>
	public static string RowText(string fileName)
	{
		int at = fileName.IndexOf(Extension, StringComparison.Ordinal);
		return at < 0 ? fileName : fileName[..at];
	}

	/// <summary>
	/// The name the dialog offers: the player's country's name with only its first space made <c>_</c>, then the year,
	/// month, day and hour, <c>Germany1936_01_01_00</c>.
	/// </summary>
	public static string OfferedName(string countryName, long hours)
	{
		int space = countryName.IndexOf(' ');
		string country = space < 0 ? countryName : countryName[..space] + "_" + countryName[(space + 1)..];
		long day = Math.DivRem(hours, CGameState.HoursPerDay, out long hour);
		CDate date = CDate.FromDayNumber((int)day);
		return string.Create(CultureInfo.InvariantCulture, $"{country}{date.Year}_{date.Month:00}_{date.Day:00}_{hour:00}");
	}

	/// <summary>
	/// The file a typed name is saved as, or none for an empty box. The original trims the name and throws the trimmed
	/// one away, so a name of blanks is saved; <c>.hoi3</c> is added unless the last dot, after the first character,
	/// begins exactly <c>.hoi3</c> to the end - <c>x.HOI3</c> gives <c>x.HOI3.hoi3</c>. No character is refused.
	/// </summary>
	public static string? FileName(string typed)
	{
		if (typed.Length == 0)
		{
			return null;
		}
		int dot = typed.LastIndexOf('.');
		return dot > 0 && string.Equals(typed[dot..], Extension, StringComparison.Ordinal) ? typed : typed + Extension;
	}

	/// <summary>
	/// The game written to the file, truncating one there, in the original's text and bytes - Windows-1252, which the
	/// readers hold as Latin-1. The folder is made where it is missing.
	/// </summary>
	public static void Write(CGameState state, Func<string, string> localise, string path)
	{
		string text = CSaveWriter.Write(state, localise);
		if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
		{
			Directory.CreateDirectory(folder);
		}
		File.WriteAllBytes(path, Encoding.Latin1.GetBytes(text));
	}
}
