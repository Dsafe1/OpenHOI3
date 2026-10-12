using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Content.History;

/// <summary>
/// Gives every country its start: the people its own file declares, and the laws and
/// the government its history file puts in place on the start date. See
/// <c>docs/mechanics/modifiers.md</c>, section 2.
///
/// A history file can also hand the country goods. Those are returned rather than
/// applied, because what a stockpile starts with is the simulation's rule and not the
/// reader's: <c>docs/mechanics/stockpile.md</c>, section 2.
/// </summary>
public static class CCountryHistory
{
	private const string HistoryDirectory = "history/countries";

	/// <returns>The goods each country's history file gives it by the start date, by tag.</returns>
	public static Dictionary<string, CGoodsPool> Apply(CDirectorySettings directories, CCountries countries, CDate start,
		Action<string>? warn = null, IReadOnlyCollection<CTechnology>? technologies = null,
		IReadOnlyCollection<CTechnologyCategory>? categories = null)
	{
		warn ??= CDiagnostics.Warn;
		var goodsGiven = new Dictionary<string, CGoodsPool>(StringComparer.OrdinalIgnoreCase);
		int badAmounts = 0;
		var categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CTechnologyCategory category in categories ?? Array.Empty<CTechnologyCategory>())
		{
			categoryNames.Add(category.Name);
		}
		var technologyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CTechnology technology in technologies ?? Array.Empty<CTechnology>())
		{
			technologyNames.Add(technology.Name);
		}

		Dictionary<string, Dictionary<string, CLaw>> laws = CModifierReader.ReadLaws(directories, warn);
		Dictionary<string, CMinisterType> ministerTypes = CModifierReader.ReadMinisterTypes(directories, warn);
		HashSet<string> positions = CModifierReader.ReadGovernmentPositions(directories, warn);

		var unknownTypes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CCountry country in countries.All)
		{
			ReadMinisters(directories, country, positions, ministerTypes, unknownTypes);
		}
		if (unknownTypes.Count > 0)
		{
			warn($"ministers are declared with {unknownTypes.Count} minister types that are not defined: " +
				string.Join(", ", unknownTypes));
		}

		var unknownLaws = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		int unknownMinisters = 0;
		int unseated = 0;
		foreach (string file in directories.ListFiles(HistoryDirectory, "*.txt"))
		{
			// The country a file belongs to is the tag its name starts with.
			string name = Path.GetFileName(file);
			if (name.Length < CCountryTag.Length
				|| !CCountryTag.TryParse(name[..CCountryTag.Length], out CCountryTag tag))
			{
				warn($"'{file}': the name does not start with a country tag");
				continue;
			}
			CCountry? country = countries.Find(tag);
			if (country is null)
			{
				warn($"'{file}': there is no country '{tag}'");
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

			foreach (ClausewitzNode entry in CHistory.Fold(root, start))
			{
				// Where it stands on the alignment triangle, set as written: the original clamps
				// nothing here (alignment.md, section 2).
				if (entry.IsBlock && entry.Key is { } blockKey && blockKey.Equals("alignment", StringComparison.OrdinalIgnoreCase))
				{
					if (CFixedPoint.TryParse(entry.GetString("x"), out CFixedPoint x)
						&& CFixedPoint.TryParse(entry.GetString("y"), out CFixedPoint y))
					{
						country.AlignmentX = x.Raw;
						country.AlignmentY = y.Raw;
					}
					else
					{
						badAmounts++;
					}
					continue;
				}
				if (entry.Key is null || entry.Value is null)
				{
					continue;
				}
				if (entry.Key.Equals("ideology", StringComparison.OrdinalIgnoreCase))
				{
					country.RulingIdeology = entry.Value;
					continue;
				}

				if (laws.TryGetValue(entry.Key, out Dictionary<string, CLaw>? group))
				{
					if (group.TryGetValue(entry.Value, out CLaw? law))
					{
						country.Laws[entry.Key] = law;
					}
					else
					{
						unknownLaws.Add($"{entry.Key} = {entry.Value}");
					}
				}
				else if (entry.Key.Equals("capital", StringComparison.OrdinalIgnoreCase))
				{
					if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int capital))
					{
						country.Capital = capital;
					}
				}
				else if (entry.Key.Equals("national_unity", StringComparison.OrdinalIgnoreCase))
				{
					// The original holds it between 1 and 100.
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint unity))
					{
						country.NationalUnity = CFixedPoint.Clamp(unity, CFixedPoint.One, CFixedPoint.FromInt(100));
					}
				}
				else if (entry.Key.Equals("government", StringComparison.OrdinalIgnoreCase))
				{
					country.GovernmentType = entry.Value;
				}
				else if (entry.Key.Equals("neutrality", StringComparison.OrdinalIgnoreCase))
				{
					// Floored at nothing and not capped, as the original sets it (diplomacy.md, section 6).
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint neutrality))
					{
						country.Neutrality = CFixedPoint.Max(neutrality, CFixedPoint.Zero);
					}
					else
					{
						badAmounts++;
					}
				}
				else if (entry.Key.Equals("threat", StringComparison.OrdinalIgnoreCase))
				{
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint threat))
					{
						country.HistoryThreat = threat;
					}
					else
					{
						badAmounts++;
					}
				}
				else if (entry.Key.Equals("join_faction", StringComparison.OrdinalIgnoreCase))
				{
					country.Faction = entry.Value;
				}
				else if (entry.Key.Equals("leave_faction", StringComparison.OrdinalIgnoreCase))
				{
					if (country.Faction.Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
					{
						country.Faction = "";
					}
				}
				else if (entry.Key.Equals("oob", StringComparison.OrdinalIgnoreCase))
				{
					// The file is read once the country is otherwise set up; a later line replaces an earlier one.
					country.OrderOfBattleFile = entry.Value;
				}
				else if (entry.Key.Equals("officers_ratio", StringComparison.OrdinalIgnoreCase))
				{
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint ratio))
					{
						country.OfficersRatio = ratio;
					}
					else
					{
						badAmounts++;
					}
				}
				else if (CGoodsPool.TryParseKey(entry.Key, out EGoods goods))
				{
					// Each line gives that much more, so two lines for one good add up.
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint amount))
					{
						if (!goodsGiven.TryGetValue(tag.Value, out CGoodsPool? given))
						{
							goodsGiven[tag.Value] = given = new CGoodsPool();
						}
						given[goods] += amount;
					}
					else
					{
						badAmounts++;
					}
				}
				else if (categoryNames.Contains(entry.Key))
				{
					// A category's key sets the country's ability in it.
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint ability))
					{
						country.Abilities[entry.Key] = ability;
					}
					else
					{
						badAmounts++;
					}
				}
				else if (technologyNames.Contains(entry.Key))
				{
					// A technology's key sets the level the country has of it.
					if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
					{
						country.TechnologyLevels[entry.Key] = level;
					}
				}
				else if (positions.Contains(entry.Key))
				{
					if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
						&& country.Ministers.TryGetValue(id, out CMinister? minister))
					{
						country.Government[entry.Key] = minister;
						if (minister.TypeIn(entry.Key) is null)
						{
							unseated++;
						}
					}
					else
					{
						unknownMinisters++;
					}
				}
			}
		}

		if (unknownLaws.Count > 0)
		{
			warn($"country history names {unknownLaws.Count} laws that are not defined: " +
				string.Join(", ", unknownLaws));
		}
		if (unknownMinisters > 0)
		{
			warn($"country history fills {unknownMinisters} positions with ministers the country does not declare");
		}
		if (unseated > 0)
		{
			warn($"{unseated} ministers hold a position they were declared with no type for, and add nothing");
		}
		if (badAmounts > 0)
		{
			warn($"{badAmounts} amounts in country history are not numbers and were ignored");
		}
		return goodsGiven;
	}

	/// <summary>
	/// Reads the <c>ministers</c> block of a country's own file. Each minister has one
	/// line per position he could hold, naming the minister type he would be in it.
	/// </summary>
	private static void ReadMinisters(CDirectorySettings directories, CCountry country,
		HashSet<string> positions, Dictionary<string, CMinisterType> ministerTypes,
		SortedSet<string> unknownTypes)
	{
		string path = directories.Resolve("common/" + country.DefinitionFile);
		ClausewitzNode definition;
		try
		{
			definition = ClausewitzParser.ParseFile(path);
		}
		catch (IOException)
		{
			return;                                          // already reported when the country was read
		}

		foreach (ClausewitzNode block in definition.All("ministers"))
		{
			foreach (ClausewitzNode entry in block.Children)
			{
				if (!entry.IsBlock
					|| !int.TryParse(entry.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
				{
					continue;
				}
				var minister = new CMinister(id, entry.GetString("name"));
				foreach (ClausewitzNode line in entry.Children)
				{
					if (line.Key is null || line.Value is null || !positions.Contains(line.Key))
					{
						continue;
					}
					if (ministerTypes.TryGetValue(line.Value, out CMinisterType? type))
					{
						minister.SetType(line.Key, type);
					}
					else
					{
						unknownTypes.Add(line.Value);
					}
				}
				country.Ministers[id] = minister;
			}
		}
	}
}
