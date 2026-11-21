using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

/// <summary>
/// Makes a new game as a player gets it, its two days run, and sets each country's government beside a savegame the
/// original wrote on the start date:
///
///   OpenHOI3.SaveCheck politics &lt;install folder&gt; &lt;start-date savegame&gt; [examples] [laws=save]
///
/// The ruling party, the government, who holds each post, the last election, national unity and dissent, each party's
/// organisation and popularity and the cabinet. Popularity has a draw in it on a generator whose seeding is ours, so it
/// is set against the save to within the draw's reach, five points. <c>laws=save</c> gives every country the save's laws
/// before the two days, the laws the original's AI chose as the game was entered. See <c>docs/mechanics/politics.md</c>.
/// </summary>
internal static class CPoliticsCheck
{
	private sealed class Tally
	{
		public int Same { get; set; }

		public int Compared { get; set; }

		public List<string> Examples { get; } = new();

		public void Add(bool same, string example, int keep)
		{
			Compared++;
			if (same)
			{
				Same++;
			}
			else if (Examples.Count < keep)
			{
				Examples.Add(example);
			}
		}
	}

	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck politics <install folder> <start-date savegame> [examples] [laws=save]");
			return 2;
		}
		int examples = args.Length > 3 && int.TryParse(args[3], out int wanted) ? wanted : 5;
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		using var scripts = new CAiScripts(directories, warnings.Add);
		bool saveLaws = args.Skip(3).Contains("laws=save");
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		// "seed=N": the game's seed, and with it the save's player played - what the AI's ministers draw their first
		// hours from, the original's being its seconds since launch and its played country drawing nothing
		// (ministers.md, section 1).
		string? seedText = args.Skip(3).FirstOrDefault(a => a.StartsWith("seed=", StringComparison.Ordinal))?["seed=".Length..];
		uint? seed = seedText is null ? null : uint.Parse(seedText);
		CCountryTag[]? played = seed is not null && save.GetString("player") is { Length: 3 } player
			? new[] { new CCountryTag(player) } : null;
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add,
			fill: !saveLaws, firstDays: !saveLaws, aiScripts: scripts, played: played, date: CStartSave.Date(args[2]), seed: seed);
		if (seed is not null)
		{
			// The first frame's AI pass, and what it posted run, as the first screen of a game has it.
			_ = new CSimulation(state);
			Console.WriteLine($"seed {seed}, played {(played is null ? "nobody" : played[0].Value)}; the tech shares moved off a quarter each: "
				+ string.Join(" ", state.Countries.Where(c => c.LeadershipShares.Any(s => s.Raw != c.LeadershipShares[0].Raw)).Select(c => c.Tag.Value + " " + string.Join(" ", c.LeadershipShares.Select(s => s.Raw)))));
		}
		if (saveLaws)
		{
			Dictionary<string, Dictionary<string, CLaw>> lawBook = CModifierReader.ReadLaws(directories, warnings.Add);
			foreach (ClausewitzNode block in save.Children)
			{
				if (block.IsBlock && Tag(block.Key) is { } tag && state.Country(tag) is { } country)
				{
					foreach ((string group, Dictionary<string, CLaw> book) in lawBook)
					{
						if (book.TryGetValue(block.GetString(group), out CLaw? law))
						{
							country.Laws[group] = law;
						}
					}
				}
			}
			CTick.RunFirstDays(state);
		}
		Console.WriteLine($"a new game, its two days run: {state.Date}");
		foreach (string warning in warnings.Where(w => w.Contains("party", StringComparison.OrdinalIgnoreCase)
			|| w.Contains("government", StringComparison.OrdinalIgnoreCase) || w.Contains("politics", StringComparison.OrdinalIgnoreCase)
			|| w.StartsWith("LUA", StringComparison.Ordinal)))
		{
			Console.WriteLine("warning: " + warning);
		}

		var tallies = new SortedDictionary<string, Tally>(StringComparer.Ordinal);
		Tally Of(string name) => tallies.TryGetValue(name, out Tally? tally) ? tally : tallies[name] = new Tally();
		foreach (ClausewitzNode block in save.Children)
		{
			if (!block.IsBlock || Tag(block.Key) is not { } tag || state.Country(tag) is not { } country
				|| block.Get("organization") is null)
			{
				continue;
			}
			string ours = country.RulingIdeology;
			string theirs = block.GetString("ideology");
			// The save writes no party as noIdeology.
			Of("ruling party").Add(ours == theirs || ours.Length == 0 && theirs == "noIdeology", $"{tag}: ours {ours}, theirs {theirs}", examples);
			Of("government").Add(country.GovernmentType == block.GetString("government"),
				$"{tag}: ours {country.GovernmentType}, theirs {block.GetString("government")}", examples);
			foreach (CGovernmentPosition position in state.GovernmentPositions)
			{
				int held = country.Government.TryGetValue(position.Name, out CMinister? minister) ? minister.Id : 0;
				int saved = block.GetInt(position.Name);
				Of("posts").Add(held == saved, $"{tag} {position.Name}: ours {held}, theirs {saved}", examples);
			}
			string lastElection = block.GetString("last_election");
			string ourElection = $"{country.LastElection.Year}.{country.LastElection.Month}.{country.LastElection.Day}.0";
			Of("last election").Add(lastElection == ourElection, $"{tag}: ours {ourElection}, theirs {lastElection}", examples);
			Of("national unity").Add(country.NationalUnity == Number(block.GetString("national_unity")),
				$"{tag}: ours {country.NationalUnity}, theirs {block.GetString("national_unity")}", examples);
			Of("dissent").Add(country.Dissent == Number(block.GetString("dissent")),
				$"{tag}: ours {country.Dissent}, theirs {block.GetString("dissent")}", examples);
			foreach (CLawGroup group in state.LawGroups)
			{
				string ourLaw = country.Laws.TryGetValue(group.Name, out CLaw? law) ? law.Name : "";
				string theirLaw = block.GetString(group.Name);
				Of("laws").Add(ourLaw == theirLaw, $"{tag} {group.Name}: ours {ourLaw}, theirs {theirLaw}", examples);
			}

			List<long> organisation = Data(block.Get("organization"));
			List<long> popularity = Data(block.Get("popularity"));
			List<long> cabinet = Whole(block.Get("cabinet"));
			for (int i = 1; i < state.Ideologies.Count; i++)
			{
				string party = $"{tag} {state.Ideologies[i].Name}";
				if (i < organisation.Count)
				{
					Of("organisation").Add(country.Organisation[i] == organisation[i],
						$"{party}: ours {country.Organisation[i]}, theirs {organisation[i]}", examples);
				}
				if (i < popularity.Count)
				{
					Of("popularity, within five").Add(Math.Abs(country.Popularity[i] - popularity[i]) <= 5000,
						$"{party}: ours {country.Popularity[i]}, theirs {popularity[i]}", examples);
				}
				if (i < cabinet.Count)
				{
					Of("cabinet").Add(CCabinet.Seats(country, i) == cabinet[i],
						$"{party}: ours {CCabinet.Seats(country, i)}, theirs {cabinet[i]}", examples);
				}
			}
		}

		bool all = true;
		foreach ((string name, Tally tally) in tallies)
		{
			Console.WriteLine($"{name}: {tally.Same} of {tally.Compared} agree");
			foreach (string example in tally.Examples)
			{
				Console.WriteLine("   " + example);
			}
			all &= tally.Same == tally.Compared;
		}
		return all ? 0 : 1;
	}

	/// <summary>A party block's <c>data</c>, in thousandths.</summary>
	private static List<long> Data(ClausewitzNode? block)
	{
		var values = new List<long>();
		foreach (ClausewitzNode child in block?.Get("data")?.Children ?? new List<ClausewitzNode>())
		{
			if (child.Value is not null)
			{
				values.Add(Number(child.Value).Raw);
			}
		}
		return values;
	}

	private static List<long> Whole(ClausewitzNode? block)
	{
		var values = new List<long>();
		foreach (ClausewitzNode child in block?.Children ?? new List<ClausewitzNode>())
		{
			if (child.Value is not null && long.TryParse(child.Value, out long value))
			{
				values.Add(value);
			}
		}
		return values;
	}

	private static CCountryTag? Tag(string? key)
	{
		return key is { Length: CCountryTag.Length } && CCountryTag.TryParse(key, out CCountryTag tag) ? tag : null;
	}

	private static CFixedPoint Number(string text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
