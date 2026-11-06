using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Frontend;

/// <summary>
/// Reads <c>common/bookmarks.txt</c> for the one start's look: the bookmark whose date is the start date, of the file's
/// <c>bookmark</c> blocks in order. A country that is none is dropped, as the original drops it, and reported. Only the
/// start's is offered (<c>docs/compatibility.md</c>, entry 17); see <c>docs/mechanics/frontend.md</c>, section 3.
/// </summary>
public static class CBookmarkReader
{
	public const string File = "common/bookmarks.txt";

	/// <summary>The bookmark of the start date, or null where the file has none of that date.</summary>
	public static CBookmark? ReadStart(CDirectorySettings directories, CCountries countries, CDate start, Action<string>? warn = null)
	{
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			return null;
		}
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (IOException e)
		{
			(warn ?? CDiagnostics.Warn)($"Could not read '{path}': {e.Message}");
			return null;
		}
		return Start(root, tag => countries.Find(tag) is not null, start, warn ?? CDiagnostics.Warn);
	}

	/// <summary>The first <c>bookmark</c> block whose <c>date</c> is the start date.</summary>
	public static CBookmark? Start(ClausewitzNode root, Func<CCountryTag, bool> isCountry, CDate start, Action<string> warn)
	{
		foreach (ClausewitzNode block in root.All("bookmark"))
		{
			if (!CDate.TryParse(block.GetString("date"), out CDate date) || date.CompareTo(start) != 0)
			{
				continue;
			}
			var offered = new List<CCountryTag>();
			foreach (ClausewitzNode country in block.All("country"))
			{
				if (CCountryTag.TryParse(country.Value, out CCountryTag tag) && isCountry(tag))
				{
					offered.Add(tag);
				}
				else
				{
					warn($"Bad tag {country.Value} in bookmark {block.GetString("name")}");
				}
			}
			return new CBookmark(block.GetString("icon"), block.GetString("name"), block.GetString("desc"), date, offered);
		}
		return null;
	}
}
