using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

/// <summary>
/// A look, not a check: makes a new game with the install's Lua and nobody playing, and runs it hour by hour with every
/// minister at work, telling what the foreign ministers do:
///
///   OpenHOI3.SaveCheck foreignai &lt;install folder&gt; [days]
///
/// 180 days with none given. Every offer posted other than trade, by kind and value, then whether it was valid when it
/// ran and how each proposal was answered; the decisions taken and the wars declared; and what of the Lua failed or read
/// what is not built. No save keeps what the original's AI offered when. See
/// <c>docs/mechanics/foreignminister.md</c>.
/// </summary>
internal static class CForeignAiTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck foreignai <install folder> [days]");
			return 2;
		}
		int days = args.Length > 2 && int.TryParse(args[2], out int given) ? given : 180;

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var reports = new List<string>();
		using var scripts = new CAiScripts(directories, reports.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts);
		state.Played.Clear();
		Console.WriteLine($"a new game, {state.Date}, nobody playing");

		var made = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var answered = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var lines = new List<string>();
		var clock = Stopwatch.StartNew();
		for (int hour = 0; hour < days * CGameState.HoursPerDay; hour++)
		{
			CTick.RunHour(state);
			var posted = new List<CCommand>(state.Outbox);
			state.Outbox.Clear();
			foreach (CCommand command in posted)
			{
				bool may = command.MayExecute(state);
				switch (command)
				{
					case COfferCommand { Offer.Kind: not EOfferKind.Trade } offer:
					{
						COffer o = offer.Offer;
						bool valid = COfferRules.IsValid(state, o);
						string key = $"{o.Kind} {(o.Value ? "yes" : "no")}";
						if (o.Stage == EOfferStage.Proposed)
						{
							made[key] = made.GetValueOrDefault(key) + 1;
							if (lines.Count < 60)
							{
								lines.Add($"{state.Date} {state.Hour}h {o.Actor} -> {o.Recipient}: {key}" +
									(o.Versus is { } versus ? $" against {versus}" : "") + (valid ? "" : " (not valid)"));
							}
						}
						else
						{
							string answer = $"{key} {o.Stage}";
							answered[answer] = answered.GetValueOrDefault(answer) + 1;
						}
						break;
					}
					case CExecuteDecisionCommand decision:
						lines.Add($"{state.Date} {state.Hour}h {decision.Scope.Country} takes {decision.Decision}");
						break;
				}
				if (may)
				{
					command.Execute(state);
				}
			}
		}
		Console.WriteLine($"{days} days in {clock.ElapsedMilliseconds} ms");
		Console.WriteLine("offers made, by kind and value:");
		foreach ((string key, int count) in made)
		{
			Console.WriteLine($"   {count,6} {key}");
		}
		Console.WriteLine("answers given:");
		foreach ((string key, int count) in answered)
		{
			Console.WriteLine($"   {count,6} {key}");
		}
		Console.WriteLine("the first of them, and the decisions:");
		foreach (string line in lines)
		{
			Console.WriteLine("   " + line);
		}
		Console.WriteLine("at the end: " + string.Join(", ",
			$"{state.Naps.Count} NAPs", $"{state.Alliances.Count} alliances", $"{state.Guarantees.Count} guarantees",
			$"{state.Influences.Count} influences", $"{state.Aligns.Count} alignments", $"{state.Embargoes.Count} embargoes",
			$"{state.Wars.Count} wars"));
		Console.WriteLine($"Lua errors, {reports.Count(r => r.StartsWith("LUA Error", StringComparison.Ordinal))}, and what is not built:");
		foreach (var group in reports.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(15))
		{
			Console.WriteLine($"   {group.Count()} x {group.Key}");
		}
		return 0;
	}
}
