using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Replay;

/// <summary>
/// Whether a game plays back from a recording of its AI (docs/replay.md): two new games with nobody playing, run hour by
/// hour side by side to a date - the first with its AI recorded, the second with the recording in the AI's place - and set
/// beside each other at every midnight, part by part. On the first day they part it names the figures that differ: each
/// country's, province's and unit's. And it says how long each game's hours took.
/// </summary>
internal static class CReplayCheck
{
	/// <summary>
	/// A host's game and a client's, two new games with nobody playing, with nothing between them but the lines of text
	/// two machines would send (docs/multiplayer.md, section 5): the host runs an hour a frame with its AI, the client
	/// follows through a <c>CClientSession</c>, kept a few frames behind so that the host's waiting is exercised, and once
	/// a day the client's side posts an order of its own. It says how many days ran, whether the client ever found its
	/// midnight's hash another than the host's, how much was sent, and how long each side took.
	/// </summary>
	public static int RunTwoMachines(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck twomachines <install folder> [until date] [window]");
			return 2;
		}
		CDate end = args.Length > 2 && CDate.TryParse(args[2], out CDate given) ? given : new CDate(1936, 2, 1);
		int window = args.Length > 3 && int.TryParse(args[3], out int asked) ? asked : OpenHOI3.Sim.Multiplayer.CHostSession.DefaultWindow;

