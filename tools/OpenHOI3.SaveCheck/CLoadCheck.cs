using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// Loads a save of the original's as a game here and says how it went:
///
///   OpenHOI3.SaveCheck load &lt;install folder&gt; &lt;savegame&gt; [hours]
///
/// what of the save is not loaded yet, block by block; the loaded game's figures set beside the save's where the loader
/// reads them, and the ones it works out again - IC - beside nothing; and then the hours given run, to show the game goes
/// on from there. Nothing is written. See <c>docs/mechanics/savegame.md</c>, section 4.
/// </summary>
public static class CLoadCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck load <install folder> <savegame> [hours] [later savegame]");
			return 2;
		}
		int hours = args.Length > 3 && int.TryParse(args[3], out int h) ? h : 24;
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var clock = Stopwatch.StartNew();
		(CGameState state, CSaveReport report) = CSaveLoader.Load(directories, map, localisation, countries, args[2],
			warn: warnings.Add);
		Console.WriteLine($"loaded {System.IO.Path.GetFileName(args[2])} in {clock.ElapsedMilliseconds} ms: " +
			$"{state.Date} {state.Hour}:00, {report.Player} played, seed {state.Seed}");
		Console.WriteLine("not loaded yet:");
		foreach (string line in report.Lines())
		{
			Console.WriteLine("  " + line);
		}

		// What was read, set beside the save it was read from: a miss here is a fault of the reading.
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		int owners = 0, ownersSame = 0, laws = 0, lawsSame = 0, levels = 0, levelsSame = 0;
		foreach (ClausewitzNode node in save.Children)
		{
			if (int.TryParse(node.Key, out int id) && id > 0 && id < state.Provinces.Count)
			{
				owners++;
				CProvince province = state.Province(id);
				string saved = node.Get("owner")?.Value?.Trim('"') ?? "";
				if (saved == (province.Owner?.Value ?? ""))
				{
					ownersSame++;
				}
			}
			else if (CCountryTag.TryParse(node.Key, out CCountryTag tag) && state.Country(tag) is { } country)
			{
				foreach (CLawGroup group in state.LawGroups)
				{
					laws++;
					if ((node.Get(group.Name)?.Value ?? "") == (country.Laws.GetValueOrDefault(group.Name)?.Name ?? ""))
					{
						lawsSame++;
					}
				}
				foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
				{
					if (entry.Key is { } key && entry.IsBlock && state.Technologies.ContainsKey(key))
					{
						levels++;
						if (country.TechnologyLevels.GetValueOrDefault(key) == (int)CSaveValues.Whole(entry.Children[0].Value))
						{
							levelsSame++;
						}
					}
				}
			}
		}
		Console.WriteLine($"owners {ownersSame} of {owners}, laws {lawsSame} of {laws}, technology levels {levelsSame} of {levels}");
		foreach (CCountry country in state.Countries.Where(c => c.Tag.Value is "GER" or "ENG" or "SOV" or "USA").ToList())
		{
			Console.WriteLine($"  {country.Tag}: IC {country.TotalIc} of base {country.BaseIc}, manpower {country.Manpower}, " +
				$"officers {country.Officers}, unity {country.NationalUnity}, faction '{country.Faction}'");
		}

		Console.WriteLine("the units, beside the save they were read from:");
		CompareUnits(state, save, "  ", exact: true);

		// Given a later save of the same game, the hours to it are run and the units set beside it.
		ClausewitzNode? later = null;
		if (args.Length > 4)
		{
			later = ClausewitzParser.ParseFile(args[4]);
			if (CSaveValues.TryHours(later.Get("date")?.Value, out long then))
			{
				hours = (int)(then - state.TotalHours);
			}
		}
		clock.Restart();
		for (int hour = 0; hour < hours; hour++)
		{
			CTick.RunHour(state);
		}
		Console.WriteLine($"ran {hours} hours in {clock.ElapsedMilliseconds} ms, to {state.Date} {state.Hour}:00");
		if (later is not null)
		{
			Console.WriteLine($"the units, beside {System.IO.Path.GetFileName(args[4])} - where the AI and the player " +
				"gave other orders they differ, so a look:");
			CompareUnits(state, later, "  ", exact: false);
		}
		Console.WriteLine($"{warnings.Count} warnings; the first few:");
		foreach (string warning in warnings.Take(8))
		{
			Console.WriteLine("  " + warning);
		}
		return 0;
	}

	/// <summary>
	/// Every unit, brigade, leader in service and line of the queue the save has, found in the game by its number and set
	/// beside it. With <paramref name="exact"/> a miss is a fault of the reading, and the first few are shown.
	/// </summary>
	private static void CompareUnits(CGameState state, ClausewitzNode save, string indent, bool exact)
	{
		var ids = new CSaveIds();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key == "unit")
			{
				ids.SetHistoryNext((int)CSaveValues.Whole(node.Value));
			}
			else if (node.Key == "id" && node.IsBlock)
			{
				ids.SetHighest(node.GetInt("id"), node.GetInt("type"));
			}
		}
		var ours = new Dictionary<int, CUnit>();
		var brigades = new Dictionary<int, CSubUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits().Concat(country.Deployments))
			{
				ours[unit.Id] = unit;
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					brigades[subUnit.Id] = subUnit;
				}
			}
		}
		var tally = new SortedDictionary<string, (int Same, int All)>(StringComparer.Ordinal);
		var examples = new Dictionary<string, List<string>>();
		var byCountry = new Dictionary<string, Dictionary<string, int>>();
		void Count(string what, bool same, Func<string> detail)
		{
			(int s, int a) = tally.GetValueOrDefault(what);
			tally[what] = (s + (same ? 1 : 0), a + 1);
			if (!same)
			{
				// Each detail starts with the country's tag, which the misses are counted by.
				string tag = detail()[..3];
				Dictionary<string, int> countries = byCountry.TryGetValue(what, out Dictionary<string, int>? had) ? had
					: byCountry[what] = new Dictionary<string, int>();
				countries[tag] = countries.GetValueOrDefault(tag) + 1;
				List<string> list = examples.TryGetValue(what, out List<string>? found) ? found : examples[what] = new List<string>();
				if (list.Count < 3)
				{
					list.Add(detail());
				}
			}
		}

		int seen = 0;
		var inSave = new HashSet<int>();
		void Walk(ClausewitzNode node, string tag, bool deployed)
		{
			seen++;
			int id = ids.Of(node.Get("id"));
			string name = node.Get("name")?.Value ?? "";
			string who = $"{tag} {id} {name}";
			bool found = ours.TryGetValue(id, out CUnit? unit);
			inSave.Add(id);
			Count("a unit is found by its number", found, () => who);
			if (unit is not null)
			{
				if (!deployed)
				{
					int location = node.GetInt("location");
					Count("its location", unit.Location == location, () => $"{who}: ours {unit.Location}, save {location}");
				}
				List<int> path = node.Get("path")?.Children.Select(step => (int)CSaveValues.Whole(step.Value)).ToList() ?? new List<int>();
				Count("its path", unit.Path.SequenceEqual(path), () => $"{who}: ours [{string.Join(' ', unit.Path)}], save [{string.Join(' ', path)}]");
				CFixedPoint progress = CSaveValues.Fixed(node, "movement_progress");
				Count("its movement progress", unit.MovementProgress == progress, () => $"{who}: ours {unit.MovementProgress}, save {progress}");
				int leader = node.Get("leader")?.GetInt("id") ?? 0;
				Count("its leader", (unit.Leader?.Id ?? 0) == leader, () => $"{who}: ours {unit.Leader?.Id ?? 0}, save {leader}");
				string order = node.Children.FirstOrDefault(child => child.Key is { } key && child.IsBlock
					&& CSaveOrders.Kinds.ContainsKey(key))?.Key ?? "";
				string ourOrder = CSaveOrders.Kinds.FirstOrDefault(pair => pair.Value == unit.Order).Key ?? "";
				Count("its order", order == ourOrder || order.Length == 0 && unit.Order == EUnitOrder.None,
					() => $"{who}: ours {unit.Order}, save '{order}'");
			}
			foreach (ClausewitzNode child in node.Children)
			{
				if (child.Key is not { } key || !child.IsBlock)
				{
					continue;
				}
				if (CSaveUnitReader.UnitKinds.ContainsKey(key))
				{
					Walk(child, tag, deployed);
				}
				else if (key is "regiment" or "ship" or "wing")
				{
					int brigadeId = ids.Of(child.Get("id"));
					string brigade = $"{who} / {brigadeId} {child.Get("name")?.Value}";
					bool has = brigades.TryGetValue(brigadeId, out CSubUnit? ourBrigade);
					Count("a brigade is found by its number", has, () => brigade);
					if (ourBrigade is not null)
					{
						Count("a brigade is under its unit", ourBrigade.Unit == unit, () => brigade);
						Same("a brigade's strength", ourBrigade.Strength, "strength");
						Same("a brigade's organisation", ourBrigade.Organisation, "organisation");
						Same("a brigade's experience", ourBrigade.Experience, "experience");
					}
					foreach (ClausewitzNode air in child.All("air").Where(air => air.IsBlock))
					{
						Walk(air, tag, deployed);
					}

					void Same(string what, CFixedPoint value, string key)
					{
						CFixedPoint saved = CSaveValues.Fixed(child, key);
						Count(what, value == saved, () => $"{brigade}: ours {value}, save {saved}");
					}
				}
			}
		}

		foreach (ClausewitzNode node in save.Children)
		{
			if (!CCountryTag.TryParse(node.Key, out CCountryTag tag) || state.Country(tag) is not { } country || !node.IsBlock)
			{
				continue;
			}
			var lines = new List<ClausewitzNode>();
			foreach (ClausewitzNode child in node.Children)
			{
				if (child.Key is not { } key || !child.IsBlock)
				{
					continue;
				}
				if (CSaveUnitReader.UnitKinds.ContainsKey(key))
				{
					Walk(child, tag.Value, deployed: false);
				}
				else if (key == "unit_deployment")
				{
					foreach (ClausewitzNode unit in child.Children.Where(u => u.Key is { } k && CSaveUnitReader.UnitKinds.ContainsKey(k)))
					{
						Walk(unit, tag.Value, deployed: true);
					}
				}
				else if (key is "military_construction" or "building_construction" or "convoy_construction")
				{
					lines.Add(child);
				}
				else if (key == "active_leaders")
				{
					List<int> saved = child.Children.Where(entry => entry.IsBlock && int.TryParse(entry.Key, out _))
						.Select(entry => int.Parse(entry.Key!, System.Globalization.CultureInfo.InvariantCulture)).ToList();
					Count("a country's leaders in service, in order", country.ActiveLeaders.Select(l => l.Id).SequenceEqual(saved),
						() => $"{tag}: ours {country.ActiveLeaders.Count}, save {saved.Count}");
					foreach (ClausewitzNode entry in child.Children.Where(entry => entry.IsBlock))
					{
						if (int.TryParse(entry.Key, out int leaderId) && state.Leaders.TryGetValue(leaderId, out OpenHOI3.Sim.Leaders.CLeader? leader))
						{
							CFixed15 experience = CSaveValues.Fixed15(entry.Get("current_experience")?.Value);
							Count("a leader's experience", leader.Experience == experience,
								() => $"{tag} {leaderId} {leader.Name}: ours {leader.Experience}, save {experience}");
							int rank = entry.GetInt("rank");
							Count("a leader's rank", leader.Rank == rank, () => $"{tag} {leaderId}: ours {leader.Rank}, save {rank}");
						}
					}
				}
			}
			Count("a country's lines of the queue", country.Constructions.Count == lines.Count,
				() => $"{tag}: ours {country.Constructions.Count}, save {lines.Count}");
			for (int i = 0; i < Math.Min(lines.Count, country.Constructions.Count); i++)
			{
				OpenHOI3.Sim.Construction.CConstruction ourLine = country.Constructions[i];
				CFixedPoint progress = CSaveValues.Fixed(lines[i], "progress");
				Count("a line's progress", ourLine.Progress == progress,
					() => $"{tag} line {i} {ourLine}: ours {ourLine.Progress}, save {progress}");
				int id = ids.Of(lines[i].Get("id"));
				Count("a line's number", ourLine.Id == id, () => $"{tag} line {i}: ours {ourLine.Id}, save {id}");
			}
		}
		int ourCount = state.Countries.Sum(country => country.AllUnits().Count() + country.Deployments.Count);
		Console.WriteLine($"{indent}{seen} unit blocks in the save, {ourCount} units in the game");
		foreach (CUnit extra in ours.Values.Where(unit => !inSave.Contains(unit.Id)).Take(5))
		{
			Console.WriteLine($"{indent}  ours and not the save's: {extra.Owner} {extra.Id} {extra.Name} ({extra.Kind}, " +
				$"{extra.SubUnits.Count} brigades, in {extra.Location}, parent {extra.Parent?.Name ?? "none"})");
		}
		foreach ((string what, (int same, int all)) in tally)
		{
			Console.WriteLine($"{indent}{what}: {same} of {all}");
			if (byCountry.TryGetValue(what, out Dictionary<string, int>? countries) && countries.Count > 1)
			{
				Console.WriteLine($"{indent}    by country: " + string.Join(" ", countries.OrderByDescending(pair => pair.Value)
					.Take(8).Select(pair => $"{pair.Key} {pair.Value}")));
			}
			foreach (string example in examples.GetValueOrDefault(what) ?? new List<string>())
			{
				Console.WriteLine($"{indent}    {example}");
			}
		}
	}
}
