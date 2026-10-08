using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads the historical models a country's files spell out: every file under
/// <c>units/models</c>, each named for the country it belongs to and holding blocks
/// named <c>&lt;unit type&gt;.&lt;number&gt;</c> of technology levels. What a model no
/// file gives holds is the simulation's rule, <see cref="CHistoricalModels"/>. See
/// <c>docs/mechanics/units.md</c>, section 5.
/// </summary>
public static class CHistoricalModelReader
{
	private const string ModelDirectory = "units/models";

	/// <param name="defaultCount">How many models every country has of every type before any file is read.</param>
	public static void Read(CDirectorySettings directories, CCountries countries,
		IReadOnlyDictionary<string, CSubUnitType> unitTypes, IReadOnlyDictionary<string, CTechnology> technologies,
		int defaultCount, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var broken = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var unknownTypes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var outOfOrder = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var notForType = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var noCountry = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (string file in directories.ListFiles(ModelDirectory, "*.txt"))
		{
			// The country a file belongs to is the tag its name starts with, in either case.
			string name = Path.GetFileName(file);
			CCountry? country = name.Length >= CCountryTag.Length
				? countries.Find(name[..CCountryTag.Length].ToUpperInvariant())
				: null;
			if (country is null)
			{
				noCountry.Add(name);
				continue;
			}

			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (IOException e)
			{
				warn($"Could not read '{file}': {e.Message}");
				continue;
			}

			foreach (ClausewitzNode block in root.Children)
			{
				if (block.Key is null || !block.IsBlock)
				{
					continue;
				}
				string[] parts = block.Key.Split('.');
				if (parts.Length != 2)
				{
					broken.Add($"{name}: {block.Key}");
					continue;
				}
				if (!unitTypes.TryGetValue(parts[0], out CSubUnitType? type))
				{
					unknownTypes.Add($"{name}: {parts[0]}");
					continue;
				}
				// Anything that is not a number is model 0, as the original reads it.
				int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number);

				if (!country.HistoricalModels.TryGetValue(type.Key, out SortedDictionary<int, Dictionary<string, int>>? models))
				{
					country.HistoricalModels[type.Key] = models = new SortedDictionary<int, Dictionary<string, int>>();
				}
				// A number past the models there are has to be the very next one.
				int count = defaultCount;
				foreach (int given in models.Keys)
				{
					count = Math.Max(count, given + 1);
				}
				if (number < 0 || number > count)
				{
					outOfOrder.Add($"{name}: {block.Key}, where the next would be {count}");
					continue;
				}

				// The model becomes what this block lists, and only that.
				var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				foreach (ClausewitzNode line in block.Children)
				{
					if (line.Key is null || line.IsBlock)
					{
						continue;
					}
					if (!technologies.TryGetValue(line.Key, out CTechnology? technology)
						|| !technology.UnitEffects.ContainsKey(type.Key))
					{
						notForType.Add($"{line.Key} for {type.Key}");
						continue;
					}
					int.TryParse(line.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level);
					levels[technology.Name] = level;
				}
				models[number] = levels;
			}
		}

		Report(warn, noCountry, "model files are named for no country and were skipped");
		Report(warn, broken, "statements in model files are not a unit type and a number");
		Report(warn, unknownTypes, "statements in model files name a unit type that is not defined");
		Report(warn, outOfOrder, "models are numbered past the next one and were left out");
		Report(warn, notForType, "technologies in model files are not technologies of that unit type and were left out");
	}

	private static void Report(Action<string> warn, SortedSet<string> found, string what)
	{
		if (found.Count > 0)
		{
			warn($"{found.Count} {what}: {string.Join("; ", found)}");
		}
	}
}
