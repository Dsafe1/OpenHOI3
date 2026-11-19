using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

/// <summary>
/// The orders of one stretch of hours, set beside the original's. Two saves of one game say which units the original gave
/// a way to: those whose way in the later save is not what is left of the earlier's. Every such unit is then asked whether
/// the game here gave it the same way, and every unit the game here sent or stopped that the original left alone is listed.
/// Run through <c>load</c> with a later save, and meant for saves an hour or so apart: the longer the stretch, the more
/// one order lands on the ground another left.
/// </summary>
public static class CAiOrderCheck
{
	private sealed record SavedUnit(string Tag, int Id, string Name, EUnitKind Kind, int Location, IReadOnlyList<int> Path);

	/// <summary>A unit by its country, name and level: the original numbers units afresh as it loads a save.</summary>
	private static string Key(string tag, string name, EUnitKind kind) => $"{tag}|{name.Trim('"')}|{kind}";

	private static Dictionary<string, SavedUnit> Units(ClausewitzNode save)
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
		var units = new Dictionary<string, SavedUnit>();
		void Walk(ClausewitzNode node, string tag, EUnitKind kind)
		{
			int id = ids.Of(node.Get("id"));
			List<int> path = node.Get("path")?.Children.Select(step => (int)CSaveValues.Whole(step.Value)).ToList() ?? new List<int>();
			string name = node.Get("name")?.Value ?? "";
			units[Key(tag, name, kind)] = new SavedUnit(tag, id, name, kind, node.GetInt("location"), path);
			foreach (ClausewitzNode child in node.Children)
			{
				if (child.Key is not { } key || !child.IsBlock)
				{
					continue;
				}
				if (CSaveUnitReader.UnitKinds.TryGetValue(key, out EUnitKind childKind))
				{
					Walk(child, tag, childKind);
				}
				else if (key is "regiment" or "ship" or "wing")
				{
					foreach (ClausewitzNode air in child.All("air").Where(air => air.IsBlock))
					{
						Walk(air, tag, EUnitKind.Air);
					}
				}
			}
		}
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.IsBlock && CCountryTag.TryParse(node.Key, out CCountryTag tag))
			{
				foreach (ClausewitzNode child in node.Children)
				{
					if (child.Key is { } key && child.IsBlock && CSaveUnitReader.UnitKinds.TryGetValue(key, out EUnitKind kind))
					{
						Walk(child, tag.Value, kind);
					}
				}
			}
		}
		return units;
	}

	/// <summary>Whether a way is what is left of the earlier one, the unit having walked some of it or none.</summary>
	private static bool Continues(SavedUnit earlier, int location, IReadOnlyList<int> path)
	{
		if (path.SequenceEqual(earlier.Path))
		{
			return true;
		}
		int walked = location == earlier.Location ? -1 : earlier.Path.ToList().IndexOf(location);
		return walked >= 0 && path.SequenceEqual(earlier.Path.Skip(walked + 1));
	}

	private static string Group(EUnitKind kind) => kind switch
	{
		EUnitKind.Navy => "fleets",
		EUnitKind.Air => "air units",
		_ => "land units",
	};

	/// <summary>Where every unit here stands and is bound, to tell afterwards what one stretch of hours gave it.</summary>
	public static Dictionary<string, (int Location, IReadOnlyList<int> Path)> Snapshot(CGameState state)
	{
		var snapshot = new Dictionary<string, (int, IReadOnlyList<int>)>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				snapshot[Key(country.Tag.Value, unit.Name, unit.Kind)] = (unit.Location, unit.Path.ToList());
			}
		}
		return snapshot;
	}

	/// <summary>
	/// One unit by the save's number for it, there and here: where it stands, its progress, its attack delay and its way.
	/// </summary>
	public static string Follow(CGameState state, ClausewitzNode save, int id)
	{
		ClausewitzNode? Find(ClausewitzNode node)
		{
			foreach (ClausewitzNode child in node.Children)
			{
				if (!child.IsBlock)
				{
					continue;
				}
				if (child.Get("id") is { IsBlock: true } number && number.GetInt("id") == id && child.Get("name") is not null
					&& child.Get("location") is not null)
				{
					return child;
				}
				if (Find(child) is { } found)
				{
					return found;
				}
			}
			return null;
		}
		ClausewitzNode? theirs = Find(save);
		// The game here numbers units afresh, so its unit is found by the save's name.
		string? name = theirs?.Get("name")?.Value.Trim('"');
		CUnit? ours = name is null ? null : state.Countries.SelectMany(country => country.AllUnits()).FirstOrDefault(unit => unit.Name == name);
		string Way(IEnumerable<int> path) => string.Join(" ", path);
		string oursText = ours is null ? "none" : $"{ours.Location} progress {ours.MovementProgress} delay {ours.AttackDelay} " +
			$"way [{Way(ours.Path)}] combats {ours.Combats.Count}";
		string theirsText = theirs is null ? "none" : $"{theirs.GetInt("location")} progress {theirs.Get("movement_progress")?.Value ?? "0"} " +
			$"delay {theirs.Get("attack_delay")?.Value ?? "0"} way [{Way(theirs.Get("path")?.Children.Select(step => (int)CSaveValues.Whole(step.Value)) ?? [])}]";
		return $"follow {id} {ours?.Name}: ours {oursText} | the original {theirsText}";
	}

	/// <param name="oursBefore">
	/// Where the game here stood at the stretch's start, where it has played on from the earlier save and so differs from it.
	/// </param>
	public static void Report(CGameState state, ClausewitzNode earlierSave, ClausewitzNode laterSave, string indent, string? onlyCountry,
		string? allFile = null, Dictionary<string, (int Location, IReadOnlyList<int> Path)>? oursBefore = null)
	{
		using System.IO.StreamWriter? everyWay = allFile is null ? null : new System.IO.StreamWriter(allFile);
		Dictionary<string, SavedUnit> earlier = Units(earlierSave);
		Dictionary<string, SavedUnit> later = Units(laterSave);
		var ours = new Dictionary<string, CUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				ours[Key(country.Tag.Value, unit.Name, unit.Kind)] = unit;
			}
		}

		// Whole, where one country's examples are asked for; shortened otherwise.
		string Way(IReadOnlyList<int> path) => path.Count == 0 ? "stop" : path.Count <= 6 || onlyCountry is not null
			? string.Join(' ', path) : $"{string.Join(' ', path.Take(3))} .. {string.Join(' ', path.TakeLast(2))} ({path.Count})";

		foreach (string group in new[] { "land units", "fleets", "air units" })
		{
			var given = new List<string>();
			var missed = new List<string>();
			var other = new List<string>();
			var invented = new List<string>();
			var missedBy = new Dictionary<string, int>();
			var inventedBy = new Dictionary<string, int>();
			var byKind = new SortedDictionary<string, (int Given, int Same, int Other, int None, int Invented)>(StringComparer.Ordinal);
			void Tally(EUnitKind kind, Func<(int Given, int Same, int Other, int None, int Invented), (int Given, int Same, int Other, int None, int Invented)> change)
			{
				byKind[kind.ToString()] = change(byKind.GetValueOrDefault(kind.ToString()));
			}
			int givenCount = 0;
			int sameWay = 0;
			int all = 0;
			foreach ((string id, SavedUnit theirs) in later.Where(pair => Group(pair.Value.Kind) == group))
			{
				if (!earlier.TryGetValue(id, out SavedUnit? before) || !ours.TryGetValue(id, out CUnit? unit))
				{
					continue;
				}
				all++;
				bool originalOrdered = !Continues(before, theirs.Location, theirs.Path);
				SavedUnit ourBefore = before;
				if (oursBefore is not null)
				{
					if (!oursBefore.TryGetValue(id, out var was))
					{
						continue;
					}
					ourBefore = before with { Location = was.Location, Path = was.Path };
				}
				bool weOrdered = !Continues(ourBefore, unit.Location, unit.Path);
				if (everyWay is not null && (originalOrdered || weOrdered))
				{
					everyWay.WriteLine($"{group}\t{theirs.Tag}\t{theirs.Kind}\t{before.Location}\t{theirs.Name.Trim((char)34)}\t{(originalOrdered ? 1 : 0)}\t{(weOrdered ? 1 : 0)}\t{string.Join(' ', theirs.Path)}\t{string.Join(' ', unit.Path)}");
				}
				string who = $"{theirs.Tag} {theirs.Id} {theirs.Name.Trim('"')} at {before.Location}";
				if (originalOrdered)
				{
					givenCount++;
					Tally(theirs.Kind, t => t with { Given = t.Given + 1 });
					if (unit.Path.SequenceEqual(theirs.Path))
					{
						sameWay++;
						Tally(theirs.Kind, t => t with { Same = t.Same + 1 });
						given.Add(who);
					}
					else if (weOrdered)
					{
						missedBy[theirs.Tag] = missedBy.GetValueOrDefault(theirs.Tag) + 1;
						Tally(theirs.Kind, t => t with { Other = t.Other + 1 });
						other.Add($"{who}: the original {Way(theirs.Path)}, ours {Way(unit.Path)}, was {Way(before.Path)}");
					}
					else
					{
						missedBy[theirs.Tag] = missedBy.GetValueOrDefault(theirs.Tag) + 1;
						Tally(theirs.Kind, t => t with { None = t.None + 1 });
						missed.Add($"{who}: the original {Way(theirs.Path)}, ours left it on {Way(unit.Path)}");
					}
				}
				else if (weOrdered)
				{
					inventedBy[theirs.Tag] = inventedBy.GetValueOrDefault(theirs.Tag) + 1;
					Tally(theirs.Kind, t => t with { Invented = t.Invented + 1 });
					invented.Add($"{who}: the original left it on {Way(theirs.Path)}, ours {Way(unit.Path)}");
				}
			}
			Console.WriteLine($"{indent}{group}: {all} - the original gave {givenCount} ways, ours gave {sameWay} of them the same, " +
				$"{other.Count} another way, {missed.Count} none; ours gave {invented.Count} the original did not");
			foreach ((string kind, (int g, int s, int o, int n, int i)) in byKind)
			{
				Console.WriteLine($"{indent}  {kind}: the original {g}, the same {s}, another way {o}, none {n}; ours alone {i}");
			}
			Examples("another way", other);
			Examples("none", missed);
			Examples("not the original's", invented);
			if (missedBy.Count > 0)
			{
				Console.WriteLine($"{indent}  missed by country: " + string.Join(' ', missedBy.OrderByDescending(p => p.Value).Take(12).Select(p => $"{p.Key} {p.Value}")));
			}
			if (inventedBy.Count > 0)
			{
				Console.WriteLine($"{indent}  invented by country: " + string.Join(' ', inventedBy.OrderByDescending(p => p.Value).Take(12).Select(p => $"{p.Key} {p.Value}")));
			}

			void Examples(string what, List<string> list)
			{
				IEnumerable<string> shown = onlyCountry is null ? list : list.Where(line => line.StartsWith(onlyCountry, StringComparison.Ordinal));
				foreach (string line in shown.Take(onlyCountry is null ? 6 : 24))
				{
					Console.WriteLine($"{indent}    {what}: {line}");
				}
			}
		}
	}
}
