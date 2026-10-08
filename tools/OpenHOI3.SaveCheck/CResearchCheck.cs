// Checks research against two savegames of one game, an earlier and a later.
//
//   dotnet run --project tools/OpenHOI3.SaveCheck -- research <install folder> <earlier save> <later save> [examples]
//
// Every country is given what the earlier save says it has - laws, government, technology
// levels, abilities - and one day's gain is worked out for each technology it was
// researching. That, times the days between the two saves, is set against how far the
// later save says the technology got. The month's decay of abilities is checked the same way.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

internal static class CResearchCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 4)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck research <install folder> <earlier save> <later save> [examples]");
			return 2;
		}
		int examples = args.Length > 4 && int.TryParse(args[4], out int wanted) ? wanted : 5;

		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);

		ClausewitzNode earlier = ClausewitzParser.ParseFile(args[2]);
		ClausewitzNode later = ClausewitzParser.ParseFile(args[3]);
		if (!TryDate(earlier.GetString("date"), out CDate from) || !TryDate(later.GetString("date"), out CDate to)
			|| to.DayNumber <= from.DayNumber)
		{
			Console.Error.WriteLine($"the saves are dated '{earlier.GetString("date")}' and '{later.GetString("date")}'; the second must be later");
			return 2;
		}
		int days = to.DayNumber - from.DayNumber;
		int months = (to.Year * 12 + to.Month) - (from.Year * 12 + from.Month);
		Console.WriteLine($"earlier: {earlier.GetString("date")}   later: {later.GetString("date")}   {days} days, {months} month ends between");
		if (from.Year != state.Date.Year)
		{
			Console.WriteLine($"note: the year penalty is worked out for {state.Date.Year}, the year a new game starts in");
		}
		Console.WriteLine();

		var findings = new Dictionary<string, List<string>>();
		var compared = new Dictionary<string, int>();
		void Compare(string what, bool agrees, Func<string> detail)
		{
			compared[what] = compared.GetValueOrDefault(what) + 1;
			if (agrees)
			{
				return;
			}
			if (!findings.TryGetValue(what, out List<string>? list))
			{
				findings[what] = list = new List<string>();
			}
			list.Add(detail());
		}

		Dictionary<string, Dictionary<string, CLaw>> lawBook = CModifierReader.ReadLaws(directories, warnings.Add);
		HashSet<string> seats = CModifierReader.ReadGovernmentPositions(directories, warnings.Add);

		// ---- every country as the earlier save has it
		var pairs = new List<(CCountry Country, ClausewitzNode Earlier, ClausewitzNode Later)>();
		foreach (ClausewitzNode node in earlier.Children)
		{
			if (node.Key is null || !node.IsBlock || !CCountryTag.TryParse(node.Key, out CCountryTag tag)
				|| state.Country(tag) is not { } country || later.Get(node.Key) is not { } laterNode)
			{
				continue;
			}
			pairs.Add((country, node, laterNode));

			foreach ((string group, Dictionary<string, CLaw> laws) in lawBook)
			{
				if (laws.TryGetValue(node.GetString(group), out CLaw? law))
				{
					country.Laws[group] = law;
				}
			}
			country.Government.Clear();
			foreach (string seat in seats)
			{
				if (int.TryParse(node.GetString(seat), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
					&& country.Ministers.TryGetValue(id, out CMinister? minister))
				{
					country.Government[seat] = minister;
				}
			}
			foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.Key is not null && entry.IsBlock && entry.Children.Count > 0 && state.Technologies.ContainsKey(entry.Key))
				{
					country.TechnologyLevels[entry.Key] = (int)Number(entry.Children[0].Value).ToInt();
				}
			}
			country.Abilities.Clear();
			foreach (CTechnologyCategory category in state.TechnologyCategories)
			{
				if (node.Get(category.Name) is { Value: { } text })
				{
					country.Abilities[category.Name] = Number(text);
				}
			}
			List<ClausewitzNode> shares = node.Get("distribution")?.Children ?? new List<ClausewitzNode>();
			for (int i = 0; i < CProduction.CategoryCount && shares.Count == CProduction.CategoryCount; i++)
			{
				country.ProductionShares[i] = Share(shares[i].Value);
			}
		}
		// Who is in which faction is the save's to say: it decides who is given whose
		// strategic resources.
		var aligned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CCountry country in state.Countries)
		{
			country.Faction = "";
		}
		foreach (ClausewitzNode faction in earlier.Get("faction")?.Children ?? new List<ClausewitzNode>())
		{
			foreach (ClausewitzNode member in faction.Children)
			{
				if (member.Value is { Length: CCountryTag.Length } tag)
				{
					aligned.Add(tag);
					if (faction.Key is not null && state.Country(new CCountryTag(tag)) is { } country)
					{
						country.Faction = faction.Key;
					}
				}
			}
		}

		// A day begins by working out who has the use of which strategic resource, and that
		// needs each country's industry first.
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);
		CStrategicResources.Update(state);
		CCountryModifiers.Rebuild(state);

		// Triggered modifiers are not applied by this project yet, and one of them touches
		// nearly every country's research: `neutrality`, for a country in none of the factions.
		// It is given here by hand, from the file's own figure and the save's own factions, so
		// that what is compared is the research and not that omission.
		CFixedPoint unaligned = Number(ClausewitzParser.ParseFile(directories.Resolve("common/triggered_modifiers.txt"))
			.Get("neutrality")?.GetString("research_efficiency"));
		int givenByHand = 0;
		foreach ((CCountry country, ClausewitzNode _, ClausewitzNode _) in pairs)
		{
			if (!aligned.Contains(country.Tag.Value))
			{
				country.Modifier.Add("research_efficiency", unaligned);
				givenByHand++;
			}
		}
		Console.WriteLine($"{givenByHand} countries in no faction were given the `neutrality` triggered modifier's {unaligned} research efficiency by hand");
		Console.WriteLine();

		// ---- one day's gain for everything being researched, from the earlier save's own state
		var expected = new List<(CCountry Country, CTechnology Technology, int Level, long From, long Gain, string LaterLevel, string LaterProgress, bool SameLeadership)>();
		int notResearched = 0;
		foreach ((CCountry country, ClausewitzNode before, ClausewitzNode after) in pairs)
		{
			ClausewitzNode? laterTechnology = after.Get("technology");
			foreach (ClausewitzNode entry in before.Get("technology")?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.Key is null || !entry.IsBlock || entry.Children.Count < 3
					|| !state.Technologies.TryGetValue(entry.Key, out CTechnology? technology))
				{
					continue;
				}
				CFixedPoint leadership = Number(entry.Children[2].Value);
				if (leadership <= CFixedPoint.Zero)
				{
					notResearched++;
					continue;
				}
				if (laterTechnology?.Get(entry.Key) is not { IsBlock: true, Children.Count: >= 3 } then)
				{
					continue;
				}
				int level = (int)Number(entry.Children[0].Value).ToInt();
				long gain = CResearch.Gain(state, country, technology, level + 1, leadership).Raw;
				expected.Add((country, technology, level, Share(entry.Children[1].Value).Raw, gain,
					then.Children[0].Value ?? "", then.Children[1].Value ?? "",
					Number(then.Children[2].Value) == leadership));
			}
		}

		// ---- the decay between the two saves, and which abilities something else moved. What
		// slows the decay is the shares of IC on the day it happens, and the later save - an hour
		// after it, where the two are a month apart - is the nearer witness to those.
		foreach ((CCountry country, ClausewitzNode _, ClausewitzNode after) in pairs)
		{
			List<ClausewitzNode> shares = after.Get("distribution")?.Children ?? new List<ClausewitzNode>();
			for (int i = 0; i < CProduction.CategoryCount && shares.Count == CProduction.CategoryCount; i++)
			{
				country.ProductionShares[i] = Share(shares[i].Value);
			}
		}
		for (int month = 0; month < months; month++)
		{
			CResearch.DecayMonthly(state);
		}
		var moved = new HashSet<(string Tag, string Category)>();
		foreach ((CCountry country, ClausewitzNode _, ClausewitzNode after) in pairs)
		{
			foreach (CTechnologyCategory category in state.TechnologyCategories)
			{
				if (after.Get(category.Name) is not { Value: { } text })
				{
					continue;
				}
				CFixedPoint saved = Number(text);
				CFixedPoint ours = country.Ability(category.Name);
				if (saved == CFixedPoint.Zero && ours == CFixedPoint.Zero)
				{
					continue;
				}
				if (saved != ours)
				{
					moved.Add((country.Tag.Value, category.Name));
				}
				// An ability that went up was gained - a unit built, a level finished - and says
				// nothing about decay either way.
				if (saved <= ours)
				{
					Compare(months == 0 ? "abilities unchanged where nothing was gained" : "abilities after the decay, where nothing was gained",
						saved == ours, () => $"{country.Tag} {category.Name}: save {saved}, ours {ours}");
				}
			}
		}

		// ---- the progress
		int finished = 0;
		int leadershipChanged = 0;
		int abilityChanged = 0;
		foreach ((CCountry country, CTechnology technology, int level, long start, long gain, string laterLevel, string laterProgress, bool sameLeadership) in expected)
		{
			long total = start + gain * days;
			if (laterLevel != level.ToString(CultureInfo.InvariantCulture))
			{
				finished++;
				continue;
			}
			if (!sameLeadership)
			{
				leadershipChanged++;
				continue;
			}
			if (technology.ResearchBonusFrom.Any(bonus => moved.Contains((country.Tag.Value, bonus.Category))))
			{
				abilityChanged++;
				continue;
			}
			string ours = new CFixed15(total).ToString();
			Compare($"research progress over {days} days", ours == laterProgress,
				() => $"{country.Tag} {technology.Name} level {level + 1}: save {laterProgress}, ours {ours}  " +
					$"(from {new CFixed15(start)}, {gain} of 32768 a day, cost {CResearch.Cost(country, technology)})");
		}

		// ---- report
		int faults = 0;
		foreach ((string what, int count) in compared.OrderBy(entry => entry.Key))
		{
			int wrong = findings.TryGetValue(what, out List<string>? list) ? list.Count : 0;
			Console.WriteLine($"{(wrong == 0 ? "agree   " : "DISAGREE")}  {what}: {count - wrong} of {count}");
			if (list is not null)
			{
				faults += wrong;
				foreach (string line in list.Take(examples))
				{
					Console.WriteLine("            " + line);
				}
			}
		}
		Console.WriteLine();
		Console.WriteLine($"not compared: {finished} that finished a level in between, {leadershipChanged} whose leadership changed, " +
			$"{abilityChanged} whose cost changed with an ability; {notResearched} technologies had progress and no leadership");
		Console.WriteLine($"{warnings.Count} warnings while loading");
		foreach (string warning in warnings.Take(examples))
		{
			Console.WriteLine("   " + (warning.Length > 200 ? warning[..200] + " ..." : warning));
		}
		return faults == 0 ? 0 : 1;
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	/// <summary>A value written with five decimals; the nearest with fifteen binary places is it.</summary>
	private static CFixed15 Share(string? text)
	{
		return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
			? new CFixed15((long)Math.Round(value * CFixed15.Scale))
			: CFixed15.Zero;
	}

	/// <summary>A save's date, which has the hour after the day.</summary>
	private static bool TryDate(string text, out CDate date)
	{
		string[] parts = text.Split('.');
		return CDate.TryParse(parts.Length >= 3 ? string.Join('.', parts.Take(3)) : text, out date);
	}
}
