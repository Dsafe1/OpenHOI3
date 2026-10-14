using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

/// <summary>
/// Sets the units a new game starts with here beside the ones in a savegame of the
/// start date: <c>docs/mechanics/units.md</c>.
///
/// Units are matched by number. The original numbers units and brigades from one count
/// as it reads the orders of battle, and this project numbers them the same way, so a
/// unit of ours and the save's unit of the same number are the same line of the same
/// file - or the numbering is wrong, which the names then show.
///
/// What a reserve starts with depends on two of its country's laws, and the original's
/// AI has chosen laws of its own before the units are made, and goes on changing them
/// afterwards. So each country is tried with the save's law and with its history's, for
/// conscription and for training, and judged with whichever pair its brigades fit best;
/// which countries needed their history's is reported.
/// </summary>
internal static class CUnitCheck
{
	private const string Conscription = "conscription_law";
	private const string Training = "training_laws";

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

	private static readonly HashSet<string> SubUnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"regiment", "ship", "wing",
	};

	private sealed record SavedUnit(ClausewitzNode Node, EUnitKind Kind, string Tag, int Parent);

	private sealed record SavedSubUnit(ClausewitzNode Node, string Tag, int Unit);

	/// <returns>The countries judged with a law out of their history, and which law.</returns>
	public static List<string> Run(CDirectorySettings directories,
		IReadOnlyList<(ClausewitzNode Node, CCountry Country)> countries, CGameState state,
		Dictionary<string, Dictionary<string, CLaw>> lawBook, Action<string, bool, Func<string>> compare)
	{
		var savedUnits = new Dictionary<int, SavedUnit>();
		var savedSubUnits = new Dictionary<int, SavedSubUnit>();
		foreach ((ClausewitzNode node, CCountry country) in countries)
		{
			Collect(node, country.Tag.Value, 0, savedUnits, savedSubUnits);
		}

		// ---- which laws each country's units were made under
		var history = new Dictionary<string, (CLaw? Conscription, CLaw? Training)>();
		var saved = new Dictionary<string, (CLaw? Conscription, CLaw? Training)>();
		foreach ((ClausewitzNode node, CCountry country) in countries)
		{
			history[country.Tag.Value] = (country.Laws.GetValueOrDefault(Conscription), country.Laws.GetValueOrDefault(Training));
			saved[country.Tag.Value] = (Law(lawBook, Conscription, node) ?? country.Laws.GetValueOrDefault(Conscription),
				Law(lawBook, Training, node) ?? country.Laws.GetValueOrDefault(Training));
		}

		(bool Conscription, bool Training)[] choices = { (true, true), (true, false), (false, true), (false, false) };
		var best = new Dictionary<string, (int Faults, (bool Conscription, bool Training) Choice)>();
		foreach ((bool Conscription, bool Training) choice in choices)
		{
			foreach ((ClausewitzNode _, CCountry country) in countries)
			{
				Give(country, choice.Conscription ? saved[country.Tag.Value].Conscription : history[country.Tag.Value].Conscription,
					choice.Training ? saved[country.Tag.Value].Training : history[country.Tag.Value].Training);
			}
			SetUp(directories, state);
			foreach ((ClausewitzNode _, CCountry country) in countries)
			{
				int faults = 0;
				foreach (CUnit unit in country.AllUnits())
				{
					foreach (CSubUnit subUnit in unit.SubUnits)
					{
						if (savedSubUnits.TryGetValue(subUnit.Id, out SavedSubUnit? found)
							&& (Number(found.Node.GetString("highest")) != subUnit.Highest
								|| Number(found.Node.GetString("experience")) != subUnit.Experience))
						{
							faults++;
						}
					}
				}
				if (!best.TryGetValue(country.Tag.Value, out (int Faults, (bool, bool) Choice) soFar) || faults < soFar.Faults)
				{
					best[country.Tag.Value] = (faults, choice);
				}
			}
		}

		var fromHistory = new List<string>();
		foreach ((ClausewitzNode _, CCountry country) in countries)
		{
			string tag = country.Tag.Value;
			(bool useConscription, bool useTraining) = best[tag].Choice;
			Give(country, useConscription ? saved[tag].Conscription : history[tag].Conscription,
				useTraining ? saved[tag].Training : history[tag].Training);
			var which = new List<string>();
			if (!useConscription && saved[tag].Conscription != history[tag].Conscription)
			{
				which.Add("conscription");
			}
			if (!useTraining && saved[tag].Training != history[tag].Training)
			{
				which.Add("training");
			}
			if (which.Count > 0)
			{
				fromHistory.Add($"{tag} ({string.Join(", ", which)})");
			}
		}
		SetUp(directories, state);

		// ---- the comparison
		var savedLevels = new Dictionary<string, Dictionary<string, int>>();
		foreach ((ClausewitzNode node, CCountry country) in countries)
		{
			var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.Key is not null && entry.IsBlock && entry.Children.Count > 0)
				{
					levels[entry.Key] = (int)Number(entry.Children[0].Value).ToInt();
				}
			}
			savedLevels[country.Tag.Value] = levels;
		}
		var seenUnits = new HashSet<int>();
		var seenSubUnits = new HashSet<int>();
		foreach ((ClausewitzNode _, CCountry country) in countries)
		{
			string tag = country.Tag.Value;
			foreach (CUnit unit in country.AllUnits())
			{
				string who = $"{tag} {unit.Kind} {unit.Id} '{unit.Name}'";
				bool found = savedUnits.TryGetValue(unit.Id, out SavedUnit? savedUnit);
				compare("units: each is in the save under its number, with its name and kind",
					found && savedUnit!.Tag == tag && savedUnit.Kind == unit.Kind && savedUnit.Node.GetString("name") == unit.Name,
					() => found ? $"{who}: the save has {savedUnit!.Tag} {savedUnit.Kind} '{savedUnit.Node.GetString("name")}'" : $"{who}: not in the save");
				if (!found)
				{
					continue;
				}
				seenUnits.Add(unit.Id);

				compare("units: where each is", savedUnit!.Node.GetInt("location") == unit.Location,
					() => $"{who}: save {savedUnit.Node.GetInt("location")}, ours {unit.Location}");
				if (unit.Kind is EUnitKind.Navy or EUnitKind.Air && unit.CarriedBy is null)
				{
					compare("units: where a fleet or an air group is based", savedUnit.Node.GetInt("base") == unit.Base,
						() => $"{who}: save {savedUnit.Node.GetInt("base")}, ours {unit.Base}");
				}
				if (unit.Parent is not null)
				{
					// What the files leave at the top, the original puts under a theatre of its own.
					compare("units: which unit commands each, where the files say", savedUnit.Parent == unit.Parent.Id,
						() => $"{who}: save under {savedUnit.Parent}, ours under {unit.Parent.Id} '{unit.Parent.Name}'");
				}

				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					Check(state, country, unit, subUnit, savedSubUnits, seenSubUnits, savedLevels[tag], compare);
				}
			}
		}

		// What the save has and the files do not: the original's AI makes theatres as a game starts.
		foreach (KeyValuePair<int, SavedUnit> extra in savedUnits.Where(unit => !seenUnits.Contains(unit.Key)))
		{
			compare($"units the save has and the files do not: {extra.Value.Kind}", false,
				() => $"{extra.Value.Tag} {extra.Key} '{extra.Value.Node.GetString("name")}' at {extra.Value.Node.GetInt("location")}");
		}
		foreach (KeyValuePair<int, SavedSubUnit> extra in savedSubUnits.Where(subUnit => !seenSubUnits.Contains(subUnit.Key)))
		{
			compare("brigades the save has and the files do not", false,
				() => $"{extra.Value.Tag} {extra.Key} '{extra.Value.Node.GetString("name")}' {extra.Value.Node.GetString("type")}, " +
					$"strength {extra.Value.Node.GetString("strength")}, experience {extra.Value.Node.GetString("experience")}");
		}

		// The rest of the check starts from the history's laws, as it did before this ran.
		foreach ((ClausewitzNode _, CCountry country) in countries)
		{
			Give(country, history[country.Tag.Value].Conscription, history[country.Tag.Value].Training);
		}
		CCountryModifiers.Rebuild(state);
		return fromHistory;
	}

	private static CLaw? Law(Dictionary<string, Dictionary<string, CLaw>> lawBook, string group, ClausewitzNode country)
	{
		return lawBook.TryGetValue(group, out Dictionary<string, CLaw>? laws)
			&& laws.TryGetValue(country.GetString(group), out CLaw? law) ? law : null;
	}

	private static void Give(CCountry country, CLaw? conscription, CLaw? training)
	{
		Set(country, Conscription, conscription);
		Set(country, Training, training);
	}

	private static void Set(CCountry country, string group, CLaw? law)
	{
		if (law is null)
		{
			country.Laws.Remove(group);
		}
		else
		{
			country.Laws[group] = law;
		}
	}

	/// <summary>Makes every country's units afresh under the laws it has now, and gives them their leaders.</summary>
	private static void SetUp(CDirectorySettings directories, CGameState state)
	{
		CCountryModifiers.Rebuild(state);
		CGameStateLoader.ReadUnits(directories, state, _ => { });
		foreach (CCountry country in state.Countries)
		{
			country.Officers = CFixedPoint.Zero;
		}
		CUnits.SetAtStart(state);
		CLeaders.AssignAtStart(state);
	}

	private static void Check(CGameState state, CCountry country, CUnit unit, CSubUnit subUnit,
		Dictionary<int, SavedSubUnit> savedSubUnits, HashSet<int> seen, Dictionary<string, int> savedLevels,
		Action<string, bool, Func<string>> compare)
	{
		string who = $"{country.Tag} {subUnit.Id} '{subUnit.Name}' {subUnit.Type.Key} in '{unit.Name}'";
		bool found = savedSubUnits.TryGetValue(subUnit.Id, out SavedSubUnit? saved);
		bool same = found && saved!.Unit == unit.Id && saved.Node.GetString("type").Equals(subUnit.Type.Key, StringComparison.OrdinalIgnoreCase);
		compare("brigades: each is in the save under its number, in its unit, of its type", same,
			() => found ? $"{who}: the save has '{saved!.Node.GetString("name")}' {saved.Node.GetString("type")} in unit {saved.Unit}" : $"{who}: not in the save");
		if (!found)
		{
			return;
		}
		seen.Add(subUnit.Id);
		if (!same)
		{
			return;
		}
		ClausewitzNode node = saved!.Node;

		// A wing or a ship nobody named is named by a rule that has not been read.
		if (subUnit.Name.Length > 0)
		{
			compare("brigades: names", node.GetString("name") == subUnit.Name,
				() => $"{who}: save '{node.GetString("name")}'");
		}
		else
		{
			compare("brigades: wings the files do not name, which the original names", false,
				() => $"{who}: save '{node.GetString("name")}'");
		}

		bool savedReserve = node.GetString("is_reserve").Equals("yes", StringComparison.OrdinalIgnoreCase);
		compare("brigades: which are reserves", savedReserve == subUnit.IsReserve,
			() => $"{who}: save {savedReserve}, ours {subUnit.IsReserve}");

		CFixedPoint highest = Number(node.GetString("highest"));
		compare("brigades: the strength each started with (the save's 'highest')", highest == subUnit.Highest,
			() => $"{who}: save {highest}, ours {subUnit.Highest}  (most {subUnit.Stats[CSubUnitType.MaxStrength]}, " +
				$"reserve {subUnit.IsReserve}, mobilised {country.Mobilised}, penalty {country.Modifier[CUnits.ReservesPenaltySize]})");

		CFixedPoint strength = Number(node.GetString("strength"));
		compare("brigades: strength two days in is no more than each started with", strength <= subUnit.Strength,
			() => $"{who}: save {strength}, ours {subUnit.Strength}");

		CFixedPoint experience = Number(node.GetString("experience"));
		compare("brigades: experience", experience == subUnit.Experience,
			() => $"{who}: save {experience}, ours {subUnit.Experience}  (reserve {subUnit.IsReserve}, " +
				$"the law gives {country.Modifier[CUnits.UnitStartExperience]})");

		// Organisation is not what it started with two days in. COrganisationCheck judges it.

		foreach (CTechnology technology in subUnit.Type.Technologies)
		{
			ClausewitzNode? level = node.Get(technology.Name);
			int savedLevel = level is { IsBlock: true, Children.Count: > 0 }
				? (int)Number(level.Children[0].Value).ToInt()
				: -1;
			int ours = subUnit.TechnologyLevels.GetValueOrDefault(technology.Name);
			// The level the country has in the save, which for a few is one its history does not give.
			int countrys = savedLevels.GetValueOrDefault(technology.Name, country.TechnologyLevels.GetValueOrDefault(technology.Name));
			if (technology.Change)
			{
				compare("brigades: the level of each technology that takes new equipment", savedLevel == ours,
					() => $"{who} {technology.Name}: save {savedLevel}, ours {ours}  (model {subUnit.HistoricalModel}, the country has {countrys})");
			}
			else
			{
				// Two days in, a doctrine the country knows better has reached the brigade.
				compare("brigades: the level of each doctrine, or its country's where that is higher",
					savedLevel == Math.Max(ours, countrys),
					() => $"{who} {technology.Name}: save {savedLevel}, ours {ours}  (model {subUnit.HistoricalModel}, the country has {countrys})");
			}
		}
		int listed = node.Children.Count(child => child.Key is not null && child.IsBlock && state.Technologies.ContainsKey(child.Key));
		compare("brigades: how many technologies each lists", listed == subUnit.Type.Technologies.Count,
			() => $"{who}: save {listed}, ours {subUnit.Type.Technologies.Count}");
	}

	private static void Collect(ClausewitzNode node, string tag, int parent,
		Dictionary<int, SavedUnit> units, Dictionary<int, SavedSubUnit> subUnits)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || !child.IsBlock)
			{
				continue;
			}
			if (UnitKinds.TryGetValue(child.Key, out EUnitKind kind))
			{
				int id = Id(child);
				units.TryAdd(id, new SavedUnit(child, kind, tag, parent));
				Collect(child, tag, id, units, subUnits);
			}
			else if (SubUnitKeywords.Contains(child.Key) && parent != 0)
			{
				subUnits.TryAdd(Id(child), new SavedSubUnit(child, tag, parent));
				// A carrier's air group is written inside the ship.
				Collect(child, tag, 0, units, subUnits);
			}
		}
	}

	private static int Id(ClausewitzNode node)
	{
		return node.Get("id") is { IsBlock: true } id ? id.GetInt("id") : 0;
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
