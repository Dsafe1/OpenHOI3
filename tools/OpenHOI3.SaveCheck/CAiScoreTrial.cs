using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

/// <summary>
/// A look, not a check: makes a new game with the install's Lua and asks the AI's functions for
/// offers that take nothing but tags, for every pair of countries that own land, and says what they
/// answered, what failed and what of the game the scripts read that is not built:
///
///   OpenHOI3.SaveCheck aiscore &lt;install folder&gt; [function] [tag]
///
/// The actor offers and the recipient's AI answers, the observer being the recipient - which tag a
/// real offer passes as its observer comes with the agreements. Given a tag, that country's answers
/// to each country's offer are listed. No save keeps what the original's AI answered. See
/// <c>docs/mechanics/lua.md</c>, section 6.
/// </summary>
internal static class CAiScoreTrial
{
	/// <summary>The functions the engine calls that are handed nothing but tags, or an offer they do not read.</summary>
	private static readonly string[] Functions =
	{
		"DiploScore_Alliance", "DiploScore_Debt", "DiploScore_InviteToFaction", "DiploScore_DemandMilitaryAccess",
		"DiploScore_NonAgression", "DiploScore_OfferLendLease", "DiploScore_OfferMilitaryAccess",
		"DiploScore_PeaceAction", "DiploScore_SendExpeditionaryForce", "DiploScore_CallAlly",
		"DiploScore_RequestLendLease",
	};

	/// <summary>The score over which the original's foreign minister acts on a decision it holds.</summary>
	private const int Acted = 50;

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck aiscore <install folder> [function] [tag]");
			return 2;
		}
		string[] functions = args.Length > 2 && args[2].StartsWith("DiploScore_", StringComparison.Ordinal)
			? new[] { args[2] }
			: Functions;
		string? tagText = args.Skip(2).FirstOrDefault(a => a.Length == 3);

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var reports = new List<string>();
		using var scripts = new CAiScripts(directories, reports.Add);
		Console.WriteLine($"the install's Lua loaded, {scripts.Failures} errors");
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts);
		var landed = state.Countries.Where(c => CCountryLists.OwnedCount(state, c.Tag) > 0).ToList();
		Console.WriteLine($"a new game: {landed.Count} countries own land, {state.Date}");

		// `second`: a second Lua state made beside the game's - the one a dialog asks an offer's chance of
		// (docs/multiplayer.md, section 12) - asked the same of the same game, and what it answers set beside the first's.
		if (args.Contains("second"))
		{
			var made = Stopwatch.StartNew();
			using var display = new CAiScripts(directories, reports.Add);
			Console.WriteLine($"a second Lua state made in {made.ElapsedMilliseconds} ms");
			foreach (string function in functions)
			{
				int asked = 0;
				int other = 0;
				int posted = 0;
				foreach (CCountry actor in landed)
				{
					foreach (CCountry recipient in landed)
					{
						if (actor == recipient)
						{
							continue;
						}
						int outbox = state.Outbox.Count;
						int first = scripts.DiploScore(state, function, actor, recipient, recipient);
						int second = display.DiploScore(state, function, actor, recipient, recipient);
						asked++;
						other += first != second ? 1 : 0;
						posted += state.Outbox.Count - outbox;
					}
				}
				Console.WriteLine($"{function}: {asked} asked of both, {other} answered otherwise by the second, {posted} commands posted");
			}
			return 0;
		}

		foreach (string function in functions)
		{
			int before = scripts.Failures;
			var scores = new List<int>();
			var clock = Stopwatch.StartNew();
			foreach (CCountry actor in landed)
			{
				foreach (CCountry recipient in landed)
				{
					if (actor != recipient)
					{
						scores.Add(scripts.DiploScore(state, function, actor, recipient, recipient));
					}
				}
			}
			int failed = scripts.Failures - before;
			Console.WriteLine($"{function}: {scores.Count} asked in {clock.ElapsedMilliseconds} ms, {failed} failed; " +
				$"from {scores.Min()} to {scores.Max()}, mean {scores.Average():F1}, {scores.Count(s => s > Acted)} over {Acted}");
		}

		if (tagText is not null && state.Country(new CCountryTag(tagText)) is { } chosen)
		{
			foreach (string function in functions)
			{
				Console.WriteLine($"{chosen.Tag}'s AI answering {function}:");
				var line = new List<string>();
				foreach (CCountry actor in landed)
				{
					if (actor != chosen)
					{
						line.Add($"{actor.Tag} {scripts.DiploScore(state, function, actor, chosen, chosen)}");
					}
				}
				Console.WriteLine("  " + string.Join(", ", line));
			}
		}

		var errors = reports.Where(r => r.StartsWith("LUA Error", StringComparison.Ordinal))
			.GroupBy(r => r).OrderByDescending(g => g.Count()).ToList();
		Console.WriteLine($"Lua errors, {errors.Count} kinds:");
		foreach (var error in errors.Take(10))
		{
			Console.WriteLine($"  {error.Count()} x {error.Key}");
		}
		Console.WriteLine("not built, as the scripts met it:");
		foreach (string unbuilt in scripts.Api.Unbuilt)
		{
			Console.WriteLine("  " + unbuilt);
		}
		return 0;
	}
}
