using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.History;

/// <summary>
/// Reads each country's order of battle - the file under <c>history/units</c> its
/// history names - and makes its units as the file gives them. What a brigade then
/// starts a game with is the simulation's rule, <see cref="CUnits.SetAtStart"/>.
///
/// Units and their brigades are numbered as the original numbers them: one count over
/// every country in turn, each block taking the next number as it closes, so a unit's
/// brigades come before it and a division before the corps it is in. See
/// <c>docs/mechanics/units.md</c>, section 3.
/// </summary>
public static class COrderOfBattleReader
{
	private const string Directory = "history/units";
	private const string Regiment = "regiment";

	/// <summary>The line naming the country that lent a unit.</summary>
	private const string ExpeditionaryOwner = "expeditionary_owner";

	/// <summary>The keywords that declare a unit, and what each makes.</summary>
	private static readonly Dictionary<string, EUnitKind> UnitKinds = new(StringComparer.OrdinalIgnoreCase)
	{
		["theatre"] = EUnitKind.Theatre,
		["armygroup"] = EUnitKind.ArmyGroup,
		["army"] = EUnitKind.Army,
		["corps"] = EUnitKind.Corps,
		["division"] = EUnitKind.Division,
		["navy"] = EUnitKind.Navy,
		["air"] = EUnitKind.Air,
	};

