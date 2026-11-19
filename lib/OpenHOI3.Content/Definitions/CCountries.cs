using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Every country the game defines.
///
/// <c>common/countries.txt</c> is a flat list of tag to definition file. Each
/// definition holds the colour and graphical culture; the readable name comes from
/// localisation, keyed by the tag. Definition files are resolved through the normal
/// layering, so an expansion can replace one country without restating the list.
/// </summary>
public sealed class CCountries
{
	private const string TagListFile = "common/countries.txt";

	/// <summary>
	/// What a country's own file under <c>common/countries</c> may hold: what this reader takes and what the country's
	/// history reads from it (<c>CCountryHistory</c>: the ministers, the last election and its duration).
	/// </summary>
	private static readonly HashSet<string> DefinitionKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"color", "graphical_culture", "major", "default_templates", "unit_names", "ministers", "last_election", "duration",
	};

	private readonly Dictionary<string, CCountry> _byTag =
		new(StringComparer.OrdinalIgnoreCase);
	private readonly List<CCountry> _all = new();
	private readonly List<CCountry> _declared = new();

	public int Count => _all.Count;

	/// <summary>All countries, sorted by name for direct use in a list.</summary>
	public IReadOnlyList<CCountry> All => _all;

	/// <summary>
	/// All countries in the order <c>countries.txt</c> declares them, which is the order
	/// the simulation works through them in. Where two countries share something, the
	/// one declared first comes first.
	/// </summary>
	public IReadOnlyList<CCountry> Declared => _declared;

	/// <summary>
	/// Reads the country list. Diagnostics go to <paramref name="warn"/>, and to
	/// <see cref="CDiagnostics"/> when the caller does not pass one.
	/// </summary>
	public static CCountries Load(CDirectorySettings directories, CLocalisation localisation,
		Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var countries = new CCountries();

		string listPath = directories.Resolve(TagListFile);
		ClausewitzNode list;
		try
		{
			list = ClausewitzParser.ParseFile(listPath);
		}
		catch (Exception e)
		{
			warn(CDiagnostics.Located(listPath, 0, $"could not be read: {e.Message}"));
			return countries;
		}

		// The line of each country's graphical_culture, for a report of it once the cultures are read; nought for none.
		var cultureLines = new Dictionary<CCountry, int>();
		foreach (ClausewitzNode entry in list.Children)
		{
			if (entry.Key is null || entry.Value is null || !CCountryTag.TryParse(entry.Key, out CCountryTag tag))
			{
				// Said, where it was passed over: a line that is no tag and its file (clausewitz.md, section 3).
				warn(CDiagnostics.Located(listPath, entry.Line,
					$"'{entry.Key ?? entry.Value}' is no country's tag and a file; passed over"));
				continue;
			}
			if (countries._byTag.ContainsKey(tag.Value))
			{
				warn(CDiagnostics.Located(listPath, entry.Line, $"{tag} is declared again, to '{entry.Value}'; the first stands"));
				continue;
			}

			CCountry country = Read(directories, localisation, tag, entry.Value, warn, out int cultureLine);
			cultureLines[country] = cultureLine;
			countries._byTag.Add(tag.Value, country);
			countries._all.Add(country);
			countries._declared.Add(country);
		}

		countries._all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
		CheckCultures(directories, countries._declared, cultureLines, warn);
		return countries;
	}

	/// <summary>
	/// A country's <c>graphical_culture</c> against <c>common/graphicalculturetype.txt</c>, a plain list of names. The
	/// original matches the name exactly, case and all, drops one it does not hold without a word, and gives a country
	/// with none <c>Generic</c>, logging "No gfxculturetype set"; it names only the 3D unit figures, which are not drawn
	/// here (<c>reversing/findings/FINDINGS-fontterrain.md</c>, section 4). Both are said (clausewitz.md, section 3).
	/// </summary>
	private static void CheckCultures(CDirectorySettings directories, IEnumerable<CCountry> countries,
		Dictionary<CCountry, int> cultureLines, Action<string> warn)
	{
		string path = directories.Resolve("common/graphicalculturetype.txt");
		if (!System.IO.File.Exists(path))
		{
			return;
		}
		var cultures = new HashSet<string>(StringComparer.Ordinal);
		foreach (ClausewitzNode entry in ClausewitzParser.ParseFile(path).Children)
		{
			if (entry.Key is null && entry.Value is { } name)
			{
				cultures.Add(name);
			}
			else
			{
				warn(CDiagnostics.Located(path, entry.Line, $"'{entry.Key ?? entry.Value}' is no name of a culture; passed over"));
			}
		}
		foreach (CCountry country in countries)
		{
			if (country.GraphicalCulture.Length == 0)
			{
				warn(CDiagnostics.Located(country.DefinitionFile, 0,
					$"{country.Tag} has no graphical_culture; the original gives it Generic"));
			}
			else if (!cultures.Contains(country.GraphicalCulture))
			{
				warn(CDiagnostics.Located(country.DefinitionFile, cultureLines.GetValueOrDefault(country),
					$"{country.Tag}'s graphical_culture '{country.GraphicalCulture}' is no culture " +
					"of graphicalculturetype.txt; the original drops it"));
			}
		}
	}

	private static CCountry Read(CDirectorySettings directories, CLocalisation localisation,
		CCountryTag tag, string definitionFile, Action<string> warn, out int cultureLine)
	{
		CColor color = CColor.Gray;
		string culture = "";
		bool major = false;
		ClausewitzNode? definition = null;
		cultureLine = 0;

		string path = directories.Resolve("common/" + definitionFile);
		try
		{
			definition = ClausewitzParser.ParseFile(path);
			foreach (ClausewitzNode entry in definition.Children)
			{
				if (entry.Key is not null && !DefinitionKeys.Contains(entry.Key))
				{
					warn(CDiagnostics.Located(path, entry.Line, $"'{entry.Key}' is no key of a country's file; passed over"));
				}
			}
			color = definition.GetRgb("color", CColor.Gray);
			culture = definition.GetString("graphical_culture");
			cultureLine = definition.Get("graphical_culture")?.Line ?? 0;
			major = definition.GetString("major").Equals("yes", StringComparison.OrdinalIgnoreCase);
		}
		catch (Exception e)
		{
			// A missing definition costs the colour, not the country.
			warn(CDiagnostics.Located(path, 0, $"could not read the country file: {e.Message}"));
		}

		var country = new CCountry
		{
			Tag = tag,
			Name = localisation.Localise(tag.Value),
			Adjective = localisation.Localise(tag.Value + "_ADJ"),
			DefinitionFile = definitionFile,
			Color = color,
			GraphicalCulture = culture,
			IsMajor = major,
		};
		if (definition is not null)
		{
			ReadTemplates(definition, country);
			ReadUnitNames(definition, country);
		}
		return country;
	}

	/// <summary>
	/// <c>default_templates</c>: a block for each division the country keeps ready to
	/// order, holding its brigades' unit types one after another.
	/// </summary>
	private static void ReadTemplates(ClausewitzNode definition, CCountry country)
	{
		if (definition.Get("default_templates") is not { IsBlock: true } templates)
		{
			return;
		}
		foreach (ClausewitzNode entry in templates.Children)
		{
			if (entry.Key is null || !entry.IsBlock)
			{
				continue;
			}
			var template = new CDivisionTemplate(entry.Key);
			foreach (ClausewitzNode brigade in entry.Children)
			{
				if (brigade.Value is { Length: > 0 } type)
				{
					template.Brigades.Add(type);
				}
			}
			country.Templates.Add(template);
		}
	}

	/// <summary>
	/// <c>unit_names</c>: for a unit type, the names the country gives new units - a
	/// division's under the type of its first brigade.
	/// </summary>
	private static void ReadUnitNames(ClausewitzNode definition, CCountry country)
	{
		if (definition.Get("unit_names") is not { IsBlock: true } lists)
		{
			return;
		}
		foreach (ClausewitzNode entry in lists.Children)
		{
			if (entry.Key is null || !entry.IsBlock)
			{
				continue;
			}
			// A type listed twice has its second list after its first.
			if (!country.UnitNames.TryGetValue(entry.Key, out List<string>? names))
			{
				country.UnitNames[entry.Key] = names = new List<string>();
			}
			foreach (ClausewitzNode name in entry.Children)
			{
				if (name.Value is { Length: > 0 } text)
				{
					names.Add(text);
				}
			}
		}
	}

	public CCountry? Find(CCountryTag tag)
	{
		return Find(tag.Value);
	}

	public CCountry? Find(string? tag)
	{
		if (string.IsNullOrEmpty(tag))
		{
			return null;
		}
		return _byTag.TryGetValue(tag, out CCountry? country) ? country : null;
	}
}
