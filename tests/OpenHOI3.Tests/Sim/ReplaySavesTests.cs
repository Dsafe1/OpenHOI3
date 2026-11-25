using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Replays;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Tests.Sim;

/// <summary>A game's recording kept beside its saves, and played back across them (docs/replay.md, section 10).</summary>
public class ReplaySavesTests
{
	private static CGameState State(bool enhanced = false)
	{
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, Array.Empty<CCountry>());
		if (enhanced)
		{
			state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		}
		return state;
	}

	private static string Folder() => Path.Combine(Path.GetTempPath(), $"openhoi3-replays-{Guid.NewGuid():N}");

	/// <summary>A command that, as it runs, has the game call a decision of the AI's - as a province changing hands has its agents told.</summary>
	private sealed class CCalls : CCommand
	{
		public override void Execute(CGameState state)
		{
			state.Replay!.RunCalledStep(state, "told #1", game => game.Outbox.Add(new CFogOfWarCommand(false)));
		}
	}

	[Fact]
	public void AStepTheGameCalledInADrainBetweenTwoHoursIsPlayedBackInThatDrain()
	{
		// A game played with frames between its hours: an order runs in a drain of the queue that has no hour, and the
		// game calls a decision of the AI's as it runs. A playback has to make that drain to be given the step - until
		// 2026-11-25 it found the step where it looked for an hour and stopped, as a played game's did on the day
		// provinces changed hands.
		CGameState recorded = State();
		recorded.FogOfWar = true;
		var recordedRun = new CSimulation(recorded);
		CReplay recording = CReplay.Recording();
		recorded.Replay = recording;
		recordedRun.RunHours(1);
		recordedRun.Post(new CCalls());
		recordedRun.Pump();
		recordedRun.Pump();
		recordedRun.RunHours(1);
		Assert.False(recorded.FogOfWar);
		// The step lies between the two hours, before the entry of the drain it was called in.
		List<string> kinds = recording.Entries.Select(entry => entry.GetType().Name).ToList();
		Assert.True(kinds.IndexOf(nameof(CReplayStep)) < kinds.LastIndexOf(nameof(CReplayDrain)));
		Assert.True(kinds.IndexOf(nameof(CReplayStep)) < kinds.LastIndexOf(nameof(CReplayTick)));

		CGameState replayed = State();
		replayed.FogOfWar = true;
		var replayedRun = new CSimulation(replayed);
		replayed.Replay = CReplay.Replaying(recording.Entries);
		replayedRun.RunHours(2);
		Assert.False(replayed.FogOfWar);
		Assert.Equal(recorded.TotalHours, replayed.TotalHours);

		// A step between two hours that nothing in the game calls: the game is another, and the playback says so.
		CGameState other = State();
		var otherRun = new CSimulation(other);
		other.Replay = CReplay.Replaying(recording.Entries.Where(entry => entry is not CReplayPost).ToList());
		Assert.Throws<CReplayOutOfStepException>(() => otherRun.RunHours(2));
	}

	[Fact]
	public void AHostsRecordingIsKeptAsWellAsSentAndItsSavesMarkIt()
	{
		string folder = Folder();
		string store = CReplayStore.Folder(folder);
		try
		{
			// The header says it is a game between machines, with every country played.
			var played = new[] { new CCountryTag("GER"), new CCountryTag("ENG") };
			(CReplayFile file, _) = CReplayStore.Begin(store, new CReplayHeader(played, State().Seed, "net", null, "engine", null, Net: true));
			CGameState hosted = State();
			var run = new CSimulation(hosted);
			var session = new OpenHOI3.Sim.Multiplayer.CHostSession(run, keep: file.Write, segment: "net");
			var sent = new List<string>();
			session.Add("Ann", sent.Add);
			run.RunHours(2);
			run.MarkSaved();
			Assert.Equal(new CReplayLink("net", 1), hosted.ReplayLink);
			file.Dispose();
			CReplayHeader header = CReplayFile.ReadHeader(CReplayStore.PathOf(store, "net"));
			Assert.True(header.Net);
			Assert.Equal(played, header.Played);
			CReplayStore.Begin(store, new CReplayHeader(played, 5, "plain")).File.Dispose();
			Assert.False(CReplayFile.ReadHeader(CReplayStore.PathOf(store, "plain")).Net);

			// What was kept is what was sent: the client's lines of the recording and the file's are the same entries.
			var kept = new List<string>();
			CGameState read = State();
			read.Played.Add(played[0]);
			read.Played.Add(played[1]);
			using (CReplayFile back = CReplayFile.Open(CReplayStore.PathOf(store, "net"), read))
			{
				while (back.Read() is { } entry)
				{
					kept.Add(CReplayFile.ToLine(entry));
				}
			}
			List<string> wire = sent.Where(line => line.Length > 2 && line[0] == OpenHOI3.Sim.Multiplayer.CWire.Entry).Select(line => line[2..]).ToList();
			Assert.Equal(wire, kept);
			Assert.Contains(kept, line => line.Contains("\"k\":\"v\""));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Fact]
	public void ASaveMarksItsPlaceInARecordingThatHasAName()
	{
		CGameState state = State();
		var run = new CSimulation(state);
		state.Replay = CReplay.Recording(null, "abc");
		run.MarkSaved();
		Assert.Equal(new CReplayLink("abc", 1), state.ReplayLink);
		run.RunHours(1);
		run.MarkSaved();
		Assert.Equal(new CReplayLink("abc", 2), state.ReplayLink);
		Assert.Equal(new[] { 1, 2 }, state.Replay.Entries.OfType<CReplaySaved>().Select(saved => saved.Mark));

		// A recording with no name is kept beside no saves, and a game not recorded marks nothing.
		CGameState plain = State();
		var plainRun = new CSimulation(plain);
		plain.Replay = CReplay.Recording();
		plainRun.MarkSaved();
		Assert.Null(plain.ReplayLink);
		Assert.Empty(plain.Replay.Entries);
		plain.Replay = null;
		plainRun.MarkSaved();
		Assert.Null(plain.ReplayLink);
	}

	[Fact]
	public void APlaybackToAMarkEndsThereWithWhatWasOrderedBeforeTheSaveRunAndNothingAfter()
	{
		string folder = Folder();
		try
		{
			CGameState recorded = State();
			var recordedRun = new CSimulation(recorded);
			(CReplayFile file, CReplay recording) = CReplayStore.Begin(CReplayStore.Folder(folder),
				new CReplayHeader(Array.Empty<CCountryTag>(), recorded.Seed, "first"));
			recorded.Replay = recording;
			recorded.FogOfWar = true;
			recordedRun.RunHours(2);
			recordedRun.Post(new CFogOfWarCommand(false));
			recordedRun.Pump();
			recordedRun.MarkSaved();
			// After the save: an order and hours the game played on from the save never saw.
			recordedRun.Post(new CFogOfWarCommand(true));
			recordedRun.RunHours(3);
			file.Dispose();
			Assert.True(recorded.FogOfWar);

			CGameState replayed = State();
			replayed.FogOfWar = true;
			var replayedRun = new CSimulation(replayed);
			using CReplayFile read = CReplayFile.Open(CReplayStore.PathOf(CReplayStore.Folder(folder), "first"), replayed);
			replayed.Replay = CReplay.Replaying(read, untilMark: 1);
			Assert.Throws<CReplayEndedException>(() => replayedRun.RunHours(5));
			Assert.Equal(1, replayed.Replay.LastMark);
			Assert.Equal(recorded.TotalHours - 3, replayed.TotalHours);
			Assert.False(replayed.FogOfWar);
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Fact]
	public void ASaveWrittenFromAPlaybackNamesHowFarItHadPlayedAndAPlaybackToThatPlaceStandsWhereItStood()
	{
		string folder = Folder();
		string store = CReplayStore.Folder(folder);
		try
		{
			CGameState recorded = State();
			var recordedRun = new CSimulation(recorded);
			(CReplayFile file, CReplay recording) = CReplayStore.Begin(store,
				new CReplayHeader(Array.Empty<CCountryTag>(), recorded.Seed, "first"));
			recorded.Replay = recording;
			recorded.FogOfWar = true;
			recordedRun.RunHours(3);
			recordedRun.Post(new CFogOfWarCommand(false));
			recordedRun.RunHours(4);
			recordedRun.MarkSaved();
			file.Dispose();

			// Played back three hours and saved there: the place is the entries played, and no mark.
			CGameState watched = State();
			watched.FogOfWar = true;
			var watchedRun = new CSimulation(watched);
			CReplayLink? place;
			using (CReplayFile read = CReplayFile.Open(CReplayStore.PathOf(store, "first"), watched))
			{
				watched.Replay = CReplay.Replaying(read, new CReplayLink("first", 1));
				watchedRun.RunHours(3);
				watchedRun.MarkSaved();
				place = watched.ReplayLink;
			}
			Assert.NotNull(place);
			Assert.True(place!.ByEntries);
			Assert.Equal("first", place.Segment);
			Assert.Equal(watched.Replay!.Played, place.Entries);
			Assert.True(watched.FogOfWar);

			// A playback to that place ends there - before the order given after it - and stands at it.
			CGameState again = State();
			again.FogOfWar = true;
			var againRun = new CSimulation(again);
			using (CReplayFile read = CReplayFile.Open(CReplayStore.PathOf(store, "first"), again))
			{
				again.Replay = CReplay.Replaying(read, place);
				Assert.Throws<CReplayEndedException>(() => againRun.RunHours(7));
				Assert.True(again.Replay.StandsAt(place));
				Assert.Equal(place.Entries, again.Replay.Played);
			}
			Assert.Equal(watched.TotalHours, again.TotalHours);
			Assert.True(again.FogOfWar);

			// And a recording begun from it is found behind it in the chain.
			CReplayStore.Begin(store, new CReplayHeader(Array.Empty<CCountryTag>(), recorded.Seed, "on", place)).File.Dispose();
			List<CReplaySegment>? chain = CReplayStore.Chain(store, new CReplayLink("on", 1));
			Assert.Equal(new[] { place, new CReplayLink("on", 1) }, chain!.Select(segment => segment.Until));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Fact]
	public void TheRecordingsThatLeadToASaveAreFoundByTheirParentsTheNewGamesFirst()
	{
		string folder = Folder();
		string store = CReplayStore.Folder(folder);
		try
		{
			var nobody = Array.Empty<CCountryTag>();
			CReplayStore.Begin(store, new CReplayHeader(nobody, 5, "a", null, "engine 1")).File.Dispose();
			CReplayStore.Begin(store, new CReplayHeader(new[] { new CCountryTag("GER") }, 5, "b", new CReplayLink("a", 3), "engine 1")).File.Dispose();
			CReplayStore.Begin(store, new CReplayHeader(nobody, 5, "c", new CReplayLink("b", 1))).File.Dispose();
			// A second game played on from the same save of `a`: a fork, which the chain to `c` does not go through.
			CReplayStore.Begin(store, new CReplayHeader(nobody, 5, "d", new CReplayLink("a", 3))).File.Dispose();

			List<CReplaySegment>? chain = CReplayStore.Chain(store, new CReplayLink("c", 2));
			Assert.NotNull(chain);
			Assert.Equal(new[] { "a", "b", "c" }, chain!.Select(segment => segment.Header.Segment));
			Assert.Equal(new[] { 3, 1, 2 }, chain.Select(segment => segment.UntilMark));
			Assert.Equal("engine 1", chain[0].Header.Made);
			Assert.Equal(new CReplayLink("a", 3), chain[1].Header.Parent);
			Assert.Equal(new[] { new CCountryTag("GER") }, chain[1].Header.Played);

			// A recording that is not there, and recordings that lead round in a ring.
			var told = new List<string>();
			Assert.Null(CReplayStore.Chain(store, new CReplayLink("missing", 1), told.Add));
			CReplayStore.Begin(store, new CReplayHeader(nobody, 5, "x", new CReplayLink("y", 1))).File.Dispose();
			CReplayStore.Begin(store, new CReplayHeader(nobody, 5, "y", new CReplayLink("x", 1))).File.Dispose();
			Assert.Null(CReplayStore.Chain(store, new CReplayLink("x", 1), told.Add));
			Assert.Equal(2, told.Count);
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Fact]
	public void ARecordingKeepsTheLobbysChoicesAndANewGamesRecordingTheSaveItBeginsFrom()
	{
		string folder = Folder();
		string store = CReplayStore.Folder(folder);
		try
		{
			var options = new[] { 3, 0, 1, 0, 2, 0, 0, 1, 0, 1 };
			CReplayStore.Begin(store, new CReplayHeader(Array.Empty<CCountryTag>(), 9, "a", null, "engine", options)).File.Dispose();
			Assert.Equal(options, CReplayFile.ReadHeader(CReplayStore.PathOf(store, "a")).Options);
			CReplayStore.Begin(store, new CReplayHeader(Array.Empty<CCountryTag>(), 9, "b")).File.Dispose();
			Assert.Null(CReplayFile.ReadHeader(CReplayStore.PathOf(store, "b")).Options);

			byte[] save = System.Text.Encoding.Latin1.GetBytes("HoI3txt\ndate=\"1936.1.1.0\"\nplayer=\"GER\"\n" + new string('x', 5000));
			CReplayStore.WriteStart(store, "a", save);
			Assert.Equal(save, CReplayStore.ReadStart(store, "a"));
			Assert.True(new FileInfo(CReplayStore.StartPathOf(store, "a")).Length < save.Length);
			Assert.Null(CReplayStore.ReadStart(store, "b"));

			// A save's text names its place, or none.
			Assert.Equal(new CReplayLink("abc", 4), CReplayStore.LinkOf("date=\"1936.1.1.0\"\nopenhoi_replay=\"abc:4\"\nplayer=\"GER\"\n"));
			Assert.Null(CReplayStore.LinkOf("date=\"1936.1.1.0\"\nplayer=\"GER\"\n"));
			Assert.Null(CReplayStore.LinkOf("openhoi_replay=\"abc"));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Fact]
	public void WhetherASavesGameCanBePlayedBackIsFoundBeforeAnythingIsPlayed()
	{
		string folder = Folder();
		string store = CReplayStore.Folder(folder);
		try
		{
			var nobody = Array.Empty<CCountryTag>();
			CReplayStore.Begin(store, new CReplayHeader(nobody, 5, "a", null, "engine 1")).File.Dispose();
			CReplayStore.Begin(store, new CReplayHeader(nobody, 5, "b", new CReplayLink("a", 1), "engine 1")).File.Dispose();
			// Without the first recording's start nothing can be played.
			Assert.Null(CReplayStore.Probe(store, new CReplayLink("b", 1), "engine 1"));
			CReplayStore.WriteStart(store, "a", new byte[] { 1, 2, 3 });
			Assert.Equal(new CReplayProbe(2, null), CReplayStore.Probe(store, new CReplayLink("b", 1), "engine 1"));
			Assert.True(CReplayStore.Probe(store, new CReplayLink("b", 1), "engine 1")!.Fits);
			// Made with something else: found, and not to be played here.
			CReplayProbe? other = CReplayStore.Probe(store, new CReplayLink("b", 1), "engine 2");
			Assert.Equal(new CReplayProbe(2, "engine 1"), other);
			Assert.False(other!.Fits);
			Assert.Null(CReplayStore.Probe(store, new CReplayLink("missing", 1), "engine 1"));

			// What the lobby's button says of each, in the words of ours where no table has the keys.
			string fits = OpenHOI3.Interface.Screens.CReplayText.Tooltip(key => key, 2, null, "engine 1");
			Assert.Contains("2 recordings", fits);
			string not = OpenHOI3.Interface.Screens.CReplayText.Tooltip(key => key, 2, "engine 1", "engine 2");
			Assert.Contains("(engine 1)", not);
			Assert.Contains("(engine 2)", not);
			Assert.Equal("Seen: 3", OpenHOI3.Interface.Screens.CReplayText.Tooltip(
				key => key == OpenHOI3.Interface.Screens.CReplayText.WatchTipKey ? "Seen: $COUNT$" : key, 3, null, ""));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Fact]
	public void ARecordingBeingWrittenIsReadAsFarAsItsLastMark()
	{
		string folder = Folder();
		string store = CReplayStore.Folder(folder);
		try
		{
			CGameState recorded = State();
			var run = new CSimulation(recorded);
			(CReplayFile file, CReplay recording) = CReplayStore.Begin(store, new CReplayHeader(Array.Empty<CCountryTag>(), recorded.Seed, "open"));
			recorded.Replay = recording;
			run.RunHours(1);
			run.MarkSaved();
			run.RunHours(1);
			// The file is still open and its end not written: its head and everything to the mark are on disk.
			Assert.Equal("open", CReplayFile.ReadHeader(CReplayStore.PathOf(store, "open")).Segment);
			CGameState replayed = State();
			using (CReplayFile read = CReplayFile.Open(CReplayStore.PathOf(store, "open"), replayed))
			{
				replayed.Replay = CReplay.Replaying(read, untilMark: 1);
				Assert.Throws<CReplayEndedException>(() => new CSimulation(replayed).RunHours(3));
				Assert.Equal(1, replayed.Replay.LastMark);
			}
			file.Dispose();
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Fact]
	public void APlaceInARecordingIsOneWordAndARecordingsNameIsLettersAndDigits()
	{
		var link = new CReplayLink("0f3a9c", 12);
		Assert.Equal("0f3a9c:12", link.Text);
		Assert.Equal(link, CReplayLink.Parse(link.Text));
		Assert.Null(CReplayLink.Parse(null));
		Assert.Null(CReplayLink.Parse("0f3a9c"));
		Assert.Null(CReplayLink.Parse("0f3a9c:"));
		Assert.Null(CReplayLink.Parse("0f3a9c:-1"));
		Assert.Null(CReplayLink.Parse(":3"));
		// A place by the entries played, of a save written from a playback.
		var played = new CReplayLink("0f3a9c", 2, 1836);
		Assert.Equal("0f3a9c:2:1836", played.Text);
		Assert.Equal(played, CReplayLink.Parse(played.Text));
		Assert.True(played.ByEntries);
		Assert.False(link.ByEntries);
		Assert.Null(CReplayLink.Parse("0f3a9c:2:x"));
		Assert.Null(CReplayLink.Parse("0f3a9c:2:3:4"));
		// A name is a file's name: nothing that could lead out of the folder.
		Assert.Null(CReplayLink.Parse("../other:3"));
		Assert.Null(CReplayLink.Parse("a\\b:3"));
		Assert.Null(CReplayLink.Parse("a.b:3"));
		Assert.True(CReplayLink.IsName(CReplayStore.NewName()));
	}

	[Fact]
	public void AnEnhancedSaveHoldsItsPlaceInTheRecordingAndAFaithfulOneNothing()
	{
		CGameState faithful = State();
		faithful.ReplayLink = new CReplayLink("abc", 4);
		var writer = new CSaveTextWriter();
		CSaveTop.WriteRules(writer, faithful);
		Assert.Equal("", writer.Text);

		CGameState enhanced = State(enhanced: true);
		writer = new CSaveTextWriter();
		CSaveTop.WriteRules(writer, enhanced);
		Assert.DoesNotContain(CSaveTop.ReplayKey, writer.Text);
		enhanced.ReplayLink = new CReplayLink("abc", 4);
		writer = new CSaveTextWriter();
		CSaveTop.WriteRules(writer, enhanced);
		Assert.Contains("openhoi_replay=\"abc:4\"", writer.Text);

		CGameState loaded = State();
		var report = new CSaveReport();
		foreach (ClausewitzNode node in ClausewitzParser.Parse(writer.Text).Children)
		{
			Assert.True(CSaveTop.Read(loaded, node, report));
		}
		Assert.Equal(new CReplayLink("abc", 4), loaded.ReplayLink);
		Assert.Empty(report.UnreadKeys);

		// A place that is none is reported and left out.
		Assert.True(CSaveTop.Read(State(), ClausewitzParser.Parse("openhoi_replay=\"../x:1\"").Children[0], report));
		Assert.Single(report.UnreadKeys);
	}
}