	/// <summary>The keywords that declare what a unit is made of.</summary>
	private static readonly HashSet<string> SubUnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		Regiment, "ship", "wing",
	};

	/// <summary>
	/// What else a unit's block may hold in the original. It is what a savegame carries
	/// for a unit in the field, and none of it is kept here yet.
	/// </summary>
	private static readonly HashSet<string> UnitKeysNotKept = new(StringComparer.OrdinalIgnoreCase)
	{
		"id", "path", "track", "previous", "movement_progress", "retreat", "possible_retreat", "disengage",
		"arrow_state", "start_date", "end_date", "was_paradropped", "supplies", "fuel", "dig_in", "attack_delay",
		"combat_hours", "can_upgrade", "can_reinforce", "is_prioritized", "plan", "expeditionary_owner",
		"expeditionary_date", "aggression_enabled", "aggression_setting", "country_intel", "target", "parent",
		"staging_province", "at_sea",
	};

	/// <summary>The same for a brigade's line.</summary>
	private static readonly HashSet<string> SubUnitKeysNotKept = new(StringComparer.OrdinalIgnoreCase)
	{
		"id", "home", "builder", "current_distance", "sunk_by",
	};

	/// <summary>Keys of the country's own that an order of battle may carry, and that are not applied from one.</summary>
	private static readonly HashSet<string> CountryKeysNotApplied = new(StringComparer.OrdinalIgnoreCase);

	private const string Construction = "military_construction";

	/// <summary>
	/// A building's line of the queue and a convoy's: the original reads an order of battle through its country's own
	/// save keys, so a file may hold either (BlackICE's 85 do, every one loaded by an event's <c>load_oob</c>).
	/// </summary>
	private const string BuildingConstruction = "building_construction";
	private const string ConvoyConstruction = "convoy_construction";

	/// <summary>What a building's or a convoy's line may also carry, and that is not kept from a file.</summary>
	private static readonly HashSet<string> OtherLineKeysNotKept = new(StringComparer.OrdinalIgnoreCase)
	{
		"id", "country", "builder",
	};

	/// <summary>What a unit under construction may also carry in the original, and that is not kept here.</summary>
	private static readonly HashSet<string> ConstructionKeysNotKept = new(StringComparer.OrdinalIgnoreCase)
	{
		"id", "unit", "country", "builder", "location", "target",
	};

	/// <summary>The same for one of its brigades.</summary>
	private static readonly HashSet<string> BrigadeOrderKeysNotKept = new(StringComparer.OrdinalIgnoreCase)
	{
		"id", "upgrading_from_subunit", "upgrading_from_subunit_str",
	};

	/// <summary>
	/// Gives every country in turn the units its order of battle declares, and the
	/// units it is building.
	/// </summary>
	/// <param name="provinceCount">One more than the highest province id there is.</param>
	/// <returns>The last number given to a unit or a brigade.</returns>
	/// <summary>
	/// A file of units read into a country as a game runs, for <c>load_oob</c>: placed at once, each brigade held to its
	/// ceiling and its experience left as the file gives it, and no leader chosen. A unit with no place is left out. A line of
	/// the build queue in such a file goes on the end of the country's queue as the file gives it - its cost, its duration
	/// and how far it has got kept, nothing priced again - and numbered in the file's order with the units, a building's
	/// line and a convoy's as well (<see cref="CReading.ReadOtherLine"/>); a brigade's
	/// <c>historical_model</c> is turned into its model as it is read (<c>docs/mechanics/units.md</c>, "A file of units as
	/// a game runs").
	/// </summary>
	public static void ReadInto(CDirectorySettings directories, CGameState state, CCountry country, string file, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		string path = directories.Resolve($"{Directory}/{file}");
		if (!File.Exists(path))
		{
			return;
		}
		ClausewitzNode root = ClausewitzParser.ParseFile(path);
		var reading = new CReading(state.SubUnitTypes, state.Technologies, state.Provinces.Count)
		{
			Leaders = state.Leaders, Buildings = state.Buildings,
		};
		reading.StartAfter(state.LastUnitId);
		var made = new List<CUnit>();
		foreach (ClausewitzNode entry in Entries(root))
		{
			if (entry.Key is null)
			{
				continue;
			}
			if (entry.IsBlock && UnitKinds.TryGetValue(entry.Key, out EUnitKind kind))
			{
				CUnit unit = reading.ReadUnit(entry, kind, country, parent: null, carriedBy: null);
				if (unit.Location != 0)
				{
					country.Units.Add(unit);
					made.Add(unit);
				}
				else
				{
					reading.Nowhere.Add($"{country.Tag} {unit.Name}");
				}
			}
			else if (ReadPairBlock(state, country, entry, reading))
			{
				// What the country holds of another, as the file gives it - as a new game's reading takes it.
			}
			else if (entry.IsBlock && entry.Key.Equals(Construction, StringComparison.OrdinalIgnoreCase))
			{
				// Onto the queue's end as the file has it: a new game's pricing (CBuildQueue.SetUpAtStart) is the entering's,
				// which a game under way does not run again; the model is the original's loader's (buildqueue.md, section 5).
				CConstruction line = reading.ReadConstruction(entry, country);
				foreach (CBrigadeOrder order in line.Brigades)
				{
					if (order.HistoricalModel != CSubUnit.NoModel)
					{
						CBuildQueue.SetModelToHistorical(state, country, order, order.HistoricalModel);
					}
				}
			}
			else if (entry.IsBlock && reading.ReadOtherLine(entry, country))
			{
				// A building's or a convoy's line, onto the queue's end as the file has it, as a unit's is.
			}
			else
			{
				// Said, where the original drops it: a building's line passed over so made an event do nothing unseen.
				reading.UnknownKeys.Add(entry.Key);
			}
		}
		state.LastUnitId = reading.LastId;
		Report(warn, reading.UnknownKeys, $"keys in the order of battle {file} are not understood");
		Report(warn, reading.BadNumbers, $"figures in the order of battle {file} are not numbers and were ignored");
		Report(warn, reading.UnknownTypes, $"brigades in the order of battle {file} are of a type that is not defined and were left out");
		Report(warn, reading.NoType, $"brigades in the order of battle {file} have no type and were left out");
		Report(warn, reading.Nowhere, $"units at the top of the order of battle {file} have no location and were left out");
		Report(warn, reading.BadLocations,
			$"units in the order of battle {file} are in a province that does not exist and were put in province 1");
		Report(warn, reading.UnknownLeaders, UnknownLeadersReport);
		foreach (CUnit unit in made)
		{
			AddLent(unit, tag => state.Country(tag));
		}
		var waiting = new Stack<CUnit>(made);
		while (waiting.Count > 0)
		{
			CUnit part = waiting.Pop();
			foreach (CUnit child in part.Children)
			{
				waiting.Push(child);
			}
			{
				foreach (CSubUnit subUnit in part.SubUnits)
				{
					CUnits.AfterLoad(state, country, subUnit);
				}
				if (part.LeaderId != 0 && state.Leaders.TryGetValue(part.LeaderId, out Sim.Leaders.CLeader? leader))
				{
					Sim.Leaders.CLeaders.SetLeader(state, part, leader, false, false);
				}
			}
		}
		state.ForgetPositions();
	}

	/// <param name="nameSubUnit">
	/// For a new game, what a ship or a wing the file leaves unnamed is called, asked as its block closes: every name is
	/// then registered with its country as it is read, so only a name earlier in the file can take a number from it
	/// (<c>docs/mechanics/units.md</c>, section 3). None gives no names and registers none.
	/// </param>
	/// <param name="unnamed">
	/// Where a new game is told each unit the file leaves unnamed that the original names as a game is entered - an air
	/// group aboard a carrier as its block closes, a unit at the top of the file as it is added - in that order.
	/// </param>
	/// <param name="state">
	/// The game the files are read into, for a block under another country's tag (<see cref="ReadPairBlock"/>); none
	/// reports such a block as not understood.
	/// </param>
	public static int Read(CDirectorySettings directories, IEnumerable<CCountry> countries,
		IReadOnlyDictionary<string, CSubUnitType> unitTypes, IReadOnlyDictionary<string, CTechnology> technologies,
		int provinceCount, Action<string>? warn = null, Func<CCountry, CSubUnitType, string>? nameSubUnit = null,
		List<(CCountry Country, CUnit Unit)>? unnamed = null, IReadOnlyDictionary<int, Sim.Leaders.CLeader>? leaders = null,
		CGameState? state = null)
	{
		warn ??= CDiagnostics.Problem;
		var reading = new CReading(unitTypes, technologies, provinceCount)
		{
			NameSubUnit = nameSubUnit, Unnamed = unnamed, Leaders = leaders, Buildings = state?.Buildings,
		};

		foreach (CCountry country in countries)
		{
			if (country.OrderOfBattleFile.Length == 0)
			{
				continue;
			}
			string path = directories.Resolve($"{Directory}/{country.OrderOfBattleFile}");
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(path);
			}
			catch (IOException e)
			{
				warn($"{country.Tag}: could not read its order of battle '{path}': {e.Message}");
				continue;
			}

			foreach (ClausewitzNode entry in Entries(root))
			{
				if (entry.Key is null)
				{
					continue;
				}
				if (entry.IsBlock && UnitKinds.TryGetValue(entry.Key, out EUnitKind kind))
				{
					CUnit unit = reading.ReadUnit(entry, kind, country, parent: null, carriedBy: null);
					if (unit.Location == 0)
					{
						// A unit that is nowhere is read in full and then thrown away.
						reading.Nowhere.Add($"{country.Tag} {unit.Name}");
					}
					else
					{
						country.Units.Add(unit);
						if (unit.Name.Length == 0)
						{
							unnamed?.Add((country, unit));
						}
					}
				}
				else if (entry.IsBlock && entry.Key.Equals(Construction, StringComparison.OrdinalIgnoreCase))
				{
					reading.ReadConstruction(entry, country);
				}
				else if (entry.IsBlock && reading.ReadOtherLine(entry, country))
				{
					// A building's or a convoy's line - none of the base game's or BlackICE's start files has one.
				}
				else if (entry.Key.Equals("last_election", StringComparison.OrdinalIgnoreCase)
					&& CDate.TryParse(entry.Value, out CDate lastElection))
				{
					// When the country last held an election, as its own file may say too (politics.md, section 2).
					country.LastElection = lastElection;
				}
				else if (CountryKeysNotApplied.Contains(entry.Key))
				{
					reading.NotApplied.Add(entry.Key);
				}
				else if (!ReadPairBlock(state, country, entry, reading))
				{
					reading.UnknownKeys.Add(entry.Key);
				}
			}
		}

		// Each lent unit on its lender's list, in the order the files were read, as a save's reader puts them.
		var byTag = new Dictionary<CCountryTag, CCountry>();
		foreach (CCountry country in countries)
		{
			byTag.TryAdd(country.Tag, country);
		}
		foreach (CCountry country in countries)
		{
			foreach (CUnit unit in country.Units)
			{
				AddLent(unit, tag => byTag.GetValueOrDefault(tag));
			}
		}

		Report(warn, reading.UnknownKeys, "keys in orders of battle are not understood");
		Report(warn, reading.UnknownTypes, "brigades are of a type that is not defined and were left out");
		Report(warn, reading.NoType, "brigades have no type and were left out");
		Report(warn, reading.Nowhere, "units at the top of an order of battle have no location and were left out");
		Report(warn, reading.BadLocations, "units are in a province that does not exist and were put in province 1");
		Report(warn, reading.BadNumbers, "figures in orders of battle are not numbers and were ignored");
		Report(warn, reading.NotApplied, "keys of the country's own in orders of battle are not applied from there");
		Report(warn, reading.UnknownLeaders, UnknownLeadersReport);
		return reading.LastId;
	}

	/// <summary>
	/// A block's entries as the original's reader takes them, where a key written without its <c>=</c> - <c>army {</c> - is
	/// read as though it had one: the original reads a key and the next token, throws that token away where it is not
	/// <c>=</c>, and a unit's loader then reads the block's contents up to its closing brace whatever the value was
	/// (<c>reversing/findings/FINDINGS-effectgrammar.md</c>, section 1; <c>FINDINGS-oob.md</c>). Our parser makes the key a
	/// bare word and the block one with no key, so the two are put back together here. Their Finest Hour's
	/// <c>JAP_DayOfInfamy.txt</c> opens two theatres so and <c>SWE_DayOfInfamy.txt</c> its army, which the start save of the
	/// Day of Infamy bookmark has (<c>docs/mechanics/units.md</c>, section 3).
	/// </summary>
	internal static IEnumerable<ClausewitzNode> Entries(ClausewitzNode block)
	{
		List<ClausewitzNode> children = block.Children;
		for (int i = 0; i < children.Count; i++)
		{
			ClausewitzNode entry = children[i];
			if (entry.Key is null && entry.Value is { Length: > 0 } word && !entry.IsQuoted && i + 1 < children.Count
				&& children[i + 1] is { Key: null, IsBlock: true } unkeyed)
			{
				var keyed = new ClausewitzNode { Key = word };
				keyed.Children.AddRange(unkeyed.Children);
				yield return keyed;
				i++;
				continue;
			}
			yield return entry;
		}
	}

	/// <summary>
	/// A block under another country's tag, which the original's reader - the country's own loader, an order of battle being
	/// read as a piece of a country's save block - takes as what the country holds of that one, key by key: so Their Finest
	/// Hour's German files of 1941 and after, opening with <c>SWE = { military_access = yes }</c>, give Germany leave to
	/// pass through Sweden, as the 1941 bookmarks' start saves hold (<c>docs/mechanics/units.md</c>, section 3). Answers
	/// whether the entry was such a block.
	/// </summary>
	private static bool ReadPairBlock(CGameState? state, CCountry country, ClausewitzNode entry, CReading reading)
	{
		if (state is null || !entry.IsBlock || entry.Key is not { Length: CCountryTag.Length } key
			|| !CCountryTag.TryParse(key, out CCountryTag tag) || state.Country(tag) is not { } other)
		{
			return false;
		}
		Saves.CSaveStatus.Apply(state, CGameState.Status(country, other), entry, unknown => reading.UnknownKeys.Add($"{key}/{unknown}"));
		return true;
	}

	/// <summary>Ours: the original passes them over in silence.</summary>
	private const string UnknownLeadersReport =
		"leaders named in orders of battle are declared by no file and were passed over, as the original passes them over";

	private static void Report(Action<string> warn, SortedSet<string> found, string what)
	{
		if (found.Count > 0)
		{
			warn($"{found.Count} {what}: {string.Join("; ", found)}");
		}
	}

	/// <summary>A unit and every one under it that a file says was lent, put on its lender's list, the unit first.</summary>
	private static void AddLent(CUnit unit, Func<CCountryTag, CCountry?> country)
	{
		if (unit.Lender is { } lender && country(lender) is { } owner)
		{
			owner.LentUnits.Add(unit);
		}
		foreach (CUnit child in unit.Children)
		{
			AddLent(child, country);
		}
	}

	/// <summary>One pass over every country's file: the count of numbers given, and what went wrong.</summary>
	private sealed class CReading
	{
		private readonly IReadOnlyDictionary<string, CSubUnitType> _unitTypes;
		private readonly IReadOnlyDictionary<string, CTechnology> _technologies;
		private readonly int _provinceCount;

		public CReading(IReadOnlyDictionary<string, CSubUnitType> unitTypes,
			IReadOnlyDictionary<string, CTechnology> technologies, int provinceCount)
		{
			_unitTypes = unitTypes;
			_technologies = technologies;
			_provinceCount = provinceCount;
		}

		public int LastId { get; private set; }

		/// <summary>The <c>leader</c> lines taken so far, which places each in the order of the reading (CUnit.LeaderLine).</summary>
		private int _leaderLines;

		/// <summary>What an unnamed ship or wing is called, for a new game; with it, every name read is registered as read.</summary>
		public Func<CCountry, CSubUnitType, string>? NameSubUnit { get; init; }

		/// <summary>Where a new game is told the units it names as it is entered, in the order the original lists them.</summary>
		public List<(CCountry Country, CUnit Unit)>? Unnamed { get; init; }

		private void Registered(CCountry owner, string name)
		{
			if (NameSubUnit is not null)
			{
				CUnitNames.Register(owner, name);
			}
		}

		/// <summary>Numbers what is read after the game's last number, for a file read as a game runs.</summary>
		public void StartAfter(int last)
		{
			LastId = last;
		}

		/// <summary>
		/// A unit being built as the game starts: a line of its country's build queue, as
		/// the file gives it. What it costs and how far it has got are worked out when the
		/// game starts, by <see cref="CBuildQueue.SetUpAtStart"/>; what the file calls
		/// its duration is the days it still has to go.
		///
		/// It takes numbers as it closes: one for the line, one for the unit it will
		/// make, two that are passed over, and one for each of its brigades.
		/// </summary>
		public CConstruction ReadConstruction(ClausewitzNode block, CCountry owner)
		{
			var construction = new CConstruction(owner.Tag);
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null)
				{
					continue;
				}
				string key = entry.Key;
				if (entry.IsBlock && _unitTypes.TryGetValue(key, out CSubUnitType? type))
				{
					construction.Brigades.Add(ReadBrigadeOrder(entry, type));
				}
				else if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
				{
					construction.Name = entry.Value ?? "";
				}
				else if (key.Equals("size", StringComparison.OrdinalIgnoreCase))
				{
					construction.Size = Whole(entry.Value);
				}
				else if (key.Equals("cost", StringComparison.OrdinalIgnoreCase))
				{
					construction.Cost = Number(entry, construction.Cost);
				}
				else if (key.Equals("duration", StringComparison.OrdinalIgnoreCase))
				{
					construction.Duration = Number(entry, construction.Duration);
				}
				else if (key.Equals("progress", StringComparison.OrdinalIgnoreCase))
				{
					construction.Progress = Number(entry, construction.Progress);
				}
				else if (key.Equals("status", StringComparison.OrdinalIgnoreCase))
				{
					construction.Status = Number(entry, construction.Status);
				}
				else if (key.Equals("is_reserve", StringComparison.OrdinalIgnoreCase))
				{
					construction.IsReserve = IsYes(entry.Value);
				}
				else if (key.Equals("manpower", StringComparison.OrdinalIgnoreCase))
				{
					construction.Manpower = Number(entry, construction.Manpower);
				}
				else if (key.Equals("factor", StringComparison.OrdinalIgnoreCase))
				{
					construction.ReservesFactor = Number(entry, construction.ReservesFactor);
				}
				else if (key.Equals("accumulated_experiance", StringComparison.OrdinalIgnoreCase))
				{
					// The original's own spelling.
					construction.AccumulatedExperience = Number(entry, construction.AccumulatedExperience);
				}
				else if (key.Equals("accumulated_progress", StringComparison.OrdinalIgnoreCase))
				{
					construction.AccumulatedProgress = Number(entry, construction.AccumulatedProgress);
				}
				else if (!ConstructionKeysNotKept.Contains(key))
				{
					UnknownKeys.Add($"{key} in a {block.Key}");
				}
			}

			construction.Id = ++LastId;
			construction.UnitId = ++LastId;
			LastId += 2;
			foreach (CBrigadeOrder order in construction.Brigades)
			{
				order.Id = ++LastId;
			}

			// A file's line builds one unit, whatever size it gives.
			if (construction.Size > 1)
			{
				construction.Size = 1;
			}
			owner.Constructions.Add(construction);
			return construction;
		}

		private CBrigadeOrder ReadBrigadeOrder(ClausewitzNode block, CSubUnitType type)
		{
			var order = new CBrigadeOrder(type);
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null)
				{
					continue;
				}
				string key = entry.Key;
				if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
				{
					order.Name = entry.Value ?? "";
				}
				else if (key.Equals("historical_model", StringComparison.OrdinalIgnoreCase))
				{
					order.HistoricalModel = Whole(entry.Value);
				}
				else if (key.Equals("experience", StringComparison.OrdinalIgnoreCase))
				{
					order.Experience = Number(entry, order.Experience);
				}
				else if (entry.IsBlock && key.Equals("model", StringComparison.OrdinalIgnoreCase))
				{
					// The levels themselves, as a savegame writes them.
					order.Model.Clear();
					foreach (ClausewitzNode level in entry.Children)
					{
						order.Model.Add(Whole(level.Value));
					}
				}
				else if (!BrigadeOrderKeysNotKept.Contains(key))
				{
					UnknownKeys.Add($"{key} in a {block.Key} under construction");
				}
			}
			return order;
		}

		public SortedSet<string> UnknownKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> UnknownTypes { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> NoType { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> Nowhere { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> BadLocations { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> BadNumbers { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> NotApplied { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> UnknownLeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// The game's leaders, every file's, by number: a <c>leader</c> line naming none of them is passed over, leaving the
		/// unit whatever an earlier line gave it. None takes every number at its word.
		/// </summary>
		public IReadOnlyDictionary<int, Sim.Leaders.CLeader>? Leaders { get; init; }

		/// <summary>The game's buildings in their file's order, which a building's line names by place; none reads no such line.</summary>
		public IReadOnlyList<Sim.Provinces.CBuilding>? Buildings { get; init; }

		/// <summary>
		/// A <c>building_construction</c> or a <c>convoy_construction</c> block put on the end of its country's queue as the
		/// file gives it, as a save's are read (<c>docs/mechanics/savegame.md</c>, section 5): a building's by its place in
		/// <c>common/buildings.txt</c>, counted from one, and a convoy's escorts by a second <c>status</c> of yes. Its cost,
		/// duration and progress are the file's. It takes the next number. Answers false for any other block.
		/// </summary>
		public bool ReadOtherLine(ClausewitzNode block, CCountry owner)
		{
			bool building = block.Key!.Equals(BuildingConstruction, StringComparison.OrdinalIgnoreCase);
			if (!building && !block.Key.Equals(ConvoyConstruction, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			CConstruction line;
			if (building)
			{
				int index = Whole(block.Get("building")?.Value) - 1;
				if (Buildings is null || index < 0 || index >= Buildings.Count)
				{
					UnknownKeys.Add($"a {block.Key} of no building known, left out");
					return true;
				}
				line = new CConstruction(owner.Tag) { Building = Buildings[index] };
			}
			else
			{
				bool escorts = false;
				foreach (ClausewitzNode status in block.Children)
				{
					if (status.Key == "status" && status.Value is "yes" or "no")
					{
						escorts = status.Value == "yes";
					}
				}
				line = new CConstruction(owner.Tag) { IsConvoy = true, IsEscorts = escorts };
			}
			foreach (ClausewitzNode entry in block.Children)
			{
				switch (entry.Key?.ToLowerInvariant())
				{
					case null:
					case "building":
						break;
					case "size":
						line.Size = Math.Max(1, Whole(entry.Value));
						break;
					case "cost":
						line.Cost = Number(entry, line.Cost);
						break;
					case "duration":
						line.Duration = Number(entry, line.Duration);
						break;
					case "progress":
						line.Progress = Number(entry, line.Progress);
						break;
					case "status" when entry.Value is not ("yes" or "no"):
						line.Status = Number(entry, line.Status);
						break;
					case "status":
						break;
					case "location":
						line.Location = Whole(entry.Value);
						break;
					default:
						if (!OtherLineKeysNotKept.Contains(entry.Key!))
						{
							UnknownKeys.Add($"{entry.Key} in a {block.Key}");
						}
						break;
				}
			}
			line.Id = ++LastId;
			owner.Constructions.Add(line);
			return true;
		}

		public CUnit ReadUnit(ClausewitzNode block, EUnitKind kind, CCountry owner, CUnit? parent, CSubUnit? carriedBy)
		{
			var unit = new CUnit(kind, owner.Tag) { Parent = parent, CarriedBy = carriedBy };
			foreach (ClausewitzNode entry in Entries(block))
			{
				if (entry.Key is null)
				{
					continue;
				}
				string key = entry.Key;
				if (entry.IsBlock && UnitKinds.TryGetValue(key, out EUnitKind childKind))
				{
					unit.Children.Add(ReadUnit(entry, childKind, owner, unit, carriedBy: null));
				}
				else if (entry.IsBlock && SubUnitKeywords.Contains(key))
				{
					ReadSubUnit(entry, key, unit, owner);
				}
				else if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
				{
					unit.Name = entry.Value ?? "";
					Registered(owner, unit.Name);
				}
				else if (key.Equals("location", StringComparison.OrdinalIgnoreCase))
				{
					unit.Location = Province(entry.Value, $"{owner.Tag} {unit.Name}");
				}
				else if (key.Equals("base", StringComparison.OrdinalIgnoreCase))
				{
					unit.Base = Whole(entry.Value);
				}
				else if (key.Equals("leader", StringComparison.OrdinalIgnoreCase))
				{
					// A file gives the leader's number; a savegame a block holding it. The original looks it up among every
					// leader of the game as the line is read and, finding none, does nothing at all (leaders.md, section 5).
					int leader = entry.IsBlock ? entry.GetInt("id") : Whole(entry.Value);
					if (Leaders is null || Leaders.ContainsKey(leader))
					{
						unit.LeaderId = leader;
						unit.LeaderLine = ++_leaderLines;
					}
					else
					{
						UnknownLeaders.Add($"{owner.Tag} {leader}");
					}
				}
				else if (key.Equals("is_reserve", StringComparison.OrdinalIgnoreCase))
				{
					unit.IsReserve = IsYes(entry.Value);
				}
				else if (key.Equals(ExpeditionaryOwner, StringComparison.OrdinalIgnoreCase)
					&& CCountryTag.TryParse(entry.Value, out CCountryTag lender))
				{
					// A unit the file says another country lent: its lender's in its figures and in what it draws, the
					// original's unit loader taking the line from a history file as from a save - Their Finest Hour's
					// German, British and Finnish files of 1940 on (expeditions.md, section 4; supply.md, section 13).
					unit.Lender = lender;
				}
				else if (!UnitKeysNotKept.Contains(key))
				{
					UnknownKeys.Add($"{key} in a {block.Key}");
				}
			}
			// An air group aboard a carrier is based where the carrier's fleet is (transport.md, section 13.1); a
			// savegame writes it no base.
			foreach (CSubUnit ship in unit.SubUnits)
			{
				foreach (CUnit aboard in ship.Carried)
				{
					aboard.Base = unit.Location;
				}
			}
			unit.Id = ++LastId;
			// An air group aboard a carrier joins its country's list of units as its block closes, unnamed or not.
			if (carriedBy is not null && unit.Name.Length == 0)
			{
				Unnamed?.Add((owner, unit));
			}
			return unit;
		}

		private void ReadSubUnit(ClausewitzNode block, string keyword, CUnit unit, CCountry owner)
		{
			string typeKey = block.GetString("type");
			if (!_unitTypes.TryGetValue(typeKey, out CSubUnitType? type))
			{
				(typeKey.Length == 0 ? NoType : UnknownTypes).Add(
					typeKey.Length == 0 ? $"{owner.Tag} {block.GetString("name")}" : typeKey);
				LastId++;                                        // it is numbered all the same
				return;
			}

			// Only a regiment takes its unit's word that it is a reserve, and only if the
			// unit has said so by the time the regiment is read.
			bool fromUnit = keyword.Equals(Regiment, StringComparison.OrdinalIgnoreCase);
			var subUnit = new CSubUnit(type) { Unit = unit, IsReserve = fromUnit && unit.IsReserve };
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null)
				{
					continue;
				}
				string key = entry.Key;
				if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
				{
					subUnit.Name = entry.Value ?? "";
					Registered(owner, subUnit.Name);
				}
				else if (key.Equals("historical_model", StringComparison.OrdinalIgnoreCase))
				{
					subUnit.HistoricalModel = Whole(entry.Value);
				}
				else if (key.Equals("experience", StringComparison.OrdinalIgnoreCase))
				{
					subUnit.Experience = Number(entry, subUnit.Experience);
				}
				else if (key.Equals("strength", StringComparison.OrdinalIgnoreCase))
				{
					subUnit.Strength = Number(entry, subUnit.Strength);
				}
				else if (key.Equals("organisation", StringComparison.OrdinalIgnoreCase))
				{
					subUnit.Organisation = Number(entry, subUnit.Organisation);
				}
				else if (key.Equals("highest", StringComparison.OrdinalIgnoreCase))
				{
					subUnit.Highest = Number(entry, subUnit.Highest);
				}
				else if (key.Equals("is_reserve", StringComparison.OrdinalIgnoreCase))
				{
					subUnit.IsReserve = IsYes(entry.Value);
				}
				else if (key.Equals("pride", StringComparison.OrdinalIgnoreCase))
				{
					subUnit.IsPride = IsYes(entry.Value);
				}
				else if (entry.IsBlock && key.Equals("air", StringComparison.OrdinalIgnoreCase))
				{
					// What a carrier has aboard: hung under the fleet as its block is met, the original's CShip reading
					// (transport.md, section 13.1).
					CUnit group = ReadUnit(entry, EUnitKind.Air, owner, parent: unit, carriedBy: subUnit);
					subUnit.Carried.Add(group);
					unit.Children.Add(group);
				}
				else if (entry.IsBlock && _technologies.ContainsKey(key))
				{
					// A level and how far the next has got, as a savegame writes them.
					if (subUnit.TechnologyLevels.ContainsKey(key) && entry.Children.Count >= 2)
					{
						subUnit.TechnologyLevels[key] = Whole(entry.Children[0].Value);
						subUnit.TechnologyProgress[key] = Number(entry.Children[1], CFixedPoint.Zero);
					}
				}
				else if (!SubUnitKeysNotKept.Contains(key))
				{
					UnknownKeys.Add($"{key} in a {keyword}");
				}
			}
			if (fromUnit && unit.IsReserve)
			{
				subUnit.IsReserve = true;
			}
			subUnit.Id = ++LastId;

			unit.AddSubUnit(subUnit);
			if (subUnit.Name.Length == 0 && fromUnit)
			{
				// A regiment nobody named is its place in its unit, and the unit's name.
				subUnit.Name = $"{unit.SubUnits.Count.ToString(CultureInfo.InvariantCulture)}. {unit.Name}";
			}
			else if (subUnit.Name.Length == 0 && NameSubUnit is not null)
			{
				// A ship or a wing nobody named is named as its block closes, a new game's "1st CAG" (units.md, section 3).
				subUnit.Name = NameSubUnit(owner, type);
				CUnitNames.Register(owner, subUnit.Name);
			}
		}

		private int Province(string? text, string who)
		{
			int id = Whole(text);
			if (id <= 0 || id >= _provinceCount)
			{
				BadLocations.Add($"{who} at {text}");
				return 1;
			}
			return id;
		}

		private CFixedPoint Number(ClausewitzNode entry, CFixedPoint otherwise)
		{
			if (CFixedPoint.TryParse(entry.Value, out CFixedPoint value))
			{
				return value;
			}
			BadNumbers.Add($"{entry.Key} = {entry.Value}");
			return otherwise;
		}

		/// <summary>A whole number the way the original reads one: nothing for anything that is not.</summary>
		private static int Whole(string? text)
		{
			return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
		}

		private static bool IsYes(string? text)
		{
			return "yes".Equals(text, StringComparison.OrdinalIgnoreCase);
		}
	}
}
