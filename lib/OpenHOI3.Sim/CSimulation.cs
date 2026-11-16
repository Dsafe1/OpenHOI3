using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenHOI3.Sim.Commands;

namespace OpenHOI3.Sim;

/// <summary>
/// A game being played: its state, the queue every change to it goes through, and the
/// clock that decides when the next hour is due.
///
/// Nothing here is static, and nothing here knows about a screen. A test runs twelve
/// years of it in a loop; the client feeds it each frame's length and listens for the
/// date changing. See <c>docs/mechanics/tick.md</c>.
/// </summary>
public sealed class CSimulation
{
	/// <summary>At the top speed, how much of a frame may be spent running hours.</summary>
	private static readonly TimeSpan FrameBudget = TimeSpan.FromMilliseconds(12);

	private readonly Queue<CCommand> _queue = new();
	private long _nextSerial;
	private int _turn;

	/// <param name="state">The game.</param>
	/// <param name="discardEntryOrders">For a tool setting an hour beside a save the original wrote after its own entering:
	/// the AI's first pass still makes what it makes, and its orders, which that save already holds, are thrown away.</param>
	public CSimulation(CGameState state, bool discardEntryOrders = false)
	{
		State = state;
		CTick.RunEntryPass(state);
		if (discardEntryOrders)
		{
			state.Outbox.Clear();
		}
		// What entering posted - the AI's sort of its units into its theatres - runs at once.
		DrainOutbox();
		// A loaded game runs a second AI pass before its first hour, and that one's orders run as well.
		if (state.LoadedFromSave)
		{
			CTick.RunSecondOfLoad(state);
			if (discardEntryOrders)
			{
				state.Outbox.Clear();
			}
			DrainOutbox();
		}
	}

	private void DrainOutbox()
	{
		if (State.Outbox.Count > 0)
		{
			foreach (CCommand posted in State.Outbox)
			{
				Enqueue(posted);
			}
			State.Outbox.Clear();
			Pump();
		}
	}

	public CGameState State { get; }

	public CClock Clock { get; } = new();

	/// <summary>How many times the queue has been drained.</summary>
	public int Turn => _turn;

	/// <summary>
	/// Raised after a command has moved the clock, for whatever shows the game - never
	/// for anything that changes it. Day, month and year are raised in that order on
	/// the hours that turn them, after <see cref="HourPassed"/>.
	/// </summary>
	public event Action? HourPassed;

	public event Action? DayPassed;

	public event Action? MonthPassed;

	public event Action? YearPassed;

	/// <summary>
	/// A command from outside the game - a player's order, a tool's - stamped with its place in the order and queued, and
	/// kept where the game is being recorded. A game replaying a recording takes none: its players' orders come from the
	/// recording, and another would put it where the recording does not fit (<c>docs/replay.md</c>, section 8). Answers
	/// whether it was taken.
	/// </summary>
	public bool Post(CCommand command)
	{
		if (State.Replay is { IsReplaying: true })
		{
			return false;
		}
		State.Replay?.Posted(_turn, command);
		Enqueue(command);
		return true;
	}

	/// <summary>Whether commands from outside are taken: not while a recording is played back.</summary>
	public bool TakesCommands => State.Replay is not { IsReplaying: true };

	/// <summary>A recorded player's command, posted where it was: for <see cref="Replay.CReplay"/> alone.</summary>
	internal void PostReplayed(CCommand command)
	{
		Enqueue(command);
	}

	/// <summary>
	/// The queue drained until it has been drained that many times: what a replay does before a recorded command or an
	/// hour, so that each runs after the drains it ran after. More drains than that already means the recording does not
	/// fit.
	/// </summary>
	internal void DrainUntil(int turn)
	{
		if (_turn > turn)
		{
			throw new InvalidOperationException(
				$"the recording has the queue drained {turn} times where it has been {_turn}: it was made with other rules or another game");
		}
		while (_turn < turn)
		{
			Pump();
		}
	}

	/// <summary>Stamps a command with its place in the order and queues it.</summary>
	private void Enqueue(CCommand command)
	{
		command.Serial = _nextSerial++;
		command.Turn = _turn;
		_queue.Enqueue(command);
	}

	/// <summary>
	/// Executes what is queued, in the order it was posted, and returns how many
	/// commands ran. A command posted while this is running waits for the next call,
	/// which is what makes a drain a turn.
	/// </summary>
	public int Pump()
	{
		// The turn moves on as the drain begins, so a command posted from inside one
		// carries the next turn's number - it will run in the next drain.
		_turn++;
		int executed = 0;
		for (int waiting = _queue.Count; waiting > 0; waiting--)
		{
			CCommand command = _queue.Dequeue();
			if (!command.MayExecute(State))
			{
				continue;
			}

			long hoursBefore = State.TotalHours;
			CDate before = State.Date;
			command.Execute(State);
			executed++;

			// What the game posted while the command ran waits for the next turn.
			foreach (CCommand posted in State.Outbox)
			{
				Enqueue(posted);
			}
			State.Outbox.Clear();

			if (State.TotalHours != hoursBefore)
			{
				RaiseTimeEvents(before, State.Date);
			}
		}
		return executed;
	}

	private void RaiseTimeEvents(CDate before, CDate now)
	{
		HourPassed?.Invoke();
		if (now != before)
		{
			DayPassed?.Invoke();
		}
		if (now.Month != before.Month || now.Year != before.Year)
		{
			MonthPassed?.Invoke();
		}
		if (now.Year != before.Year)
		{
			YearPassed?.Invoke();
		}
	}

	/// <summary>
	/// Called once a frame with the frame's length. Runs the hours the clock says have
	/// come due, and everything else waiting in the queue, and returns the hours run.
	/// </summary>
	public int Advance(double realSeconds)
	{
		int due = Clock.HoursDue(realSeconds);
		if (due == 0)
		{
			// Orders given while paused still happen; a replay drains the queue only as its recording did, before each hour.
			if (State.Replay is not { IsReplaying: true })
			{
				Pump();
			}
			return 0;
		}

		var spent = Stopwatch.StartNew();
		int run = 0;
		while (run < due && (run == 0 || spent.Elapsed < FrameBudget))
		{
			RunOneHour();
			run++;
		}
		return run;
	}

	/// <summary>Runs hours with no regard to real time or to the pause: for tests and tools.</summary>
	public void RunHours(int hours)
	{
		for (int i = 0; i < hours; i++)
		{
			RunOneHour();
		}
	}

	private void RunOneHour()
	{
		// A recording keeps how often the queue had been drained when the hour began; a replay first posts the players'
		// commands that came before it and drains as far (docs/replay.md, section 8).
		if (State.Replay is { } replay)
		{
			if (replay.IsReplaying)
			{
				replay.BeforeHour(this);
			}
			else
			{
				replay.Ticked(_turn);
			}
		}
		Enqueue(new CHourlyTickCommand());
		Pump();
	}
}
