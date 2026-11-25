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
/// The game that led to a save of a player's, played back with no screen (docs/replay.md, section 10): the chain of
/// recordings beside the save, from the first one's start, a save written and loaded at each place the player loaded.
/// It says how far each recording played and whether the playback came to the save's own place - or where, and with
/// what words, it stopped fitting. A look at a recording made in play, which no check makes: nothing is written but
/// the crossings' saves, to the temporary folder.
/// </summary>
internal static class CReplaySaveCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck replaysave <install folder> <save.hoi3> [hash]");
			return 2;
		}
		string savePath = Path.GetFullPath(args[2]);
		bool hash = args.Contains("hash");
		string store = CReplayStore.Folder(Path.GetDirectoryName(savePath)!);
		CReplayLink? link = CReplayStore.LinkOf(File.ReadAllText(savePath, System.Text.Encoding.Latin1));
		List<CReplaySegment>? chain = link is null ? null : CReplayStore.Chain(store, link, Console.WriteLine);
		byte[]? start = chain is null ? null : CReplayStore.ReadStart(store, chain[0].Header.Segment ?? "");
		if (link is null || chain is null || start is null)
		{
			Console.WriteLine(link is null ? "the save names no recording" : chain is null
				? "the recordings cannot be followed back to a start" : "the first recording's start is missing");
			return 1;
		}
		Console.WriteLine($"{Path.GetFileName(savePath)} is at {link.Text}; {chain.Count} recordings lead to it");

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		string crossing = Path.Combine(Path.GetTempPath(), $"openhoi3_replaysave_{Environment.ProcessId}.hoi3");
		File.WriteAllBytes(crossing, start);
		var clock = Stopwatch.StartNew();
		try
		{
			for (int index = 0; index < chain.Count; index++)
			{
				CReplaySegment segment = chain[index];
				bool first = segment.Header.Parent is null;
				using var scripts = new CAiScripts(directories, _ => { });
				CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
				CCountryTag? player = segment.Header.Played.Count > 0 ? segment.Header.Played[0] : null;
				// A start is loaded with its player; a save played on with its own, the country chosen taken up after, as
				// the lobby's Play does. The lobby's choices are left as the save has them: a look, not the client.
				CGameState state = CSaveLoader.Load(directories, map, localisation, countries, crossing, first ? player : null, scripts,
					_ => { }).State;
				if (!first && player is { } chosen)
				{
					bool automated = state.Played.Any(played => state.AutomateTrade.Remove(played));
					state.Played.Clear();
					state.Played.Add(chosen);
					if (automated)
					{
						state.AutomateTrade.Add(chosen);
					}
					OpenHOI3.Sim.Settings.CAutomation.SetMinisters(state);
					OpenHOI3.Sim.Modifiers.CCountryModifiers.Rebuild(state);
				}
				using CReplayFile file = CReplayFile.Open(segment.Path, state);
				state.Replay = CReplay.Replaying(file, segment.Until);
				var run = new CSimulation(state);
				CDate from = state.Date;
				int fromHour = state.Hour;
				try
				{
					while (true)
					{
						CDate before = state.Date;
						run.RunHours(1);
						if (hash && state.Date != before)
						{
							Console.WriteLine($"  {state.Date} hash {CStateHash.Whole(CStateHash.Of(state)):x16}");
						}
					}
				}
				catch (CReplayOutOfStepException parted)
				{
					Console.WriteLine($"recording {index + 1} ({segment.Header.Segment}), from {from} {fromHour}h: STOPS FITTING at " +
						$"{state.Date} {state.Hour}h - {parted.Message}");
					return 1;
				}
				catch (CReplayEndedException)
				{
				}
				bool stands = state.Replay.StandsAt(segment.Until);
				Console.WriteLine($"recording {index + 1} ({segment.Header.Segment}), from {from} {fromHour}h to {state.Date} {state.Hour}h: " +
					(stands ? $"played to its place {segment.Until.Text}" : $"ENDED BEFORE its place {segment.Until.Text}"));
				if (!stands)
				{
					return 1;
				}
				CSaveFiles.Write(state, localisation.Localise, crossing);
			}
		}
		finally
		{
			File.Delete(crossing);
		}
		Console.WriteLine($"played back whole in {clock.ElapsedMilliseconds / 1000.0:0.0} s");
		return 0;
	}
}
