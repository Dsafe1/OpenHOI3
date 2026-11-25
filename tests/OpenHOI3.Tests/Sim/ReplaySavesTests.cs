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
