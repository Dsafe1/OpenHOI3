using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>A recording of the AI and its playback, the commands' written form, and the state's hash (docs/replay.md).</summary>
public class ReplayTests
{
	private static readonly CCountryTag Germany = new("GER");

	private static CGameState State()
	{
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) };
		return new CGameState(new CDate(1936, 1, 1), provinces, Array.Empty<CCountry>())
		{
			Regions = new[] { new CRegion("north_germany", new[] { 1 }, useForPeace: true) },
			CasusBelliTypes = new[] { CCasusBelliType.Null, new CCasusBelliType("conquer", 1) },
		};
	}

	/// <summary>A command the way a recording writes it.</summary>
	private static string Json(CCommand command)
	{
		using var buffer = new MemoryStream();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			CCommandCodec.Write(writer, command);
		}
		return Encoding.UTF8.GetString(buffer.ToArray());
	}

	private static CCommand ThereAndBack(CCommand command, CGameState state)
	{
		using JsonDocument document = JsonDocument.Parse(Json(command));
		return CCommandCodec.Read(document.RootElement, state);
	}

	/// <summary>
	/// An answer to an event names the event by its id and is read back holding the game's own event, so that it runs:
	/// read back as a copy it was dropped, and a player's answer never ran on the machine that followed.
	/// </summary>
	[Fact]
	public void AnAnswerToAnEventIsReadBackHoldingTheGamesOwnEvent()
	{
		CGameState state = State();
		var known = new OpenHOI3.Sim.Script.CEvent(isProvinceEvent: false, "events/test.txt", 1) { Id = 1002 };
		state.Events = new[] { new OpenHOI3.Sim.Script.CEvent(isProvinceEvent: false, "events/test.txt", 2) { Id = 7 }, known };
		var answer = new CSelectEventOptionCommand(known, OpenHOI3.Sim.Script.CEventScope.Start(Germany), 0);

		var back = (CSelectEventOptionCommand)ThereAndBack(answer, state);

		Assert.Same(known, back.Event);
		Assert.True(back.MayExecute(state));
		Assert.Equal(Germany, back.Scope.Country);
	}

	/// <summary>What each game's commands ran, in order, by the game they ran in.</summary>
	private readonly Dictionary<CGameState, List<string>> _ran = new();

	/// <summary>
	/// A player's command that tells it ran and, as a command may, has the game post another after it, which posts a third:
	/// the game's own commands, which a replay makes again.
	/// </summary>
	private sealed class Chain : CCommand
	{
		private readonly Dictionary<CGameState, List<string>> _ran;
		private readonly string _name;
		private readonly int _more;

		public Chain(Dictionary<CGameState, List<string>> ran, string name, int more)
		{
			_ran = ran;
			_name = name;
			_more = more;
		}

		public override void Execute(CGameState state)
		{
			if (!_ran.TryGetValue(state, out List<string>? ran))
			{
				_ran[state] = ran = new List<string>();
			}
			ran.Add(_name);
			if (_more > 0)
			{
				state.Outbox.Add(new Chain(_ran, _name + "'", _more - 1));
			}
		}
	}

	/// <summary>A command that posts nothing and holds a unit, which no recording can keep.</summary>
	private sealed class HoldsAUnit : CCommand
	{
		private readonly CUnit _unit = new(EUnitKind.Division, Germany);

		public override void Execute(CGameState state)
		{
		}
	}

	[Fact]
	public void CommandsOfEveryShapeComeBackAsTheyWere()
	{
		CGameState state = State();
		var commands = new CCommand[]
		{
			new CMoveUnitCommand(Germany, 12, 1, append: true, landSafety: OpenHOI3.Sim.Movement.ERouteSafety.Plain),
			new COfferCommand(new COffer(EOfferKind.Alliance, Germany, new CCountryTag("ITA"), EOfferStage.Proposed, true, 40)
			{
				WarGoal = new CWarGoal(state.CasusBelliTypes[1], Germany, new CCountryTag("POL"))
				{
					Region = state.Regions[0],
					Country = new CCountryTag("DAN"),
				},
				Versus = new CCountryTag("FRA"),
			}),
			new CExecuteDecisionCommand("anschluss", CEventScope.Start(Germany, 1, new CCountryTag("AUS"), 0, 77, 3)),
			new CConstructUnitCommand(Germany, new[] { "infantry_brigade", "artillery_brigade" }, 2, isReserve: true),
			new CSetPlanObjectivesCommand(Germany, 5, new[] { new CObjective(1, 3, Hold: true) }),
		};

		foreach (CCommand command in commands)
		{
			CCommand back = ThereAndBack(command, state);
			Assert.IsType(command.GetType(), back);
			Assert.Equal(Json(command), Json(back));
		}
	}

	[Fact]
	public void ARegionAndACasusBelliAreReadBackAsTheGamesOwn()
	{
		CGameState state = State();
		var offer = new COffer(EOfferKind.Alliance, Germany, new CCountryTag("POL"), EOfferStage.Proposed, true, 0)
		{
			WarGoal = new CWarGoal(state.CasusBelliTypes[1], Germany, new CCountryTag("POL")) { Region = state.Regions[0] },
		};

		var back = (COfferCommand)ThereAndBack(new COfferCommand(offer), state);

		Assert.Same(state.Regions[0], back.Offer.WarGoal!.Region);
		Assert.Same(state.CasusBelliTypes[1], back.Offer.WarGoal.Type);
	}

	[Fact]
	public void ACommandHoldingAPartOfTheGameCannotBeWritten()
	{
		Assert.Throws<NotSupportedException>(() => Json(new HoldsAUnit()));
	}

	[Fact]
	public void AReplayedStepPostsWhatTheRecordedOnePostedAndMovesTheGeneratorAndTheUnitNumbersOn()
	{
		CGameState recorded = State();
		CReplay recording = CReplay.Recording();
		recorded.Replay = recording;
		recording.RunStep(recorded, "agents", state =>
		{
			state.Random.Next();
			state.Random.Next();
			state.Random.Next();
			state.Outbox.Add(new CMoveUnitCommand(Germany, ++state.LastUnitId, 1));
		});

		CGameState replayed = State();
		replayed.Replay = CReplay.Replaying(recording.Entries);
		replayed.Replay.RunStep(replayed, "agents", _ => throw new InvalidOperationException("a replayed step does not run"));

		Assert.Single(replayed.Outbox);
		Assert.Equal(Json(recorded.Outbox[0]), Json(replayed.Outbox[0]));
		Assert.Equal(recorded.Random.Draws, replayed.Random.Draws);
		Assert.Equal(recorded.Random.Next(), replayed.Random.Next());
		Assert.Equal(recorded.LastUnitId, replayed.LastUnitId);
	}

	[Fact]
	public void AReplayOutOfStepWithItsRecordingStops()
	{
		CGameState recorded = State();
		CReplay recording = CReplay.Recording();
		recording.RunStep(recorded, "agents", _ => { });

		CGameState replayed = State();
		CReplay playback = CReplay.Replaying(recording.Entries);

		Assert.Throws<InvalidOperationException>(() => playback.RunStep(replayed, "trade", _ => { }));
	}

	[Fact]
	public void A_decision_the_game_calls_is_kept_where_it_posted_and_played_back_by_its_name()
	{
		// As a land unit enters a province the march asks the sealift what to do: a decision of the AI's, reading what only
		// its steps make. Kept where it posted, it comes back for the same name in the same hour; where it posted nothing,
		// nothing is kept and the replay passes it by - in memory and on disk alike (docs/replay.md, section 3).
		void Record(CReplay recording, CGameState state)
		{
			recording.RunCalledStep(state, "arrival #1", _ => { });
			recording.RunCalledStep(state, "arrival #2", s => s.Outbox.Add(new CMoveUnitCommand(Germany, 2, 5)));
			recording.RunStep(state, "agents", s => s.Outbox.Add(new CMoveUnitCommand(Germany, 7, 1)));
		}
		void Play(CReplay playback, CGameState state)
		{
			playback.RunCalledStep(state, "arrival #1", _ => throw new InvalidOperationException("a replayed decision does not run"));
			playback.RunCalledStep(state, "arrival #2", _ => throw new InvalidOperationException("a replayed decision does not run"));
			playback.RunStep(state, "agents", _ => throw new InvalidOperationException("a replayed step does not run"));
		}

		CGameState recorded = State();
		CReplay recording = CReplay.Recording();
		Record(recording, recorded);
		Assert.Equal(2, recording.Entries.Count);

		CGameState replayed = State();
		Play(CReplay.Replaying(recording.Entries), replayed);
		Assert.Equal(recorded.Outbox.Select(Json), replayed.Outbox.Select(Json));

		string path = Path.Combine(Path.GetTempPath(), $"openhoi3-replay-{Guid.NewGuid():N}.gz");
		try
		{
			CGameState onDisk = State();
			using (CReplayFile file = CReplayFile.Create(path, new CReplayHeader(Array.Empty<CCountryTag>(), onDisk.Seed)))
			{
				Record(CReplay.Recording(file.Write), onDisk);
			}
			CGameState fromDisk = State();
			using (CReplayFile file = CReplayFile.Open(path, fromDisk))
			{
				Play(CReplay.Replaying(file), fromDisk);
			}
			Assert.Equal(onDisk.Outbox.Select(Json), fromDisk.Outbox.Select(Json));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void ARecordingOnDiskPlaysBackStepByStep()
	{
		string path = Path.Combine(Path.GetTempPath(), $"openhoi3-replay-{Guid.NewGuid():N}.gz");
		try
		{
			CGameState recorded = State();
			using (CReplayFile file = CReplayFile.Create(path, new CReplayHeader(Array.Empty<CCountryTag>(), recorded.Seed)))
			{
				CReplay recording = CReplay.Recording(file.Write);
				recording.RunStep(recorded, "agents", state => state.Outbox.Add(new CMoveUnitCommand(Germany, 7, 1)));
				recording.RunStep(recorded, "trade", state => state.Random.Next());
			}

			CGameState replayed = State();
			using (CReplayFile file = CReplayFile.Open(path, replayed))
			{
				CReplay playback = CReplay.Replaying(file);
				playback.RunStep(replayed, "agents", _ => throw new InvalidOperationException());
				playback.RunStep(replayed, "trade", _ => throw new InvalidOperationException());
			}

			Assert.Equal(Json(recorded.Outbox[0]), Json(replayed.Outbox[0]));
			Assert.Equal(1, replayed.Random.Draws);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void APlayersCommandsRunInAReplayWhereTheyRanAndWhatTheyPostedAfterThem()
	{
		CGameState recorded = State();
		var recordedRun = new CSimulation(recorded);
		CReplay recording = CReplay.Recording();
		recorded.Replay = recording;
		// Posted while paused: drained once, which runs it and queues what it posts, and once more before the next is
		// posted, which runs that and queues a third; then an hour, whose drain runs the third, the second order and the tick.
		recordedRun.Post(new Chain(_ran, "a", 2));
		recordedRun.Pump();
		recordedRun.Pump();
		recordedRun.Post(new Chain(_ran, "b", 0));
		recordedRun.RunHours(2);

		CGameState replayed = State();
		var replayedRun = new CSimulation(replayed);
		replayed.Replay = CReplay.Replaying(recording.Entries);
		replayedRun.RunHours(2);

		Assert.Equal(new[] { "a", "a'", "a''", "b" }, _ran[recorded]);
		Assert.Equal(_ran[recorded], _ran[replayed]);
	}

	[Fact]
	public void AReplayTakesNoCommandFromOutside()
	{
		CGameState replayed = State();
		var run = new CSimulation(replayed);
		replayed.Replay = CReplay.Replaying(Array.Empty<CReplayEntry>());

		Assert.False(run.TakesCommands);
		Assert.False(run.Post(new Chain(_ran, "a", 0)));
	}

	[Fact]
	public void ARecordingOfAGameSomebodyPlayedDoesNotFitOneNobodyPlays()
	{
		string path = Path.Combine(Path.GetTempPath(), $"openhoi3-replay-{Guid.NewGuid():N}.gz");
		try
		{
			CGameState game = State();
			using (CReplayFile.Create(path, new CReplayHeader(new[] { new CCountryTag("IRE") }, game.Seed)))
			{
			}

			Assert.Equal(new[] { new CCountryTag("IRE") }, CReplayFile.ReadHeader(path).Played);
			Assert.Throws<InvalidDataException>(() => CReplayFile.Open(path, game));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void TwoLikeGamesHashAlikeAndAChangeShowsInItsPart()
	{
		CGameState first = State();
		CGameState second = State();
		Assert.Equal(CStateHash.Of(first), CStateHash.Of(second));

		second.Provinces[1].Controller = Germany;
		ulong[] a = CStateHash.Of(first);
		ulong[] b = CStateHash.Of(second);

		int provinces = Array.IndexOf(CStateHash.Parts, "provinces");
		for (int i = 0; i < a.Length; i++)
		{
			Assert.Equal(i == provinces, a[i] != b[i]);
		}
	}
}
