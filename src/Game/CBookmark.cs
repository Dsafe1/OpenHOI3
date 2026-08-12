using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Core;

namespace OpenHOI3.Game;

/// <summary>
/// A grand campaign starting point: a date, some flavour text, and the countries the
/// front end offers as suggestions for it.
/// </summary>
public sealed class CBookmark
{
	public required CDate Date { get; init; }

	/// <summary>Sprite name for the panel background, e.g. GFX_bg_startdate_1936.</summary>
	public string Icon { get; init; } = "";

	public required string Name { get; init; }
	public required string Description { get; init; }

	/// <summary>Countries this start date puts forward, in declaration order.</summary>
	public required IReadOnlyList<CCountryTag> Countries { get; init; }

	public override string ToString()
	{
		return $"{Date} {Name}";
	}
}

/// <summary>
/// The start dates from <c>common/bookmarks.txt</c>, oldest first.
/// </summary>
public sealed class CBookmarks
{
	private const string File = "common/bookmarks.txt";

	private readonly List<CBookmark> _bookmarks = new();

	public int Count => _bookmarks.Count;

	public IReadOnlyList<CBookmark> All => _bookmarks;

	public static CBookmarks Load(CDirectorySettings directories, CLocalisation localisation,
		Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var bookmarks = new CBookmarks();

		string path = directories.Resolve(File);
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (Exception e)
		{
			warn($"Could not read '{path}': {e.Message}");
			return bookmarks;
		}

		foreach (ClausewitzNode node in root.All("bookmark"))
		{
			if (!CDate.TryParse(node.GetString("date"), out CDate date))
			{
				warn($"Bookmark '{node.GetString("name")}' has no usable date");
				continue;
			}

			var countries = new List<CCountryTag>();
			// The countries are repeated "country = TAG" lines rather than a list.
			foreach (ClausewitzNode entry in node.All("country"))
			{
				if (CCountryTag.TryParse(entry.Value, out CCountryTag tag))
				{
					countries.Add(tag);
				}
			}

			bookmarks._bookmarks.Add(new CBookmark
			{
				Date = date,
				Icon = node.GetString("icon"),
				Name = localisation.Localise(node.GetString("name")),
				Description = localisation.Localise(node.GetString("desc")),
				Countries = countries,
			});
		}

		bookmarks._bookmarks.Sort((a, b) => a.Date.CompareTo(b.Date));
		return bookmarks;
	}
}
