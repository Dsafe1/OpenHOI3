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
		warn ??= CDiagnostics.Problem;

		// Each fault said once at its file and line, in the files' order (logs.md, section 4).
		foreach (string file in directories.ListFiles(ModelDirectory, "*.txt"))
		{
			// The country a file belongs to is the tag its name starts with, in either case.
			string name = Path.GetFileName(file);
			CCountry? country = name.Length >= CCountryTag.Length
				? countries.Find(name[..CCountryTag.Length].ToUpperInvariant())
				: null;
			if (country is null)
			{
				warn(CDiagnostics.Located(file, 0, "the model file is named for no country and was skipped"));
				continue;
			}

			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (IOException e)
			{
				warn(CDiagnostics.Located(file, 0, $"could not be read: {e.Message}"));
				continue;
			}

			foreach (ClausewitzNode block in root.Children)
			{
				if (block.Key is null || !block.IsBlock)
				{
					// Said, where it was passed over (clausewitz.md, section 3).
					warn(CDiagnostics.Located(file, block.Line, $"'{block.Key ?? block.Value}' is not a unit type and a number"));
					continue;
				}
				string[] parts = block.Key.Split('.');
				if (parts.Length != 2)
				{
					warn(CDiagnostics.Located(file, block.Line, $"'{block.Key}' is not a unit type and a number"));
					continue;
				}
				if (!unitTypes.TryGetValue(parts[0], out CSubUnitType? type))
				{
					warn(CDiagnostics.Located(file, block.Line, $"'{parts[0]}' names a unit type that is not defined"));
					continue;
				}
				// Anything that is not a number is model 0, as the original reads it - and said.
				if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
				{
					warn(CDiagnostics.Located(file, block.Line, $"{block.Key} is no whole number of a model; read as model 0"));
				}

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
					warn(CDiagnostics.Located(file, block.Line,
						$"{block.Key} is numbered past the next model, which would be {count}, and was left out"));
					continue;
				}

				// The model becomes what this block lists, and only that.
				var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				foreach (ClausewitzNode line in block.Children)
				{
					if (line.Key is null || line.IsBlock)
					{
						warn(CDiagnostics.Located(file, line.Line,
							$"'{block.Key} {{ {line.Key ?? line.Value} }}' is not a technology and a level"));
						continue;
					}
					if (!technologies.TryGetValue(line.Key, out CTechnology? technology)
						|| !technology.UnitEffects.ContainsKey(type.Key))
					{
						warn(CDiagnostics.Located(file, line.Line,
							$"'{line.Key}' is not a technology of the unit type {type.Key} and was left out"));
						continue;
					}
					if (!int.TryParse(line.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
					{
						warn(CDiagnostics.Located(file, line.Line, $"{block.Key}: {line.Key} = {line.Value} is no whole number; read as 0"));
					}
					levels[technology.Name] = level;
				}
				models[number] = levels;
			}
		}
	}
}
