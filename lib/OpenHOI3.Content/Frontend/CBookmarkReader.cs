using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Frontend;

/// <summary>
/// Reads <c>common/bookmarks.txt</c>, the lobby's Historical Start list: every <c>bookmark</c> block in file order, a mod's
/// file replacing the game's whole, as the original's search path gives it (<c>docs/mechanics/bookmarks.md</c>, section 1;
/// <c>reversing/findings/FINDINGS-bookmarks.md</c>, section 1). A bookmark with <c>is_cgm = yes</c> belongs to the custom
/// games' list, which is not offered (<c>docs/compatibility.md</c>, entry 17), and is left out. A country that is none is
/// dropped and reported, the bookmark kept; a key that is none of the seven ends the file - that bookmark and every one
/// after it lost, reported once - as the original's reading does.
/// </summary>
public static class CBookmarkReader
{
	public const string File = "common/bookmarks.txt";

	/// <summary>The seven keys a bookmark may hold; any other ends the file.</summary>
	private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "desc", "icon", "date", "country", "is_cgm", "cgm_folder",
	};

	/// <summary>The historical bookmarks of the install's file, in its order; empty where there is no file.</summary>
	public static IReadOnlyList<CBookmark> Read(CDirectorySettings directories, CCountries countries, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			warn($"Failed to open file {path}");
			return Array.Empty<CBookmark>();
		}
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (IOException e)
		{
			warn(CDiagnostics.Located(path, 0, $"could not read the file: {e.Message}"));
			return Array.Empty<CBookmark>();
		}
		return Parse(root, tag => countries.Find(tag) is not null, path, warn);
	}

	/// <summary>The historical bookmarks of a parsed file, in its order, up to the first that does not read.</summary>
	public static IReadOnlyList<CBookmark> Parse(ClausewitzNode root, Func<CCountryTag, bool> isCountry, string path, Action<string> warn)
	{
		var bookmarks = new List<CBookmark>();
		foreach (ClausewitzNode block in root.Children)
		{
			if (ReadOne(block, isCountry, path, warn, out int failedAt) is not { } read)
			{
				// The original's own words, at the key that ended the file.
				warn(CDiagnostics.Located(path, failedAt, "Failed to read bookmark"));
				break;
			}
			if (!read.IsCustom)
			{
				bookmarks.Add(read.Bookmark);
			}
		}
		return bookmarks;
	}

	/// <summary>
	/// One top-level block as a bookmark, or null where a key of it is none of the seven - with
	/// <paramref name="failedAt"/> the line of that key, or of the entry where it is no block.
	/// </summary>
	private static (CBookmark Bookmark, bool IsCustom)? ReadOne(ClausewitzNode block, Func<CCountryTag, bool> isCountry,
		string path, Action<string> warn, out int failedAt)
	{
		failedAt = block.Line;
		if (!block.IsBlock)
		{
			return null;
		}
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null || !Keys.Contains(entry.Key))
			{
				failedAt = entry.Line;
				return null;
			}
		}
		string name = block.GetString("name");
		var offered = new List<CCountryTag>();
		foreach (ClausewitzNode country in block.All("country"))
		{
			if (CCountryTag.TryParse(country.Value, out CCountryTag tag) && isCountry(tag))
			{
				offered.Add(tag);
			}
			else
			{
				warn(CDiagnostics.Located(path, country.Line, $"Bad tag {country.Value} in bookmark {name}"));
			}
		}
		// year.month.day.hour, the hour nought where it is left out; a date that does not read is the calendar's start.
		long hours = CSaveValues.TryHours(block.GetString("date"), out long read) ? read : 0;
		bool custom = block.GetString("is_cgm").Equals("yes", StringComparison.OrdinalIgnoreCase);
		return (new CBookmark(block.GetString("icon"), name, block.GetString("desc"), hours, offered), custom);
	}
}
