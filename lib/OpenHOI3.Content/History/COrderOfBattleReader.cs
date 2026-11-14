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
	/// ceiling and its experience left as the file gives it, and no leader chosen. A unit with no place is left out. A line of the build queue in
	/// such a file is not built, and is reported.
	/// </summary>
	public static void ReadInto(CDirectorySettings directories, CGameState state, CCountry country, string file, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		string path = directories.Resolve($"{Directory}/{file}");
		if (!File.Exists(path))
		{
			return;
		}
		ClausewitzNode root = ClausewitzParser.ParseFile(path);
		var reading = new CReading(state.SubUnitTypes, state.Technologies, state.Provinces.Count);
		reading.StartAfter(state.LastUnitId);
		var made = new List<CUnit>();
		foreach (ClausewitzNode entry in root.Children)
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
			}
			else if (entry.Key.Equals(Construction, StringComparison.OrdinalIgnoreCase))
			{
				warn($"{country.Tag}: '{file}' has a line of the build queue, which a game under way does not take from such a file");
			}
		}
		state.LastUnitId = reading.LastId;
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
	public static int Read(CDirectorySettings directories, IEnumerable<CCountry> countries,
		IReadOnlyDictionary<string, CSubUnitType> unitTypes, IReadOnlyDictionary<string, CTechnology> technologies,
		int provinceCount, Action<string>? warn = null, Func<CCountry, CSubUnitType, string>? nameSubUnit = null,
		List<(CCountry Country, CUnit Unit)>? unnamed = null)
	{
		warn ??= CDiagnostics.Warn;
		var reading = new CReading(unitTypes, technologies, provinceCount) { NameSubUnit = nameSubUnit, Unnamed = unnamed };

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

			foreach (ClausewitzNode entry in root.Children)
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
				else
				{
					reading.UnknownKeys.Add(entry.Key);
				}
			}
		}

		Report(warn, reading.UnknownKeys, "keys in orders of battle are not understood");
		Report(warn, reading.UnknownTypes, "brigades are of a type that is not defined and were left out");
		Report(warn, reading.NoType, "brigades have no type and were left out");
		Report(warn, reading.Nowhere, "units at the top of an order of battle have no location and were left out");
		Report(warn, reading.BadLocations, "units are in a province that does not exist and were put in province 1");
		Report(warn, reading.BadNumbers, "figures in orders of battle are not numbers and were ignored");
		Report(warn, reading.NotApplied, "keys of the country's own in orders of battle are not applied from there");
		return reading.LastId;
	}

	private static void Report(Action<string> warn, SortedSet<string> found, string what)
	{
		if (found.Count > 0)
		{
			warn($"{found.Count} {what}: {string.Join("; ", found)}");
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
		public void ReadConstruction(ClausewitzNode block, CCountry owner)
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

		public CUnit ReadUnit(ClausewitzNode block, EUnitKind kind, CCountry owner, CUnit? parent, CSubUnit? carriedBy)
		{
			var unit = new CUnit(kind, owner.Tag) { Parent = parent, CarriedBy = carriedBy };
			foreach (ClausewitzNode entry in block.Children)
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
					// A file gives the leader's number; a savegame a block holding it.
					unit.LeaderId = entry.IsBlock ? entry.GetInt("id") : Whole(entry.Value);
				}
				else if (key.Equals("is_reserve", StringComparison.OrdinalIgnoreCase))
				{
					unit.IsReserve = IsYes(entry.Value);
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

			unit.SubUnits.Add(subUnit);
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
