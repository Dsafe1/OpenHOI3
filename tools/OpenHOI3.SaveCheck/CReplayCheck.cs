using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
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
		var client = new OpenHOI3.Sim.Multiplayer.CClientSession(clientRun, line =>
		{
			sentToHost += line.Length;
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
			toClient.Enqueue(line);
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
			if (frames % 3 == 0)
			{
				clientClock.Start();
				while (toClient.Count > 0)
				{
					client.Receive(toClient.Dequeue());
				}
				client.Step(mostHours: 8);
				clientClock.Stop();
				while (toHost.Count > 0)
				{
					host.Receive(peer, toHost.Dequeue());
				}
			}
		}
		// What is still under way, so that the two stand at the same hour.
		while (toClient.Count > 0)
		{
			client.Receive(toClient.Dequeue());
		}
		client.Step(mostHours: 1000);

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
		while (recorded.Date < end)
		{
			CDate before = recorded.Date;
			recordClock.Start();
			recordedRun.RunHours(1);
			recordClock.Stop();
			replayClock.Start();
			replayedRun.RunHours(1);
			replayClock.Stop();
			if (recorded.Date == before)
			{
				continue;
			}

			days++;
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
		recordedScripts.Dispose();
		replayedScripts.Dispose();
		return differing == 0 ? 0 : 1;
	}

	/// <summary>A new game, nobody playing, entered and ready for its first hour.</summary>
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
