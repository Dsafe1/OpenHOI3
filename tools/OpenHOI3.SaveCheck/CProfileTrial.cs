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
/// A look, not a check: makes a new game as the client does, with the install's Lua and one country
/// played, runs it as the top speed would and says where the time went:
///
///   OpenHOI3.SaveCheck profile &lt;install folder&gt; [days] [tag] [from=DATE]
///
/// Thirty days as Germany with none given; from= makes the game at a later bookmark's date. It tells how long the loading took, how long an hour takes
/// on an ordinary hour and on one that turns a day, the slowest hours, and every step of the tick by
/// the time it took in all. The figures are this machine's and this build's.
/// </summary>
internal static class CProfileTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck profile <install folder> [days] [tag] [from=DATE] [warm=DAYS] [lua]");
			return 2;
		}
		int days = args.Length > 2 && int.TryParse(args[2], out int given) ? given : 30;
		string tag = args.Skip(2).FirstOrDefault(a => a.Length == 3 && !int.TryParse(a, out _)) ?? "GER";
		// from=1941.6.22: the new game made at a later bookmark's date (bookmarks.md, section 3).
		CDate? from = args.FirstOrDefault(a => a.StartsWith("from=", StringComparison.Ordinal)) is { } fromArgument
			&& CDate.TryParse(fromArgument["from=".Length..], out CDate fromDate) ? fromDate : null;

		var loading = Stopwatch.StartNew();
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		using var scripts = new CAiScripts(directories);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { new CCountryTag(tag) }, date: from);
		state.Played.Clear();
		state.Played.Add(new CCountryTag(tag));
		var simulation = new CSimulation(state);
		Console.WriteLine($"a new game as {tag}, {state.Date}, loaded in {loading.ElapsedMilliseconds} ms");

		// warm=N: N days played before anything is measured, so that a start's first passes are left out.
		if (args.FirstOrDefault(a => a.StartsWith("warm=", StringComparison.Ordinal)) is { } warmArgument
			&& int.TryParse(warmArgument["warm=".Length..], out int warm) && warm > 0)
		{
			var warming = Stopwatch.StartNew();
			simulation.RunHours(warm * CGameState.HoursPerDay);
			Console.WriteLine($"{warm} days played first, to {state.Date}, in {warming.ElapsedMilliseconds} ms");
		}
		// The world's size as the measuring begins.
		var units = state.Countries.SelectMany(c => c.AllUnits()).Where(u => !u.IsRemoved).ToList();
		Console.WriteLine($"the world: {units.Count(u => u.Kind == OpenHOI3.Sim.Units.EUnitKind.Division)} divisions, " +
			$"{units.Count(u => u.Kind is not (OpenHOI3.Sim.Units.EUnitKind.Division or OpenHOI3.Sim.Units.EUnitKind.Navy or OpenHOI3.Sim.Units.EUnitKind.Air))} headquarters, " +
			$"{units.Count(u => u.Kind == OpenHOI3.Sim.Units.EUnitKind.Navy)} fleets, {units.Count(u => u.Kind == OpenHOI3.Sim.Units.EUnitKind.Air)} air units, " +
			$"{units.Sum(u => u.SubUnits.Count)} brigades, ships and wings; {state.Wars.Count} wars, " +
			$"{state.Countries.Count(c => state.IsAtWar(c.Tag))} countries at war, {state.Combats.Count} battles, " +
			$"{state.Countries.Sum(c => c.Convoys.Count)} convoys, {state.TradeRoutes.Count} trade routes");
		state.Profile = new CTickProfile();
		// With `lua`, the time the scripts spend in each method of ours they call, on the game's own state.
		bool lua = args.Skip(2).Contains("lua");
		if (lua)
		{
			scripts.Lua.Profile = new Dictionary<string, (long Ticks, int Calls)>();
		}
		var plain = new List<double>();
		var turning = new List<double>();
		var slowest = new List<(double Ms, CDate Date)>();
		long ticksPerMs = Stopwatch.Frequency / 1000;
		var all = Stopwatch.StartNew();
		// An hour over a second keeps what each step added in it, the most first, to say where a spike went.
		var spikes = new List<(double Ms, CDate Date, int Hour, string Steps)>();
		for (int hour = 0; hour < days * CGameState.HoursPerDay; hour++)
		{
			CDate before = state.Date;
			int clock = state.Hour;
			var was = state.Profile.Steps.ToDictionary(s => s.Key, s => s.Value.Ticks);
			long start = Stopwatch.GetTimestamp();
			simulation.RunHours(1);
			double ms = (double)(Stopwatch.GetTimestamp() - start) / ticksPerMs;
			(state.Date != before ? turning : plain).Add(ms);
			slowest.Add((ms, state.Date));
			if (ms > 1000)
			{
				var added = state.Profile.Steps.Select(s => (s.Key, Ms: (s.Value.Ticks - was.GetValueOrDefault(s.Key)) / (double)ticksPerMs))
					.Where(s => s.Ms > 50).OrderByDescending(s => s.Ms).Take(6);
				spikes.Add((ms, before, clock, string.Join("; ", added.Select(s => $"{s.Key.Trim()} {s.Ms:0}"))));
			}
		}
		all.Stop();

		Console.WriteLine($"{days} days in {all.ElapsedMilliseconds} ms, {all.ElapsedMilliseconds / Math.Max(days, 1)} ms a day");
		Console.WriteLine($"   an ordinary hour: {Describe(plain)}");
		Console.WriteLine($"   an hour that turns a day: {Describe(turning)}");
		Console.WriteLine("   the slowest hours: " +
			string.Join(", ", slowest.OrderByDescending(s => s.Ms).Take(8).Select(s => $"{s.Date} {s.Ms:0.0} ms")));
		foreach (var spike in spikes.OrderByDescending(s => s.Ms).Take(10))
		{
			Console.WriteLine($"   spike {spike.Date}.{spike.Hour} {spike.Ms:0} ms: {spike.Steps}");
		}
		long total = state.Profile.Steps.Values.Sum(s => s.Ticks);
		Console.WriteLine($"the tick's steps, {total / ticksPerMs} ms in all:");
		foreach (var (name, (ticks, calls)) in state.Profile.Steps.OrderByDescending(s => s.Value.Ticks))
		{
			Console.WriteLine($"   {name,-44} {ticks / ticksPerMs,7} ms {calls,7} runs {(double)ticks / ticksPerMs / calls,9:0.000} ms a run");
		}
		if (scripts.Lua.Profile is { } methods)
		{
			Console.WriteLine($"the methods the scripts called most expensively, {methods.Values.Sum(m => m.Ticks) / ticksPerMs} ms in all:");
			foreach (var (name, (ticks, calls)) in methods.OrderByDescending(m => m.Value.Ticks).Take(25))
			{
				Console.WriteLine($"   {name,-44} {ticks / ticksPerMs,7} ms {calls,9} calls {(double)ticks * 1000 / ticksPerMs / calls,9:0.0} us a call");
			}
		}
		return 0;
	}

	private static string Describe(List<double> hours)
	{
		if (hours.Count == 0)
		{
			return "none";
		}
		var sorted = hours.OrderBy(h => h).ToList();
		return $"{hours.Count} of them, mean {hours.Average():0.00} ms, median {sorted[sorted.Count / 2]:0.00} ms, " +
			$"most {sorted[^1]:0.00} ms";
	}
}
