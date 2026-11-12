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
		CDirectorySettings directories = CDirectorySettings.At(install);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var scripts = new CAiScripts(directories, _ => { });
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts);
		state.Played.Clear();
		return (state, new CSimulation(state), scripts);
	}
}
