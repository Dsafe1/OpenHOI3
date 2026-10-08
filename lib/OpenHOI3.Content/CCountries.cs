using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content;

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
		warn ??= CDiagnostics.Warn;
		var countries = new CCountries();

		string listPath = directories.Resolve(TagListFile);
		ClausewitzNode list;
		try
		{
			list = ClausewitzParser.ParseFile(listPath);
		}
		catch (Exception e)
		{
			warn($"Could not read '{listPath}': {e.Message}");
			return countries;
		}

		foreach (ClausewitzNode entry in list.Children)
		{
			if (entry.Key is null || entry.Value is null)
			{
				continue;
			}
			if (!CCountryTag.TryParse(entry.Key, out CCountryTag tag))
			{
				continue;
			}

			CCountry country = Read(directories, localisation, tag, entry.Value, warn);
			if (countries._byTag.TryAdd(tag.Value, country))
			{
				countries._all.Add(country);
				countries._declared.Add(country);
			}
		}

		countries._all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
		return countries;
	}

	private static CCountry Read(CDirectorySettings directories, CLocalisation localisation,
		CCountryTag tag, string definitionFile, Action<string> warn)
	{
		CColor color = CColor.Gray;
		string culture = "";
		bool major = false;

		string path = directories.Resolve("common/" + definitionFile);
		try
		{
			ClausewitzNode definition = ClausewitzParser.ParseFile(path);
			color = definition.GetRgb("color", CColor.Gray);
			culture = definition.GetString("graphical_culture");
			major = definition.GetString("major").Equals("yes", StringComparison.OrdinalIgnoreCase);
		}
		catch (Exception e)
		{
			// A missing definition costs the colour, not the country.
			warn($"Could not read country file '{path}': {e.Message}");
		}

		return new CCountry
		{
			Tag = tag,
			Name = localisation.Localise(tag.Value),
			DefinitionFile = definitionFile,
			Color = color,
			GraphicalCulture = culture,
			IsMajor = major,
		};
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