		(CGameState hostState, CSimulation hostRun, IDisposable hostScripts) = Load(args[1]);
		(CGameState clientState, CSimulation clientRun, IDisposable clientScripts) = Load(args[1]);
		var toClient = new Queue<string>();
		var toHost = new Queue<string>();
		long sentToClient = 0;
		long sentToHost = 0;
		int longest = 0;
		var host = new OpenHOI3.Sim.Multiplayer.CHostSession(hostRun, window);
		// With `tcp` the lines go over a real connection on this machine's loopback, compressed, where otherwise they are
		// handed across in lists.
		bool tcp = args.Contains("tcp");
		OpenHOI3.Sim.Multiplayer.CLineListener? listener = tcp ? new OpenHOI3.Sim.Multiplayer.CLineListener(port: 0) : null;
		OpenHOI3.Sim.Multiplayer.CLineLink? clientLink = listener is null ? null
			: OpenHOI3.Sim.Multiplayer.CLineLink.Connect("127.0.0.1", listener.Port);
		OpenHOI3.Sim.Multiplayer.CLineLink? hostLink = null;
		while (listener is not null && !listener.TryAccept(out hostLink!))
		{
			System.Threading.Thread.Sleep(1);
		}
		void Carry()
		{
			while (clientLink is not null && clientLink.TryReceive(out string came))
			{
				toClient.Enqueue(came);
			}
			while (hostLink is not null && hostLink.TryReceive(out string came))
			{
				toHost.Enqueue(came);
			}
		}
		var client = new OpenHOI3.Sim.Multiplayer.CClientSession(clientRun, line =>
		{
			sentToHost += line.Length;
			if (clientLink is not null)
			{
				clientLink.Send(line);
				return;
			}
			toHost.Enqueue(line);
		});
		// What the same lines would come to compressed as one stream, flushed after every line so that each could be read
		// as it arrives: by Deflate at its fastest and by Brotli at a middling quality, with the time each takes.
		var deflated = new System.IO.MemoryStream();
		var brotlied = new System.IO.MemoryStream();
		var deflate = new System.IO.Compression.DeflateStream(deflated, System.IO.Compression.CompressionLevel.Fastest);
		var brotli = new System.IO.Compression.BrotliStream(brotlied, System.IO.Compression.CompressionLevel.Fastest);
		var deflateClock = new Stopwatch();
		var brotliClock = new Stopwatch();
		OpenHOI3.Sim.Multiplayer.CHostPeer peer = host.Add("client", line =>
		{
			sentToClient += line.Length;
			longest = Math.Max(longest, line.Length);
			if (hostLink is not null)
			{
				hostLink.Send(line);
			}
			else
			{
				toClient.Enqueue(line);
			}
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line + "\n");
			deflateClock.Start();
			deflate.Write(bytes, 0, bytes.Length);
			deflate.Flush();
			deflateClock.Stop();
			brotliClock.Start();
			brotli.Write(bytes, 0, bytes.Length);
			brotli.Flush();
			brotliClock.Stop();
		});
		bool parted = false;
		host.Parted += _ => parted = true;

		var hostClock = new Stopwatch();
		var clientClock = new Stopwatch();
		int days = 0;
		int waited = 0;
		long frames = 0;
		hostRun.Pace.Set(paused: false, CClock.SpeedCount - 1);
		while (hostState.Date < end && !parted)
		{
			frames++;
			CDate before = hostState.Date;
			long hoursBefore = hostState.TotalHours;
			hostClock.Start();
			hostRun.Advance(1.0);
			host.Tell();
			hostClock.Stop();
			if (hostState.TotalHours == hoursBefore)
			{
				waited++;
			}
			if (hostState.Date != before)
			{
				days++;
				// An order from the client's side, once a day: it goes to the host and comes back in the host's order.
				clientRun.Post(new OpenHOI3.Sim.Commands.CFogOfWarCommand(days % 2 == 0));
			}
			// The client's frame comes every third of the host's: it falls behind, the host waits, it catches up.
			Carry();
			if (frames % 3 == 0)
			{
				clientClock.Start();
				while (toClient.Count > 0)
				{
					client.Receive(toClient.Dequeue());
				}
				client.Step(mostHours: 8);
				clientClock.Stop();
			}
			Carry();
			while (toHost.Count > 0)
			{
				host.Receive(peer, toHost.Dequeue());
			}
		}
		// What is still under way, so that the two stand at the same hour: over a connection it comes when it comes.
		var patience = Stopwatch.StartNew();
		do
		{
			Carry();
			while (toClient.Count > 0)
			{
				client.Receive(toClient.Dequeue());
			}
			client.Step(mostHours: 1000);
			if (tcp && clientState.TotalHours < hostState.TotalHours)
			{
				System.Threading.Thread.Sleep(1);
			}
		}
		while (tcp && !client.Parted && clientState.TotalHours < hostState.TotalHours && patience.ElapsedMilliseconds < 10000);
		clientLink?.Dispose();
		hostLink?.Dispose();
		listener?.Dispose();
		Console.WriteLine(tcp ? "over a TCP connection on the loopback, compressed" : "the lines handed across in lists");

		ulong[] hostHash = CStateHash.Of(hostState);
		ulong[] clientHash = CStateHash.Of(clientState);
		string[] differing = CStateHash.Parts.Where((_, i) => hostHash[i] != clientHash[i]).ToArray();
		Console.WriteLine($"to {hostState.Date}: {days} days, window {window}; the host waited on {waited} of {frames} frames; " +
			$"{sentToClient / 1024} KiB to the client ({sentToClient / Math.Max(days, 1) / 1024} KiB a day, the longest line " +
			$"{longest} characters), {sentToHost / 1024} KiB to the host; host {hostClock.ElapsedMilliseconds / 1000.0:0.0} s, " +
			$"client {clientClock.ElapsedMilliseconds / 1000.0:0.0} s");
		Console.WriteLine($"compressed as a stream flushed at every line: Deflate {deflated.Length / 1024} KiB " +
			$"({100.0 * deflated.Length / Math.Max(sentToClient, 1):0.0}% of the text, {deflateClock.ElapsedMilliseconds} ms), " +
			$"Brotli {brotlied.Length / 1024} KiB ({100.0 * brotlied.Length / Math.Max(sentToClient, 1):0.0}%, " +
			$"{brotliClock.ElapsedMilliseconds} ms)");
		Console.WriteLine(client.Parted || parted
			? $"the client's game parted from the host's at hour {clientState.TotalHours}"
			: hostState.TotalHours != clientState.TotalHours
				? $"the client stands at hour {clientState.TotalHours}, the host at {hostState.TotalHours}"
				: differing.Length > 0
					? "at the end they differ in " + string.Join(", ", differing)
					: "every midnight's hash the same, and the two games alike at the end");
		hostScripts.Dispose();
		clientScripts.Dispose();
		return client.Parted || parted || differing.Length > 0 || hostState.TotalHours != clientState.TotalHours ? 1 : 0;
	}

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck replaycheck <install folder> [until date]");
			return 2;
		}
		CDate end = args.Length > 2 && CDate.TryParse(args[2], out CDate given) ? given : new CDate(1936, 2, 1);
		// from=DATE: from that date the two are set beside each other every hour, not only at midnight, and the first
		// difference shows each differing pool's goods and the steps recorded in that hour and the one before.
		string? fromText = args.FirstOrDefault(a => a.StartsWith("from=", StringComparison.Ordinal))?["from=".Length..];
		CDate? hourlyFrom = fromText is not null && CDate.TryParse(fromText, out CDate from) ? from : null;

		CReplay recording = CReplay.Recording();
		(CGameState recorded, CSimulation recordedRun, IDisposable recordedScripts) = Load(args[1]);
		(CGameState replayed, CSimulation replayedRun, IDisposable replayedScripts) = Load(args[1]);
		// The AI is recorded and played back from the first hour; what entering the game ran is the same in both.
		recorded.Replay = recording;
		// Each step goes through the file's form and back, read against the replaying game, as a playback from disk would be.
		replayed.Replay = CReplay.Following(recording, step => CReplayFile.Through(step, replayed));

		var recordClock = new Stopwatch();
		var replayClock = new Stopwatch();
		int days = 0;
		int same = 0;
		int differing = 0;
		int strategiesSame = 0;
		int strategiesShown = 0;
		while (recorded.Date < end)
		{
			CDate before = recorded.Date;
			recordClock.Start();
			recordedRun.RunHours(1);
			recordClock.Stop();
			replayClock.Start();
			replayedRun.RunHours(1);
			replayClock.Stop();
			if (hourlyFrom is { } start && recorded.Date >= start)
			{
				ulong[] a = CStateHash.Of(recorded);
				ulong[] b = CStateHash.Of(replayed);
				if (!a.SequenceEqual(b))
				{
					ShowHour(recording, recorded, replayed);
					break;
				}
			}
			if (recorded.Date == before)
			{
				continue;
			}

			days++;
			// Every country's strategy lists, which the hash leaves out and the replaying game makes for itself
			// (multiplayer.md, section 12).
			List<string> otherStrategies = recorded.Countries.Zip(replayed.Countries)
				.Where(pair => StrategyText(pair.First) != StrategyText(pair.Second))
				.Select(pair => pair.First.Tag.Value).ToList();
			if (otherStrategies.Count == 0)
			{
				strategiesSame++;
			}
			else if (strategiesShown++ < 5)
			{
				var first = recorded.Countries.Zip(replayed.Countries).First(pair => StrategyText(pair.First) != StrategyText(pair.Second));
				Console.WriteLine($"  {recorded.Date}: the strategies of {otherStrategies.Count} countries differ ({string.Join(", ", otherStrategies.Take(12))})");
				Console.WriteLine($"    {first.First.Tag} recorded: {StrategyText(first.First)}");
				Console.WriteLine($"    {first.First.Tag} replayed: {StrategyText(first.Second)}");
			}
			ulong[] then = CStateHash.Of(recorded);
			ulong[] now = CStateHash.Of(replayed);
			string[] parted = CStateHash.Parts.Where((_, i) => now[i] != then[i]).ToArray();
			if (parted.Length == 0)
			{
				same++;
				continue;
			}
			if (differing++ == 0)
			{
				Dictionary<string, ulong> fields = CStateHash.Fields(recorded).ToDictionary(pair => pair.Key, pair => pair.Value);
				List<string> figures = CStateHash.Fields(replayed)
					.Where(pair => !fields.TryGetValue(pair.Key, out ulong value) || value != pair.Value)
					.Select(pair => pair.Key).ToList();
				Console.WriteLine($"  {recorded.Date}: first differs in {string.Join(", ", parted)} - {figures.Count} figures: " +
					string.Join("; ", figures.Take(40)));
			}
			else if (differing <= 5)
			{
				Console.WriteLine($"  {recorded.Date}: differs in {string.Join(", ", parted)}");
			}
		}
		int commands = recording.Steps.Sum(step => step.Commands.Count);
		Console.WriteLine($"to {end}: recorded in {recordClock.ElapsedMilliseconds / 1000.0:0.0} s ({recording.Steps.Count()} steps, " +
			$"{commands} commands, {recording.Steps.Sum(step => step.Draws)} draws of the game's generator and " +
			$"{recording.Steps.Sum(step => step.AiDraws)} of the AI's), replayed in " +
			$"{replayClock.ElapsedMilliseconds / 1000.0:0.0} s; {same} of {days} days the same");
		Console.WriteLine($"every country's strategy lists the same on {strategiesSame} of {days} days");
		recordedScripts.Dispose();
		replayedScripts.Dispose();
		return differing == 0 && strategiesSame == days ? 0 : 1;
	}

	/// <summary>
	/// A country's strategy as one line: when it was made, its personality and each list the making fills, in order with
	/// its weights. Not the military access list, which the AI's scripts write.
	/// </summary>
	private static string StrategyText(OpenHOI3.Sim.Countries.CCountry country)
	{
		OpenHOI3.Sim.Ai.CAiStrategy strategy = country.Strategy;
		static string List<T>(string name, OpenHOI3.Sim.Ai.CStrategyList<T> list) where T : notnull =>
			$" {name}[{string.Join(",", list.Entries.Select(entry => $"{entry.Key}={entry.Weight}"))}]";
		return $"made {strategy.IsMade} at {strategy.MadeHour} {strategy.Personality}"
			+ List("conquer", strategy.ConquerProvinces) + List("defend", strategy.DefendProvinces)
			+ List("threat", strategy.Threat) + List("antagonize", strategy.Antagonize) + List("befriend", strategy.Befriend)
			+ List("protect", strategy.Protect) + List("rival", strategy.Rival) + List("vassal", strategy.Vassal);
	}

	/// <summary>The first hour the two part: the figures, each differing pool good by good, and the steps recorded around it.</summary>
	private static void ShowHour(CReplay recording, CGameState recorded, CGameState replayed)
	{
		Console.WriteLine($"  first differs at {recorded.Date} {recorded.Hour}h, hour {recorded.TotalHours}");
		Dictionary<string, ulong> fields = CStateHash.Fields(recorded).ToDictionary(pair => pair.Key, pair => pair.Value);
		List<string> figures = CStateHash.Fields(replayed)
			.Where(pair => !fields.TryGetValue(pair.Key, out ulong value) || value != pair.Value)
			.Select(pair => pair.Key).ToList();
		Console.WriteLine($"  {figures.Count} figures: " + string.Join("; ", figures.Take(60)));
		foreach (string figure in figures.Take(25))
		{
			OpenHOI3.Sim.Economy.CGoodsPool? Pool(CGameState state)
			{
				string[] words = figure.Split(' ');
				if (words.Length == 2 && words[1] == "stockpile")
				{
					return state.Country(new OpenHOI3.Sim.Countries.CCountryTag(words[0])) is { } country ? state.Stockpile(country) : null;
				}
				if (words.Length == 3 && words[0] == "province" && words[2] == "pool")
				{
					int id = int.Parse(words[1], System.Globalization.CultureInfo.InvariantCulture);
					return state.Provinces.FirstOrDefault(p => p.Id == id)?.Pool;
				}
				return null;
			}
			if (Pool(recorded) is not { } x || Pool(replayed) is not { } y)
			{
				continue;
			}
			var goods = Enumerable.Range(0, OpenHOI3.Sim.Economy.CGoodsPool.Count)
				.Select(g => (OpenHOI3.Sim.Economy.EGoods)g)
				.Where(g => x[g].Raw != y[g].Raw)
				.Select(g => $"{g} {x[g]} / {y[g]}");
			Console.WriteLine($"    {figure}: recorded / replayed: {string.Join(", ", goods)}");
		}
		long hour = recorded.TotalHours;
		foreach (CReplayStep step in recording.Steps.Where(step => step.Hour >= hour - 2 && step.Hour <= hour))
		{
			Console.WriteLine($"    recorded at hour {step.Hour}: {step.Step}, {step.Commands.Count} commands " +
				$"({string.Join(", ", step.Commands.GroupBy(c => c.GetType().Name).Select(g => $"{g.Key} x{g.Count()}"))}), " +
				$"{step.Draws} draws, {step.AiDraws} AI draws, {step.UnitIds} unit ids");
		}
	}

	/// <summary>A new game, nobody playing, entered and ready for its first hour.</summary>
	/// <summary>
	/// A machine joining a game that is running (docs/multiplayer.md, sections 2 and 4.1): a host plays a new game alone
	/// to a date; there it saves, and the save's bytes are offered to a second machine and sent it line by line; **both
	/// load that save, each with Lua states made afresh**, and play on as host and client to a second date. It says how
	/// large the save was and how it travelled, and whether every midnight's hash after the load was the same.
	/// </summary>
	public static int RunJoin(string[] args)
	{
		if (args.Length < 4 || !CDate.TryParse(args[2], out CDate joinAt) || !CDate.TryParse(args[3], out CDate end))
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck joinmachines <install folder> <date joined> <until date>");
			return 2;
		}
		string install = args[1];
		(CGameState alone, CSimulation aloneRun, IDisposable aloneScripts) = Load(install);
		while (alone.Date < joinAt)
		{
			aloneRun.RunHours(1);
		}
		// The orders the last hour's AI posted run in the next drain, which a save written now would leave out.
		aloneRun.Pump();
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(install);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		string hostPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openhoi3_join_host.hoi3");
		string clientPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openhoi3_join_client.hoi3");
		CSaveFiles.Write(alone, localisation.Localise, hostPath);
		byte[] game = System.IO.File.ReadAllBytes(hostPath);
		aloneScripts.Dispose();

		// The save offered and sent, the lines handed across; the joining machine holds no copy of its own.
		long sent = 0;
		int lines = 0;
		OpenHOI3.Sim.Multiplayer.CGameSender? sender = null;
		var receiver = new OpenHOI3.Sim.Multiplayer.CGameReceiver(line => sender!.Receive(line), _ => null);
		sender = new OpenHOI3.Sim.Multiplayer.CGameSender(line =>
		{
			sent += line.Length;
			lines++;
			receiver.Receive(line);
		}, game);
		sender.Offer();
		if (receiver.Game is not { } came)
		{
			Console.WriteLine("the game did not come whole");
			return 1;
		}
		System.IO.File.WriteAllBytes(clientPath, came);
		using var deflated = new System.IO.MemoryStream();
		using (var deflate = new System.IO.Compression.DeflateStream(deflated, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
		{
			deflate.Write(game, 0, game.Length);
		}
		Console.WriteLine($"saved at {alone.Date}: {game.Length / 1024} KiB, sent as {lines} lines of {sent / 1024} KiB of text; " +
			$"compressed as the connection would, {deflated.Length / 1024} KiB");

		// Every machine loads the one save, the host too, each with scripts of its own made now.
		(CGameState hostState, CSimulation hostRun, IDisposable hostScripts) = LoadSave(install, hostPath);
		(CGameState clientState, CSimulation clientRun, IDisposable clientScripts) = LoadSave(install, clientPath);
		receiver.SayLoaded();

		var toClient = new Queue<string>();
		var toHost = new Queue<string>();
		var host = new OpenHOI3.Sim.Multiplayer.CHostSession(hostRun);
		var client = new OpenHOI3.Sim.Multiplayer.CClientSession(clientRun, toHost.Enqueue);
		OpenHOI3.Sim.Multiplayer.CHostPeer peer = host.Add("client", toClient.Enqueue);
		int days = 0;
		hostRun.Pace.Set(paused: false, CClock.SpeedCount - 1);
		while (hostState.Date < end && !client.Parted)
		{
			CDate before = hostState.Date;
			hostRun.Advance(1.0);
			host.Tell();
			while (toClient.Count > 0)
			{
				client.Receive(toClient.Dequeue());
			}
			client.Step(mostHours: 8);
			while (toHost.Count > 0)
			{
				host.Receive(peer, toHost.Dequeue());
			}
			if (hostState.Date != before)
			{
				days++;
			}
		}
		ulong[] hostHash = CStateHash.Of(hostState);
		ulong[] clientHash = CStateHash.Of(clientState);
		string[] differing = CStateHash.Parts.Where((_, i) => hostHash[i] != clientHash[i]).ToArray();
		Console.WriteLine(client.Parted
			? $"{days} days after the load: the client's game parted from the host's at hour {clientState.TotalHours}, {clientState.Date}"
			: differing.Length > 0
				? $"{days} days after the load they differ in " + string.Join(", ", differing)
				: $"{days} days after the load, to {hostState.Date}: every midnight's hash the same, and the two games alike at the end");
		hostScripts.Dispose();
		clientScripts.Dispose();
		return client.Parted || differing.Length > 0 ? 1 : 0;
	}

	/// <summary>A save loaded as a game nobody plays, with scripts made for it, entered and ready for its first hour.</summary>
	private static (CGameState State, CSimulation Simulation, IDisposable Scripts) LoadSave(string install, string path)
	{
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(install);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var scripts = new CAiScripts(directories, _ => { });
		(CGameState state, _) = CSaveLoader.Load(directories, map, localisation, countries, path, null, scripts, _ => { });
		state.Played.Clear();
		return (state, new CSimulation(state), scripts);
	}

	private static (CGameState State, CSimulation Simulation, IDisposable Scripts) Load(string install)
	{
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(install);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var scripts = new CAiScripts(directories, _ => { });
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts);
		state.Played.Clear();
		return (state, new CSimulation(state), scripts);
	}
}
