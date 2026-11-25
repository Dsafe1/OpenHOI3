using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Replays;
using OpenHOI3.Content.Saves;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Replay;

/// <summary>
/// Whether a game's recording plays back across its saves and loads (docs/replay.md, section 10). A game is recorded
/// from its start beside its saves; at each date given it is saved, the save loaded, and the loaded game recorded on as
/// a recording of its own that names the save's place. Then the chain of recordings that leads to the last save is
/// played back from a new game: at each save's mark the game playing back writes a save of its own and loads it, as the
/// player did. It says, for every midnight, whether the game played back is the game that was played.
/// </summary>
internal static class CReplayLoadCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck replayload <install folder> [save date ...] [player=TAG] [folder=FOLDER]");
			return 2;
		}
		string[] rest = args.Skip(2).ToArray();
		List<CDate> saves = rest.Select(text => CDate.TryParse(text, out CDate date) ? date : (CDate?)null)
			.Where(date => date is not null).Select(date => date!.Value).ToList();
		if (saves.Count == 0)
		{
			saves = new List<CDate> { new(1936, 1, 10), new(1936, 1, 20), new(1936, 2, 1) };
		}
		string? player = rest.FirstOrDefault(a => a.StartsWith("player=", StringComparison.Ordinal))?["player=".Length..];
		string folder = rest.FirstOrDefault(a => a.StartsWith("folder=", StringComparison.Ordinal))?["folder=".Length..]
			?? Path.Combine(Path.GetTempPath(), "openhoi3_replayload");
		CCountryTag[] played = player is null ? Array.Empty<CCountryTag>() : new[] { new CCountryTag(player) };
		if (Directory.Exists(folder))
		{
			Directory.Delete(folder, recursive: true);
		}
		Directory.CreateDirectory(folder);
		string store = CReplayStore.Folder(folder);

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var scripts = new List<IDisposable>();

		CGameState Make(string? save)
		{
			var ai = new CAiScripts(directories, _ => { });
			scripts.Add(ai);
			CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
			CGameState state = save is null
				? CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: ai, played: played)
				: CSaveLoader.Load(directories, map, localisation, countries, save, played.FirstOrDefault(), ai, _ => { }).State;
			if (player is null)
			{
				state.Played.Clear();
			}
			return state;
		}

		// The game played: a recording to each save, and from each save a recording of its own.
		var hashes = new List<(CDate Date, ulong[] Hash)>();
		var recordClock = Stopwatch.StartNew();
		CReplayLink? link = null;
		string? savePath = null;
		bool keyKept = true;
		for (int stretch = 0; stretch < saves.Count; stretch++)
		{
			CGameState state = Make(savePath);
			if (stretch > 0 && state.Rules.Enhanced && state.ReplayLink != link)
			{
				keyKept = false;
			}
			(CReplayFile file, CReplay recording) = CReplayStore.Begin(store,
				new CReplayHeader(state.Played.ToList(), state.Seed, CReplayStore.NewName(), link, "replayload"));
			state.Replay = recording;
			var run = new CSimulation(state);
			while (state.Date < saves[stretch])
			{
				CDate before = state.Date;
				run.RunHours(1);
				if (state.Date != before)
				{
					hashes.Add((state.Date, CStateHash.Of(state)));
				}
			}
			run.Pump();
			run.MarkSaved();
			link = state.ReplayLink;
			savePath = Path.Combine(folder, $"save{stretch + 1}.hoi3");
			CSaveFiles.Write(state, localisation.Localise, savePath);
			file.Dispose();
			Console.WriteLine($"played to {state.Date} and saved, the save at {link?.Text}" +
				(state.Rules.Enhanced ? "" : " (a faithful game: its save holds no key of the recording, and the check carries the place)"));
		}
		recordClock.Stop();
		long recorded = Directory.GetFiles(store).Sum(path => new FileInfo(path).Length);

		// The game played back: the chain to the last save, a save written and loaded at each crossing.
		List<CReplaySegment>? chain = link is null ? null : CReplayStore.Chain(store, link, Console.WriteLine);
		if (chain is null)
		{
			Console.WriteLine("no chain of recordings leads to the last save");
			return 1;
		}
		Console.WriteLine($"{chain.Count} recordings lead to it, {recorded / 1024} KiB");
		var replayClock = Stopwatch.StartNew();
		int at = 0;
		int same = 0;
		bool told = false;
		string? crossing = null;
		foreach (CReplaySegment segment in chain)
		{
			CGameState state = Make(crossing);
			using CReplayFile file = CReplayFile.Open(segment.Path, state);
			state.Replay = CReplay.Replaying(file, segment.UntilMark);
			var run = new CSimulation(state);
			try
			{
				while (true)
				{
					CDate before = state.Date;
					run.RunHours(1);
					if (state.Date == before)
					{
						continue;
					}
					if (at >= hashes.Count)
					{
						Console.WriteLine($"  the playback runs on past the game played, at {state.Date}");
						return 1;
					}
					(CDate date, ulong[] hash) = hashes[at++];
					ulong[] now = CStateHash.Of(state);
					if (date == state.Date && now.SequenceEqual(hash))
					{
						same++;
					}
					else if (!told)
					{
						told = true;
						Console.WriteLine($"  first differs on {state.Date} (the game played: {date}) in " +
							string.Join(", ", CStateHash.Parts.Where((_, i) => now[i] != hash[i])));
					}
				}
			}
			catch (CReplayEndedException)
			{
				// The save's mark: the playback stands where the game was saved.
			}
			if (state.Replay.LastMark != segment.UntilMark)
			{
				Console.WriteLine($"  the recording {Path.GetFileName(segment.Path)} ended at {state.Date} before its mark {segment.UntilMark}");
				return 1;
			}
			crossing = Path.Combine(folder, "crossing.hoi3");
			CSaveFiles.Write(state, localisation.Localise, crossing);
		}
		replayClock.Stop();

		Console.WriteLine($"{same} of {hashes.Count} days the same, {at} played back, across {chain.Count - 1} loads; played in " +
			$"{recordClock.ElapsedMilliseconds / 1000.0:0.0} s, played back in {replayClock.ElapsedMilliseconds / 1000.0:0.0} s" +
			(keyKept ? "" : "; A LOADED SAVE DID NOT GIVE BACK ITS PLACE IN THE RECORDING"));
		scripts.ForEach(one => one.Dispose());
		return same == hashes.Count && at == hashes.Count && keyKept ? 0 : 1;
	}
}
