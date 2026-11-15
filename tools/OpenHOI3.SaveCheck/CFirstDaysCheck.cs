using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

/// <summary>
/// Sets a new game as this project makes it - the two days run that the original runs
/// before a game's first hour - beside a savegame the original wrote on the start date
/// with nothing touched:
///
///   OpenHOI3.SaveCheck firstdays &lt;install folder&gt; &lt;start-date savegame&gt; [examples]
///
/// Nothing is given to the game from the save: not its laws, not its shares. So this is
/// what a player starts from here against what he starts from there, and where the two
/// differ for a country nobody plays it is mostly the original's AI, which has already
/// changed laws and moved shares by the first screen. The country the save's player has
/// is told apart, the AI having less of a hand in it. The like-for-like, with the save's
/// laws given, is <c>supplypass ... countries</c>, which ends in <see cref="Compare"/> too.
/// See <c>docs/mechanics/tick.md</c>, section 6.
/// </summary>
internal static class CFirstDaysCheck
{
	private static readonly HashSet<string> UnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"theatre", "armygroup", "army", "corps", "division", "navy", "air",
	};

	private static readonly HashSet<string> SubUnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"regiment", "ship", "wing",
	};

	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck firstdays <install folder> <start-date savegame> [examples]");
			return 2;
		}
		int examples = args.Length > 3 && int.TryParse(args[3], out int wanted) ? wanted : 3;
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		// As a player gets it: the install's Lua looks at every other country's laws (politics.md, section 12).
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		using var scripts = new OpenHOI3.Lua.CAiScripts(directories, warnings.Add);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { new CCountryTag(save.GetString("player")) }, date: CStartSave.Date(args[2]));
		Console.WriteLine($"a new game, its two days run: made in {clock.ElapsedMilliseconds} ms");

		Console.WriteLine($"savegame: date {save.GetString("date")}, player {save.GetString("player")}");
		Console.WriteLine($"ours:     date {state.Date}.{state.Hour}");
		Console.WriteLine();
		Compare(state, save, examples);
		Console.WriteLine();
		// One country's day of goods beside the save's, for looking into: "country=<tag>" after the save.
		foreach (string argument in args.Skip(3))
		{
			if (argument.StartsWith("country=", StringComparison.Ordinal)
				&& CCountryTag.TryParse(argument["country=".Length..], out CCountryTag asked) && state.Country(asked) is { } country)
			{
				Ledger(state, save, country);
			}
		}
		CStrategicResourceLook.Print(state);
		Console.WriteLine();

		// What a day costs as the game is played, the comparison being done.
		clock.Restart();
		CTick.RunDailyPass(state);
		Console.WriteLine($"a day's pass then takes {clock.ElapsedMilliseconds} ms");
		Console.WriteLine($"{warnings.Count} warnings while loading");
		return 0;
	}

	/// <summary>
	/// Sets a game that has run its two days beside the save, country by country: the
	/// stockpile, manpower, officers and influence, what it knows, its build queue, and
	/// every brigade's strength and organisation.
	/// </summary>
	/// <param name="heldByLaw">
	/// Take a country's brigades to have been held to the running game's ceiling on their
	/// organisation wherever the save shows one below what the two days left it. That is
	/// what a change of law does, and the original's AI changes laws once the days are run
	/// (<c>docs/mechanics/organisation.md</c>, section 7).
	/// </param>
	public static void Compare(CGameState state, ClausewitzNode save, int examples, bool heldByLaw = false)
	{
		string player = save.GetString("player");
		var agreed = new SortedDictionary<string, (int Agree, int All)>(StringComparer.Ordinal);
		var faults = new Dictionary<string, List<string>>();
		void Compare(string what, bool isPlayers, bool agrees, Func<string> detail)
		{
			string key = what + (isPlayers ? ", the player's country" : ", the others");
			(int agree, int all) = agreed.GetValueOrDefault(key);
			agreed[key] = (agree + (agrees ? 1 : 0), all + 1);
			if (!agrees)
			{
				if (!faults.TryGetValue(key, out List<string>? list))
				{
					faults[key] = list = new List<string>();
				}
				list.Add(detail());
			}
		}

		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key is null || !node.IsBlock || !CCountryTag.TryParse(node.Key, out CCountryTag tag)
				|| state.Country(tag) is not { } country || !(CResources.HoldsCapital(state, country) || country.InExile))
			{
				continue;
			}
			bool isPlayers = tag.Value == player;
			string who = tag.Value;

			// The stockpile, where it is the country's own and nobody shares its seat: the pool of the province its
			// government sits in, the save's acting capital, or an exile's own pool (stockpile.md, section 1).
			bool shared = !country.InExile && state.Countries.Any(other => other != country && !other.InExile
				&& other.ActingCapital == country.ActingCapital && CResources.HoldsCapital(state, other));
			ClausewitzNode? pool = node.GetBool("government_in_exile")
				? node.Get("pool")
				: save.Get(node.GetString("acting_capital") is { Length: > 0 } seat ? seat
					: country.ActingCapital.ToString(CultureInfo.InvariantCulture))?.Get("pool");
			if (!shared && pool is not null && state.Stockpile(country) is { } stockpile)
			{
				foreach (EGoods goods in Enum.GetValues<EGoods>())
				{
					string key = CGoodsPool.KeyOf(goods);
					CFixedPoint saved = Number(pool.GetString(key));
					Compare("the stockpile: " + key, isPlayers, saved == stockpile[goods],
						() => $"{who}: save {saved}, ours {stockpile[goods]}  (off by {saved - stockpile[goods]})");
				}
			}

			// The occupation policy towards each country, where either side has one: the save writes none for no_occupation
			// (occupation.md, section 6).
			foreach (ClausewitzNode pair in node.Children)
			{
				if (!pair.IsBlock || pair.GetString("value").Length == 0 || pair.Key is null
					|| !CCountryTag.TryParse(pair.Key, out CCountryTag otherTag) || state.Country(otherTag) is not { } other)
				{
					continue;
				}
				string saved = pair.GetString("occupation_policy");
				OpenHOI3.Sim.Occupation.COccupationPolicy ours = OpenHOI3.Sim.Occupation.COccupationPolicies.Of(state, country, other);
				if (saved.Length > 0 || ours.IsReal)
				{
					Compare("occupation policies", isPlayers, saved == (ours.IsReal ? ours.Name : ""),
						() => $"{who}->{other.Tag}: save '{saved}', ours '{ours.Name}'");
				}
			}

			Compare("manpower", isPlayers, Number(node.GetString("manpower")) == country.Manpower,
				() => $"{who}: save {node.GetString("manpower")}, ours {country.Manpower}");
			Compare("officers", isPlayers, Number(node.GetString("officers")) == country.Officers,
				() => $"{who}: save {node.GetString("officers")}, ours {country.Officers}");
			Compare("diplomatic influence", isPlayers, Number(node.GetString("diplo_influence")) == country.DiplomaticInfluence,
				() => $"{who}: save {node.GetString("diplo_influence")}, ours {country.DiplomaticInfluence}");

			foreach (CTechnologyCategory category in state.TechnologyCategories)
			{
				CFixedPoint saved = Number(node.GetString(category.Name));
				CFixedPoint ours = country.Ability(category.Name);
				if (saved != CFixedPoint.Zero || ours != CFixedPoint.Zero)
				{
					Compare("abilities", isPlayers, saved == ours, () => $"{who} {category.Name}: save {saved}, ours {ours}");
				}
			}
			foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.Key is null || !entry.IsBlock || entry.Children.Count == 0 || !state.Technologies.ContainsKey(entry.Key))
				{
					continue;
				}
				int saved = (int)Number(entry.Children[0].Value).ToInt();
				int ours = country.TechnologyLevels.GetValueOrDefault(entry.Key);
				Compare("technology levels", isPlayers, saved == ours, () => $"{who} {entry.Key}: save {saved}, ours {ours}");
			}

			// The build queue: how far each line has got.
			Dictionary<int, ClausewitzNode> lines = CBuildQueueCheck.Collect(node);
			foreach (var construction in country.Constructions)
			{
				if (!lines.TryGetValue(construction.Id, out ClausewitzNode? line))
				{
					continue;
				}
				CFixedPoint saved = Number(line.GetString("progress"));
				Compare("the build queue: how far each line has got", isPlayers, saved == construction.Progress,
					() => $"{who} {construction.Id}: save {saved}, ours {construction.Progress}");
			}

			// Every brigade, ship and wing: what it has left of its strength and its organisation.
			var savedSubUnits = new Dictionary<int, ClausewitzNode>();
			Collect(node, savedSubUnits);
			bool held = heldByLaw && country.AllUnits().Any(unit => unit.SubUnits.Any(subUnit =>
				savedSubUnits.TryGetValue(subUnit.Id, out ClausewitzNode? found)
				&& Number(found.GetString("organisation")) < subUnit.Organisation));
			// Held with the ratio the save's own figures give: its officers over what its
			// brigades call for, those of the headquarters the original's AI added among them.
			CFixedPoint ratio = country.OfficerRatio;
			if (held)
			{
				country.OfficerRatio = COfficers.Ratio(state, Number(node.GetString("officers")), COrganisationCheck.Needed(node, state));
			}
			foreach (CUnit unit in country.AllUnits())
			{
				// A commander adds to what his army's brigades may have, by his reach.
				bool commanded = COrganisation.ArmyAbove(unit) is { } army && HasLeader(node, army.Id);
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					if (!savedSubUnits.TryGetValue(subUnit.Id, out ClausewitzNode? found))
					{
						continue;
					}
					CFixedPoint strength = Number(found.GetString("strength"));
					CFixedPoint organisation = Number(found.GetString("organisation"));
					CFixedPoint ours = held
						? CFixedPoint.Min(subUnit.Organisation, COrganisation.Ceiling(state, country, subUnit))
						: subUnit.Organisation;
					Compare("brigades: strength", isPlayers, strength == subUnit.Strength,
						() => $"{who} {subUnit.Id} '{subUnit.Name}' {subUnit.Type.Key}: save {strength}, ours {subUnit.Strength}");
					Compare("brigades: organisation" + (held && commanded ? ", under an army with a commander" : ""),
						isPlayers, organisation == ours,
						() => $"{who} {subUnit.Id} '{subUnit.Name}' {subUnit.Type.Key}: save {organisation}, ours {ours}" +
							(held ? $"  (as the two days left it {subUnit.Organisation})" : ""));
				}
			}
			country.OfficerRatio = ratio;
		}

		// Every province's nationalism, where either has any: the save writes none at nought (occupation.md, section 6).
		foreach (OpenHOI3.Sim.Provinces.CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			CFixedPoint saved = Number(save.Get(province.Id.ToString(CultureInfo.InvariantCulture))?.GetString("nationalism"));
			if (saved != CFixedPoint.Zero || province.Nationalism != CFixedPoint.Zero)
			{
				Compare("provinces: nationalism", false, saved == province.Nationalism,
					() => $"{province.Id} {province.Name}: save {saved}, ours {province.Nationalism}");
			}
		}

		foreach ((string what, (int agree, int all)) in agreed)
		{
			Console.WriteLine($"{(agree == all ? "agree   " : "DISAGREE")}  {what}: {agree} of {all}");
			if (faults.TryGetValue(what, out List<string>? list))
			{
				foreach (string line in list.Take(examples))
				{
					Console.WriteLine("            " + line);
				}
			}
		}
	}

	/// <summary>Whether the save gives a leader to the unit of this number, somewhere in a country's block.</summary>
	private static bool HasLeader(ClausewitzNode node, int id)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || !child.IsBlock || !UnitKeywords.Contains(child.Key))
			{
				continue;
			}
			if (child.Get("id") is { IsBlock: true } own && own.GetInt("id") == id)
			{
				return child.Get("leader") is not null;
			}
			if (HasLeader(child, id))
			{
				return true;
			}
		}
		return false;
	}

	private static void Collect(ClausewitzNode node, Dictionary<int, ClausewitzNode> subUnits)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || !child.IsBlock)
			{
				continue;
			}
			bool isUnit = UnitKeywords.Contains(child.Key);
			if (!isUnit && !SubUnitKeywords.Contains(child.Key))
			{
				continue;
			}
			if (!isUnit && child.Get("id") is { IsBlock: true } id)
			{
				subUnits.TryAdd(id.GetInt("id"), child);
			}
			Collect(child, subUnits);                         // a carrier's air group is written inside the ship
		}
	}

	/// <summary>
	/// The second day's counts of a country's goods, ours beside the save's, each good the save or ours has a figure of:
	/// what was made at home, what left the stockpile for units, what was made away from the capital and what came back,
	/// what was paid and was paid for friends' units, and the stockpile itself.
	/// </summary>
	private static void Ledger(CGameState state, ClausewitzNode save, CCountry country)
	{
		ClausewitzNode? node = save.Get(country.Tag.Value);
		Console.WriteLine($"{country.Tag}: ours / the save's");
		foreach ((string name, CGoodsPool? ours, string key) in new (string, CGoodsPool?, string)[]
		{
			("made", country.HomeProduced, "home"), ("used by industry", country.Usage, "usage"),
			("left the stockpile for units", country.UnitDrawn, "usage"), ("turned into another", country.ConversionUsed, "-"),
			("made by turning", country.ConversionMade, "-"),
			("made away from the capital", country.SentTo, "to"), ("come back", country.SentBack, "back"),
			("paid for its units", country.TradedAway, "traded_away"), ("paid it for friends'", country.TradedFor, "traded_for"),
			("the stockpile", state.Stockpile(country), "pool"),
		})
		{
			ClausewitzNode? saved = key == "pool" && !country.InExile
				? save.Get(node?.GetString("acting_capital") ?? country.ActingCapital.ToString(CultureInfo.InvariantCulture))?.Get("pool")
				: node?.Get(key);
			var parts = new List<string>();
			foreach (EGoods goods in Enum.GetValues<EGoods>())
			{
				CFixedPoint mine = ours?[goods] ?? CFixedPoint.Zero;
				CFixedPoint theirs = Number(saved?.GetString(CGoodsPool.KeyOf(goods)));
				if (mine != CFixedPoint.Zero || theirs != CFixedPoint.Zero)
				{
					parts.Add($"{CGoodsPool.KeyOf(goods)} {mine} / {theirs}");
				}
			}
			Console.WriteLine($"   {name,-30} {string.Join("   ", parts)}");
		}
		Console.WriteLine();
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
