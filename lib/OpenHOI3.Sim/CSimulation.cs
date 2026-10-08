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

	public CSimulation(CGameState state)
	{
		State = state;
		CTick.RunEntryPass(state);
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

	/// <summary>Stamps a command with its place in the order and queues it.</summary>
	public void Post(CCommand command)
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
			Pump();                                          // orders given while paused still happen
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
		Post(new CHourlyTickCommand());
		Pump();
	}
}
