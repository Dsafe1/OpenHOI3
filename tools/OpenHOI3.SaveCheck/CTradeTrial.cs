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
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;

/// <summary>
/// A trial, not a check: makes a new game with the install's Lua, nobody playing, and runs it hour by
/// hour so the AI trades as the original's does - <c>ProposeTrades</c> and <c>EvalutateExistingTrades</c>
/// each hour, the offers answered - and says what came of it:
///
///   OpenHOI3.SaveCheck trade &lt;install folder&gt; [days] [tag]
///
/// Thirty days with none given. It tells how many offers were made, accepted, declined and cancelled,
/// the routes standing at the end and how many could not run, and what of the Lua failed; given a tag,
/// that country's routes. No save keeps what the original's AI proposed. See
/// <c>docs/mechanics/trade.md</c>, section 9.
/// </summary>
internal static class CTradeTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck trade <install folder> [days] [tag]");
			return 2;
		}
		int days = args.Length > 2 && int.TryParse(args[2], out int given) ? given : 30;
		string? tag = args.Skip(2).FirstOrDefault(a => a.Length == 3 && !int.TryParse(a, out _));

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var reports = new List<string>();
		using var scripts = new CAiScripts(directories, reports.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts);
		state.Played.Clear();
		if (args.Contains("quiet"))
		{
			// For timing: the hour without the scripts' trading.
			state.AiScripts = new CQuietTrade(scripts);
		}
		var timed = new CTimedTrade(state.AiScripts!);
		if (args.Contains("timed"))
		{
			state.AiScripts = timed;
			scripts.Lua.Profile = new Dictionary<string, (long Ticks, int Calls)>();
		}
		Console.WriteLine($"a new game, {state.Date}, nobody playing; the Lua loaded with {scripts.Failures} errors");

		int proposed = 0, accepted = 0, declined = 0, cancelled = 0, messages = 0;
		state.PactMessaged += pact =>
		{
			if (pact.We is "ACTRAWITHUS" or "DETRAWITHUS" or "WEBRKTRADE")
			{
				messages++;
			}
		};
		var clock = Stopwatch.StartNew();
		for (int hour = 0; hour < days * CGameState.HoursPerDay; hour++)
		{
			CTick.RunHour(state);
			var posted = new List<CCommand>(state.Outbox);
			state.Outbox.Clear();
			CCommand.DropRefused(state, posted);
			foreach (CCommand command in posted)
			{
				if (command is COfferCommand { Offer.Kind: EOfferKind.Trade } offer)
				{
					switch (offer.Offer)
					{
						case { Value: false }:
							cancelled++;
							break;
						case { Stage: EOfferStage.Proposed }:
							proposed++;
							break;
						case { Stage: EOfferStage.Accepted }:
							accepted++;
							break;
						default:
							declined++;
							break;
					}
				}
				// As the simulation runs a command: only where it may still be done, the unit or pact it names there yet.
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
		}
		Console.WriteLine($"{days} days in {clock.ElapsedMilliseconds} ms: {proposed} trades offered, {accepted} accepted, " +
			$"{declined} declined, {cancelled} cancelled ({messages} accepted, declined or broken messages)");
		if (args.Contains("timed"))
		{
			Console.WriteLine($"   the scripts' trading: {timed.Runs} runs in {timed.Trading.ElapsedMilliseconds} ms, of which " +
				$"{timed.Scores} trade scores in {timed.Scoring.ElapsedMilliseconds} ms");
			foreach (var (name, (ticks, calls)) in scripts.Lua.Profile!.OrderByDescending(p => p.Value.Ticks).Take(15))
			{
				Console.WriteLine($"      {name,-48} {calls,9} calls {ticks * 1000 / Stopwatch.Frequency,7} ms");
			}
		}
		Console.WriteLine($"{state.TradeRoutes.Count} routes stand, {state.TradeRoutes.Count(r => r.Inactive)} not running, " +
			$"{state.TradeRoutes.Count(r => r.Convoy is not null)} with a convoy");
		foreach (var group in state.TradeRoutes.GroupBy(r => r.InactiveKey).Where(g => g.Key.Length > 0))
		{
			Console.WriteLine($"   {group.Key}: {group.Count()}");
		}
		foreach (CTradeRoute route in state.TradeRoutes.Where(r => tag is null || r.From.Value == tag || r.To.Value == tag)
			.Take(tag is null ? 12 : 1000))
		{
			Console.WriteLine($"   {route.From} -> {route.To}: gives {Block(route.Terms.From)}, gets {Block(route.Terms.To)}" +
				(route.Inactive ? $"  [{route.InactiveKey} {route.InactiveCountry} {route.InactiveGoods}]" : ""));
		}
		Console.WriteLine($"Lua errors, {reports.Count(r => r.StartsWith("LUA Error", StringComparison.Ordinal))}:");
		foreach (var group in reports.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(12))
		{
			Console.WriteLine($"   {group.Count()} x {group.Key}");
		}
		return 0;
	}

	private static string Block(Func<EGoods, long> amount)
	{
		var parts = new List<string>();
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			long figure = amount((EGoods)i);
			if (figure != 0)
			{
				parts.Add($"{new CFixedPoint(figure)} {(EGoods)i}");
			}
		}
		return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
	}
}
