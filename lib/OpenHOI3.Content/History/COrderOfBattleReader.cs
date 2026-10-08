using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
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
	private static readonly HashSet<string> CountryKeysNotApplied = new(StringComparer.OrdinalIgnoreCase)
	{
		"last_election",
	};

	private const string Construction = "military_construction";

	/// <summary>
	/// Gives every country in turn the units its order of battle declares.
	/// </summary>
	/// <param name="provinceCount">One more than the highest province id there is.</param>
	/// <returns>The last number given to a unit or a brigade.</returns>
	public static int Read(CDirectorySettings directories, IEnumerable<CCountry> countries,
		IReadOnlyDictionary<string, CSubUnitType> unitTypes, IReadOnlyDictionary<string, CTechnology> technologies,
		int provinceCount, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var reading = new CReading(unitTypes, technologies, provinceCount);

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
					}
				}
				else if (entry.Key.Equals(Construction, StringComparison.OrdinalIgnoreCase))
				{
					reading.SkipConstruction(entry);
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
		if (reading.Constructions > 0)
		{
			warn($"{reading.Constructions} units under construction in orders of battle were left out: the build queue is not built");
		}
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

		public int Constructions { get; private set; }

		/// <summary>
		/// A unit under construction is not made, but it is numbered: the construction
		/// takes a number, three more go to things a savegame does not show, and the
		/// brigade being built takes the one after. Measured on the thirty in the base
		/// game, each of which builds one brigade; one more number for each further
		/// brigade is this project's guess.
		/// </summary>
		public void SkipConstruction(ClausewitzNode block)
		{
			Constructions++;
			int brigades = 0;
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is not null && entry.IsBlock && _unitTypes.ContainsKey(entry.Key))
				{
					brigades++;
				}
			}
			LastId += 4 + Math.Max(1, brigades);
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
			unit.Id = ++LastId;
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
					// What a carrier has aboard.
					subUnit.Carried.Add(ReadUnit(entry, EUnitKind.Air, owner, parent: null, carriedBy: subUnit));
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
