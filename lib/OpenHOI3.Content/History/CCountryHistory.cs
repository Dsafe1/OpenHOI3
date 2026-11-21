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

	/// <summary>What a minister in a country's <c>ministers</c> block holds besides a line for each position.</summary>
	private static readonly HashSet<string> MinisterKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "ideology", "picture", "loyalty", "start_date", "death_date",
	};

	/// <returns>
	/// What each country's history sets of each good by the start date, by tag: the last line of a good stands.
	/// </returns>
	public static Dictionary<string, Dictionary<EGoods, CFixedPoint>> Apply(CDirectorySettings directories, CCountries countries, CDate start,
		Action<string>? warn = null, IReadOnlyCollection<CTechnology>? technologies = null,
		IReadOnlyCollection<CTechnologyCategory>? categories = null,
		Dictionary<string, Dictionary<string, CLaw>>? lawBook = null, IReadOnlySet<string>? occupationPolicies = null,
		CDate? definesStart = null, List<CMinisterType>? ministerTypeOrder = null)
	{
		warn ??= CDiagnostics.Problem;
		var goodsGiven = new Dictionary<string, Dictionary<EGoods, CFixedPoint>>(StringComparer.OrdinalIgnoreCase);
		// Each fault a line of its own at its place, once (logs.md, section 4).
		var said = new HashSet<string>(StringComparer.Ordinal);
		void Say(string file, int line, string message)
		{
			string located = CDiagnostics.Located(file, line, message);
			if (said.Add(located))
			{
				warn(located);
			}
		}
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

		// The game's own law objects where it gives them, so that a country holds the laws the game knows.
		Dictionary<string, Dictionary<string, CLaw>> laws = lawBook ?? CModifierReader.ReadLaws(directories, warn);
		Dictionary<string, CMinisterType> ministerTypes = CModifierReader.ReadMinisterTypes(directories, warn, ministerTypeOrder);
		HashSet<string> positions = CModifierReader.ReadGovernmentPositions(directories, warn);

		foreach (CCountry country in countries.All)
		{
			ReadMinisters(directories, country, positions, ministerTypes, Say);
		}

		// Every country's ministers by id, made the first time a history names a minister its own file has not.
		Dictionary<int, CMinister>? anyMinister = null;
		foreach (string file in directories.ListFiles(HistoryDirectory, "*.txt"))
		{
			// The country a file belongs to is the tag its name starts with.
			string name = Path.GetFileName(file);
			if (name.Length < CCountryTag.Length
				|| !CCountryTag.TryParse(name[..CCountryTag.Length], out CCountryTag tag))
			{
				Say(file, 0, "the name does not start with a country tag");
				continue;
			}
			CCountry? country = countries.Find(tag);
			if (country is null)
			{
				Say(file, 0, $"there is no country '{tag}'");
				continue;
			}

			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (IOException e)
			{
				Say(file, 0, $"could not read the file: {e.Message}");
				continue;
			}
			void Bad(ClausewitzNode node) => Say(file, node.Line, $"'{node.Key} = {node.Value}' is not a number and was ignored");

			// In the original's passes: the bookmark's own, past the defines' start date, after the rest - which a faction's
			// order of members shows (war.md, section 13).
			foreach ((ClausewitzNode entry, bool late) in CHistory.FoldInPasses(root, definesStart ?? start, start))
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
						Say(file, entry.Line, $"'{entry.Key}' is not two numbers, x and y, and was ignored");
					}
					continue;
				}
				// Each party's figure, raw, a later block changing only the parties it names; a figure below nought
				// is ignored (politics.md, section 2).
				if (entry.IsBlock && entry.Key is { } partiesKey
					&& (partiesKey.Equals("popularity", StringComparison.OrdinalIgnoreCase)
						|| partiesKey.Equals("organization", StringComparison.OrdinalIgnoreCase)))
				{
					Dictionary<string, CFixedPoint> figures = partiesKey.Equals("popularity", StringComparison.OrdinalIgnoreCase)
						? country.HistoryPopularity
						: country.HistoryOrganisation;
					foreach (ClausewitzNode party in entry.Children)
					{
						if (party.Key is null || !CFixedPoint.TryParse(party.Value, out CFixedPoint figure))
						{
							Bad(party);
						}
						else if (figure >= CFixedPoint.Zero)
						{
							figures[party.Key] = figure;
						}
					}
					continue;
				}
				if (entry.Key is null)
				{
					// A bare word: the parser has said where it is (clausewitz.md, section 2).
					continue;
				}
				if (entry.Value is null)
				{
					Say(file, entry.Line, $"'{entry.Key} = {{ }}' names nothing a country's history sets; passed over");
					continue;
				}
				if (entry.Key.Equals("dissent", StringComparison.OrdinalIgnoreCase))
				{
					// Set, and never below nought.
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint dissent))
					{
						country.Dissent = CFixedPoint.Max(dissent, CFixedPoint.Zero);
					}
					else
					{
						Bad(entry);
					}
					continue;
				}
				if (entry.Key.Equals("ideology", StringComparison.OrdinalIgnoreCase))
				{
					country.RulingIdeology = entry.Value;
					continue;
				}

				// A policy's name and a tag: the country's occupation policy towards that country, set once the
				// countries' records exist (occupation.md, section 2).
				if (occupationPolicies is not null && occupationPolicies.Contains(entry.Key)
					&& CCountryTag.TryParse(entry.Value, out CCountryTag occupied))
				{
					country.HistoryOccupationPolicies.Add((entry.Key, occupied));
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
						Say(file, entry.Line, $"'{entry.Key} = {entry.Value}' is a law that is not defined");
					}
				}
				else if (entry.Key.Equals("capital", StringComparison.OrdinalIgnoreCase))
				{
					if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int capital))
					{
						// Both capitals, as the original's history entry sets them, and no original capital: the history's
						// entries are kept apart from the country's own record of its changes, which a new game starts
						// with nothing in (war.md, section 14).
						country.Capital = capital;
						country.ActingCapital = capital;
					}
					else
					{
						Bad(entry);
					}
				}
				else if (entry.Key.Equals("national_unity", StringComparison.OrdinalIgnoreCase))
				{
					// The original holds it between 1 and 100.
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint unity))
					{
						country.NationalUnity = CFixedPoint.Clamp(unity, CFixedPoint.One, CFixedPoint.FromInt(100));
					}
					else
					{
						Bad(entry);
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
						Bad(entry);
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
						Bad(entry);
					}
				}
				else if (entry.Key.Equals("set_country_flag", StringComparison.OrdinalIgnoreCase))
				{
					// A flag the history sets, as the original's replay of a country's history sets it - before the wars'
					// history, whose goals' is_valid reads it: France's fr_signs_peace keeps Germany's vichy_war_goal off
					// a later start (war.md, section 1).
					country.Flags.Set(entry.Value);
				}
				else if (entry.Key.Equals("clr_country_flag", StringComparison.OrdinalIgnoreCase))
				{
					country.Flags.Clear(entry.Value);
				}
				else if (entry.Key.Equals("decision", StringComparison.OrdinalIgnoreCase))
				{
					// Onto the list of decisions taken, whose effects a new game runs (events.md, section 13).
					country.HistoryDecisions.Add(entry.Value);
				}
				else if (entry.Key.Equals("remove_decision", StringComparison.OrdinalIgnoreCase))
				{
					country.HistoryDecisions.Remove(entry.Value);
				}
				else if (entry.Key.Equals("join_faction", StringComparison.OrdinalIgnoreCase))
				{
					country.Faction = entry.Value;
					country.HistoryJoinedLate = late;
				}
				else if (entry.Key.Equals("historical_friend", StringComparison.OrdinalIgnoreCase))
				{
					// Appended with no test of one already held, as the original's (aistrategy.md, section 9.2).
					country.HistoricalFriends.Add(new CCountryTag(entry.Value));
				}
				else if (entry.Key.Equals("leave_faction", StringComparison.OrdinalIgnoreCase))
				{
					if (country.Faction.Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
					{
						country.Faction = "";
					}
				}
				else if (entry.Key.Equals("government_in_exile", StringComparison.OrdinalIgnoreCase))
				{
					// Into exile or back, made once the world stands (war.md, section 14).
					country.HistoryExile.Add(entry.Value.Equals("yes", StringComparison.OrdinalIgnoreCase));
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
						Bad(entry);
					}
				}
				else if (CGoodsPool.TryParseKey(entry.Key, out EGoods goods))
				{
					// Each line sets the good, so of two lines for one good the later stands: Germany's 40000 rare
					// materials, then 15000 at 1938.9.1 and 5000 at 1939.9.1 (stockpile.md, section 2).
					if (CFixedPoint.TryParse(entry.Value, out CFixedPoint amount))
					{
						if (!goodsGiven.TryGetValue(tag.Value, out Dictionary<EGoods, CFixedPoint>? given))
						{
							goodsGiven[tag.Value] = given = new Dictionary<EGoods, CFixedPoint>();
						}
						given[goods] = amount;
					}
					else
					{
						Bad(entry);
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
						Bad(entry);
					}
				}
				else if (technologyNames.Contains(entry.Key))
				{
					// A technology's key sets the level the country has of it.
					if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
					{
						country.TechnologyLevels[entry.Key] = level;
					}
					else
					{
						Bad(entry);
					}
				}
				else if (positions.Contains(entry.Key))
				{
					// The original's history takes the id as it stands, with no look at whose minister it is: BlackICE's
					// Reichskommissariat Ural is given Ukraine's 900004 to 900006, and its save holds them. Here the minister
					// is the country's own, or else the first country's in the declared order with that id (politics.md,
					// section 2).
					if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
						&& (country.Ministers.TryGetValue(id, out CMinister? minister)
							|| (anyMinister ??= AnyMinister(countries)).TryGetValue(id, out minister)))
					{
						country.Government[entry.Key] = minister;
						if (minister.TypeIn(entry.Key) is null)
						{
							Say(file, entry.Line, $"'{entry.Key} = {entry.Value}': the minister holds a position he was declared with " +
								"no type for, and adds nothing");
						}
					}
					else
					{
						Say(file, entry.Line, $"'{entry.Key} = {entry.Value}' fills the position with a minister the country " +
							"does not declare");
					}
				}
				else
				{
					// Said, where nothing was: no law group, position, technology, category, good or key of a country's.
					Say(file, entry.Line, $"'{entry.Key}' names nothing a country's history sets; passed over");
				}
			}
		}
		return goodsGiven;
	}

	/// <summary>
	/// Reads the <c>ministers</c> block of a country's own file. Each minister has one
	/// line per position he could hold, naming the minister type he would be in it.
	/// </summary>
	/// <summary>Every country's ministers by id, the first country in the declared order winning an id two declare.</summary>
	private static Dictionary<int, CMinister> AnyMinister(CCountries countries)
	{
		var ministers = new Dictionary<int, CMinister>();
		foreach (CCountry country in countries.Declared)
		{
			foreach ((int id, CMinister minister) in country.Ministers)
			{
				ministers.TryAdd(id, minister);
			}
		}
		return ministers;
	}

	private static void ReadMinisters(CDirectorySettings directories, CCountry country,
		HashSet<string> positions, Dictionary<string, CMinisterType> ministerTypes,
		Action<string, int, string> say)
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

		// When it last held an election and its own term, which the original reads from this file too
		// (politics.md, section 2).
		if (CDate.TryParse(definition.GetString("last_election"), out CDate lastElection))
		{
			country.LastElection = lastElection;
		}
		if (int.TryParse(definition.GetString("duration"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int duration))
		{
			country.ElectionDuration = duration;
		}

		foreach (ClausewitzNode block in definition.All("ministers"))
		{
			foreach (ClausewitzNode entry in block.Children)
			{
				if (!entry.IsBlock
					|| !int.TryParse(entry.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
				{
					CDiagnostics.Problem(path, entry.Line, $"ministers: '{entry.Key ?? entry.Value}' is no minister - a number " +
						"and a block; passed over");
					continue;
				}
				var minister = new CMinister(id, entry.GetString("name"))
				{
					Ideology = entry.GetString("ideology"),
					Picture = entry.GetString("picture"),
					Loyalty = CFixedPoint.TryParse(entry.GetString("loyalty"), out CFixedPoint loyalty) ? loyalty : CFixedPoint.Zero,
					StartDate = CDate.TryParse(entry.GetString("start_date"), out CDate started) ? started : CDate.Zero,
					DeathDate = CDate.TryParse(entry.GetString("death_date"), out CDate died) ? died : CDate.Zero,
				};
				foreach (ClausewitzNode line in entry.Children)
				{
					if (line.Key is null || line.Value is null || !positions.Contains(line.Key))
					{
						if (line.Key is null || !MinisterKeys.Contains(line.Key))
						{
							CDiagnostics.Problem(path, line.Line, $"minister {id}: '{line.Key ?? line.Value}' is no key of a " +
								"minister's and no position; passed over");
						}
						continue;
					}
					if (ministerTypes.TryGetValue(line.Value, out CMinisterType? type))
					{
						minister.SetType(line.Key, type);
					}
					else
					{
						say(path, line.Line, $"minister {id}: '{line.Value}' is a minister type that is not defined");
					}
				}
				country.Ministers[id] = minister;
			}
		}
	}
}
