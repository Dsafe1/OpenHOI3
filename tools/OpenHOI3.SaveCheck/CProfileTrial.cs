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
///   OpenHOI3.SaveCheck profile &lt;install folder&gt; [days] [tag] [from=DATE] [save=FILE]
///
/// Thirty days as Germany with none given; from= makes the game at a later bookmark's date, and save= loads a saved
/// game in a new one's place, played by whoever the save says played it. It tells how long the loading took, how long an hour takes
/// on an ordinary hour and on one that turns a day, the slowest hours, and every step of the tick by
/// the time it took in all. The figures are this machine's and this build's.
/// </summary>
internal static class CProfileTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck profile <install folder> [days] [tag] [from=DATE] [save=FILE] [warm=DAYS] [lua] [inturn] [orders] [hash] [without=FIX]");
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
		// save=FILE: a saved game loaded as the client loads one, its own player playing.
		const string saveWord = "save=";
		string? save = args.FirstOrDefault(a => a.StartsWith(saveWord, StringComparison.Ordinal))?[saveWord.Length..];
		CGameState state;
		if (save is not null)
		{
			(state, _) = OpenHOI3.Content.Saves.CSaveLoader.Load(directories, map, localisation, countries, save, null, scripts, warnings.Add);
			tag = state.Played.FirstOrDefault().Value ?? tag;
		}
		else
		{
			state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
				played: new[] { new CCountryTag(tag) }, date: from);
			state.Played.Clear();
			state.Played.Add(new CCountryTag(tag));
		}
		// without=NAME: an enhanced game played without that fix, whatever its save says - to set a speed-up beside
		// the same play as before a fix was built.
		const string withoutWord = "without=";
		if (state.Rules.Enhanced && args.Where(a => a.StartsWith(withoutWord, StringComparison.Ordinal))
			.Select(a => OpenHOI3.Sim.Rules.CFixes.Named(a[withoutWord.Length..])).Where(f => f is not null).Select(f => f!.Value).ToList() is { Count: > 0 } off)
		{
			state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(state.Rules.Without.Concat(off)));
		}
		// inturn: the AI's passes that run their countries side by side run them in turn, to time the one beside the other.
		state.AiInTurn = args.Contains("inturn");
		var simulation = new CSimulation(state);
		Console.WriteLine($"{(save is null ? "a new game" : "a saved game")} as {tag}, {state.Date}, loaded in {loading.ElapsedMilliseconds} ms");

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
		// orders: every order counted by its kind and its country, and a transfer by the type of what was moved.
		var orders = new Dictionary<string, (int Ran, int Refused)>();
		if (args.Skip(2).Contains("orders"))
		{
			simulation.CommandRan = (command, ran) =>
			{
				Type kind = command.GetType();
				const System.Reflection.BindingFlags own = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
				string owner = kind.GetField("_owner", own)?.GetValue(command)?.ToString() ?? "";
				string key = kind.Name + " " + owner;
				if (command is OpenHOI3.Sim.Commands.CTransferSubUnitCommand && kind.GetField("_subUnit", own)?.GetValue(command) is int id
					&& OpenHOI3.Sim.Units.CUnitFinder.FindSubUnit(state, new CCountryTag(owner), id) is { } moved)
				{
					key += " " + moved.Type.Key;
				}
				(int Ran, int Refused) was = orders.GetValueOrDefault(key);
				orders[key] = ran ? (was.Ran + 1, was.Refused) : (was.Ran, was.Refused + 1);
			};
		}
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
		// hash: the state's hash as the run ends, part by part - two builds that play the same end on the same line.
		if (args.Skip(2).Contains("hash"))
		{
			ulong[] parts = OpenHOI3.Sim.Replay.CStateHash.Of(state);
			Console.WriteLine("hash " + string.Join(" ", OpenHOI3.Sim.Replay.CStateHash.Parts.Select((name, i) => $"{name}={parts[i]:x16}")));
		}

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
		if (orders.Count > 0)
		{
			Console.WriteLine("the orders, by kind and country, the most first:");
			foreach (var (key, (ran, refused)) in orders.OrderByDescending(o => o.Value.Ran + o.Value.Refused).Take(60))
			{
				Console.WriteLine($"   {key,-70} {ran,6} run {refused,6} refused");
			}
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
