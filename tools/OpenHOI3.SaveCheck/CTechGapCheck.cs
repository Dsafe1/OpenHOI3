using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

/// <summary>
/// Looks at the technology levels a start-date savegame has above what the history files
/// give a country, and asks of each whether a unit the country starts with, or one it is
/// building, holds that level already:
///
///   OpenHOI3.SaveCheck techgap &lt;install folder&gt; &lt;start-date savegame&gt; [examples]
///
/// and whether the save's level is the highest one that was due before the game's year,
/// which it nearly always is. It also counts the levels left alone though behind what was
/// due, which are many more. A tag after the number of examples lists one country only.
///
/// It is a look and not a check: nothing here says how the original comes by the levels.
/// <c>docs/mechanics/resources.md</c>, the end, has what it found and what was tried.
/// </summary>
internal static class CTechGapCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck techgap <install folder> <start-date savegame> [examples]");
			return 2;
		}
		int examples = args.Length > 3 && int.TryParse(args[3], out int wanted) ? wanted : 5;
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, firstDays: false);
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);

		var kinds = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
		void Count(string kind, string line)
		{
			if (!kinds.TryGetValue(kind, out List<string>? list))
			{
				kinds[kind] = list = new List<string>();
			}
			list.Add(line);
		}

		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key is null || !node.IsBlock || !CCountryTag.TryParse(node.Key, out CCountryTag tag)
				|| state.Country(tag) is not { } country)
			{
				continue;
			}
			foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.Key is null || !entry.IsBlock || entry.Children.Count == 0
					|| !state.Technologies.TryGetValue(entry.Key, out CTechnology? technology))
				{
					continue;
				}
				int saved = (int)(CFixedPoint.TryParse(entry.Children[0].Value, out CFixedPoint value) ? value : CFixedPoint.Zero).ToInt();
				int ours = country.TechnologyLevels.GetValueOrDefault(entry.Key);

				if (saved == ours)
				{
					// Left where the history put it: was it behind what was due all the same?
					int dueNow = Due(state, technology);
					if (ours < dueNow)
					{
						bool used = state.SubUnitTypes.Values.Any(type => type.Technologies.Contains(technology));
						Count("left alone though behind what was due" + (used ? ", a unit type uses it" : ", no unit type uses it")
							+ (ours == 0 ? ", at nothing" : ", above nothing"),
							$"{tag.Value} {technology.Name}: {ours}, due {dueNow}, folder {technology.Folder}");
					}
					continue;
				}

				// The most any unit it has, or is building, holds of this technology.
				int held = 0;
				string holder = "";
				foreach (CUnit unit in country.AllUnits())
				{
					foreach (CSubUnit subUnit in unit.SubUnits)
					{
						int level = subUnit.TechnologyLevels.GetValueOrDefault(technology.Name);
						if (level > held)
						{
							held = level;
							holder = $"{subUnit.Type.Key} '{subUnit.Name}' model {subUnit.HistoricalModel}";
						}
					}
				}
				int building = 0;
				foreach (CConstruction construction in country.Constructions)
				{
					foreach (CBrigadeOrder order in construction.Brigades)
					{
						if (CHistoricalModels.Level(state, country, order.Type, order.HistoricalModel, technology) is { } level && level > building)
						{
							building = level;
						}
					}
				}

				bool usesIt = state.SubUnitTypes.Values.Any(type => type.Technologies.Contains(technology));
				string kind = saved < ours ? "the save is lower"
					: held == saved ? "a unit it starts with holds exactly the save's level"
					: building == saved ? "a unit it is building holds exactly the save's level"
					: held > saved || building > saved ? "a unit holds more than the save's level"
					: !usesIt ? "no unit type uses the technology"
					: held > ours || building > ours ? "a unit holds more than the history, and less than the save"
					: "a unit type uses it, and no unit of the country holds more than the history";
				Count(kind, $"{tag.Value} {technology.Name}: save {saved}, history {ours}, a unit holds {held}, one being built {building}  {holder}");

				// The other question: is the save's level the highest one due before this year?
				int due = Due(state, technology);
				Count(saved == due ? "by year: the save's level is the highest due before this year"
					: saved < due ? "by year: the save's level is under what was due before this year"
					: "by year: the save's level is over what was due before this year",
					$"{tag.Value} {technology.Name}: save {saved}, history {ours}, due {due}");
			}
		}

		string only = args.Skip(4).FirstOrDefault() ?? "";
		foreach ((string kind, List<string> lines) in kinds)
		{
			if (only.Length > 0)
			{
				lines.RemoveAll(line => !line.StartsWith(only + " ", StringComparison.Ordinal));
			}
		}
		foreach ((string kind, List<string> lines) in kinds)
		{
			Console.WriteLine($"{lines.Count,5}  {kind}");
			foreach (string line in lines.Take(examples))
			{
				Console.WriteLine("            " + line);
			}
		}
		return 0;
	}

	/// <summary>The highest level of a technology that was due before the game's year.</summary>
	private static int Due(CGameState state, CTechnology technology)
	{
		int due = 0;
		for (int level = 1; level <= technology.HighestLevel; level++)
		{
			int year = level == 1 ? technology.StartYear
				: technology.FirstOffset + (level - 2) * technology.AdditionalOffset;
			if (year >= state.Date.Year || (level > 1 && technology.IsSingleLevel))
			{
				break;
			}
			due = level;
		}
		return due;
	}
}
