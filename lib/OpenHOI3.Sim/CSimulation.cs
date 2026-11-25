using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
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
	private readonly Queue<CCommand> _queue = new();
	private long _nextSerial;
	private int _turn;

	/// <summary>The hours being run beside the frame (<see cref="BeginAdvance"/>), until <see cref="Collect"/> takes them.</summary>
	private Task<int>? _running;

	/// <summary>The thread running them, which may drain the queue itself; nought when none runs.</summary>
	private int _runningThread;

	/// <summary>Commands posted from outside while the hours ran, queued in their order when they are collected.</summary>
	private readonly List<CCommand> _held = new();

	/// <summary>What was to be told while the hours ran - the clock's events and the state's - told in order when collected.</summary>
	private readonly List<Action> _deferred = new();

	/// <param name="state">The game.</param>
	/// <param name="discardEntryOrders">For a tool setting an hour beside a save the original wrote after its own entering:
	/// the AI's first pass still makes what it makes, and its orders, which that save already holds, are thrown away.</param>
	public CSimulation(CGameState state, bool discardEntryOrders = false)
	{
		State = state;
		state.Executing(begins: true);
		try
		{
			CTick.RunFirstFrame(state);
			if (discardEntryOrders)
			{
				state.Outbox.Clear();
			}
			// What entering posted - the AI's sort of its units into its theatres, and what a minister whose hour this
			// is gave - runs at once.
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
		finally
		{
			state.Executing(begins: false);
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
			PumpCore();
		}
	}

	public CGameState State { get; }

	public CClock Clock { get; } = new();

	/// <summary>
	/// The one way a game in play has its clock stopped, started or set to a speed (<see cref="CPace"/>): what asks
	/// goes through it and writes <see cref="Clock"/> itself nowhere.
	/// </summary>
	public CPace Pace => _pace ??= new CPace(Clock);

	private CPace? _pace;

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
			// A game following another's - a client's - hands its player's order to the game it follows, and runs it
			// when it comes back in that game's order (docs/multiplayer.md, section 2).
			return ToHost?.Invoke(command) ?? false;
		}
		// While hours run beside the frame the queue is theirs: the command waits, and is posted as they are collected.
		if (_beside)
		{
			_held.Add(command);
			return true;
		}
		State.Replay?.Posted(_turn, command);
		Enqueue(command);
		return true;
	}

	/// <summary>
	/// Asked when the clock says an hour is due: false holds the hour back, and the frame does what a frame with no hour
	/// does. A host's waiting for a machine that is behind (<c>docs/multiplayer.md</c>, section 2); null lets every hour run.
	/// </summary>
	public Func<bool>? HourGate { get; set; }

	/// <summary>
	/// Where a game following another's sends what its own player orders: to the game it follows, which posts it and
	/// sends it back in its order with everything else. Null for a recording played back, which takes no order.
	/// </summary>
	public Func<CCommand, bool>? ToHost { get; set; }

	/// <summary>
	/// For a game following another's as it runs, between hours: runs what has come of the orders given while the clock
	/// stood (<see cref="Replay.CReplay.FollowBetweenHours"/>). Answers whether anything came.
	/// </summary>
	public bool Follow()
	{
		return State.Replay is { IsReplaying: true } replay && replay.FollowBetweenHours(this);
	}

	/// <summary>For a game following another's: whether the hour next to run has come from the game it follows.</summary>
	public bool HourHasCome => State.Replay is { IsReplaying: true, HasHourNext: true };

	/// <summary>
	/// The queue drained with no hour begun. Where that ran anything in a game being recorded, the recording is told how
	/// far the queue has been drained, so that a game following along runs it now and not at the next hour.
	/// </summary>
	private int IdlePump()
	{
		int executed = PumpCore();
		if (executed > 0 && State.Replay is { IsReplaying: false } recording)
		{
			recording.Drained(_turn);
		}
		return executed;
	}

	/// <summary>Whether hours are being run beside the frame: nothing outside may read or change the state until collected.</summary>
	public bool IsBusy => _beside;

	/// <summary>How long the last hours run beside the frame took, on their thread: what a frame's log counts as the game's.</summary>
	public double LastRunMilliseconds { get; private set; }

	/// <summary>
	/// Set before the hours beside the frame start and cleared as they are collected, before what they told is told: what
	/// the worker raises before the task is even stored is deferred, and what is told after them happens at once.
	/// </summary>
	private volatile bool _beside;

	/// <summary>
	/// How long the hours of the top speed run one after another before the frame takes them, in seconds of real time.
	/// Ours: the original runs an hour a frame, its hour on the frame's own thread (<c>docs/mechanics/tick.md</c>, section 2).
	/// </summary>
	public const double BackToBackSeconds = 0.05;

	/// <summary>
	/// Set as an hour beside the frame tells what shows the game something that may stop the clock - a message whose type
	/// pauses (<see cref="OnMain"/>) - after which the hours stop for the frame to take.
	/// </summary>
	private volatile bool _toldBeside;

	/// <summary>
	/// For a client running hours beside its frames, once a frame while nothing runs, with the real time since it last
	/// started one: the hour the clock says is due started on a worker, or, with none due, what is queued run here.
	/// <see cref="Collect"/> takes it when it is done. At the top speed the worker runs the hours after it too, one after
	/// another for <see cref="BackToBackSeconds"/>, where the original runs one a frame - stopping after an hour that tells
	/// what may stop the clock, turns a day or ends the game, so that what the frame does with those hours it does at the
	/// hour it would have: a message's pause, the autosave of a new day, the end (<c>docs/mechanics/tick.md</c>, section 2).
	/// </summary>
	public void BeginAdvance(double realSeconds)
	{
		if (_running is not null)
		{
			throw new InvalidOperationException("an hour is already running beside the frame");
		}
		if (!Clock.HourDue(realSeconds) || HourGate?.Invoke() == false)
		{
			if (State.Replay is not { IsReplaying: true })
			{
				IdlePump();
			}
			return;
		}
		_beside = true;
		_toldBeside = false;
		bool backToBack = Clock.IsTopSpeed;
		_running = Task.Run(() =>
		{
			_runningThread = Environment.CurrentManagedThreadId;
			var spent = Stopwatch.StartNew();
			int hours = 0;
			while (true)
			{
				CDate before = State.Date;
				RunOneHour();
				hours++;
				if (!backToBack || _toldBeside || State.Date != before || IsOver()
					|| spent.Elapsed.TotalSeconds >= BackToBackSeconds)
				{
					break;
				}
			}
			LastRunMilliseconds = spent.Elapsed.TotalMilliseconds;
			return hours;
		});
	}

	/// <summary>
	/// For a game following another's as it runs - a client's: the hours that have come run beside the frame as a game
	/// of one's are, <paramref name="hours"/> running them through <see cref="RunHours"/> on the worker and answering
	/// how many it ran. <see cref="Collect"/> takes them. Until then nothing outside reads the game, and nothing that
	/// <paramref name="hours"/> reads - the lines that came - may be changed.
	/// </summary>
	public void BeginFollowing(Func<int> hours)
	{
		if (_running is not null)
		{
			throw new InvalidOperationException("an hour is already running beside the frame");
		}
		_beside = true;
		_toldBeside = false;
		_running = Task.Run(() =>
		{
			_runningThread = Environment.CurrentManagedThreadId;
			var spent = Stopwatch.StartNew();
			int ran = hours();
			LastRunMilliseconds = spent.Elapsed.TotalMilliseconds;
			return ran;
		});
	}

	/// <summary>Whether the game has been won or has passed its end date - strictly after it, by the hour.</summary>
	private bool IsOver()
	{
		return State.GameFinished || State.Date > State.EndDate || (State.Date == State.EndDate && State.Hour > 0);
	}

	/// <summary>
	/// Once a frame: where the hours started beside the frame are done, what was told while they ran told in its order and
	/// the commands posted meanwhile queued in theirs; the hours run, nought where none were or they still run. What the
	/// hours threw is thrown here.
	/// </summary>
	public int Collect()
	{
		if (_running is not { IsCompleted: true } running)
		{
			return 0;
		}
		_running = null;
		_runningThread = 0;
		_beside = false;
		try
		{
			return running.GetAwaiter().GetResult();
		}
		finally
		{
			List<Action> told;
			lock (_deferred)
			{
				told = new List<Action>(_deferred);
				_deferred.Clear();
			}
			foreach (Action tell in told)
			{
				tell();
			}
			List<CCommand> held = new(_held);
			_held.Clear();
			foreach (CCommand command in held)
			{
				Post(command);
			}
		}
	}

	/// <summary>Waits for hours running beside the frame and collects them: before anything that must have the state to itself.</summary>
	public int Finish()
	{
		if (_running is not { } running)
		{
			return 0;
		}
		try
		{
			running.Wait();
		}
		catch (AggregateException)
		{
			// Thrown again, unwrapped, by the collecting.
		}
		return Collect();
	}

	/// <summary>
	/// Runs what is to be told of the game now - or, while hours run beside the frame, once they are collected, in the
	/// order it was told: what listens for the state's events from a screen wraps its handler in this. **A handler that may
	/// stop the clock says so** with <paramref name="mayStopTheClock"/> - a message whose type pauses the game - and the
	/// hours of the top speed stop after the hour that told it, so the clock stops where it would have; one that does not
	/// lets them run on (<see cref="BeginAdvance"/>).
	/// </summary>
	public void OnMain(Action tell, bool mayStopTheClock = false)
	{
		if (_beside && mayStopTheClock)
		{
			_toldBeside = true;
		}
		Defer(tell);
	}

	/// <summary>What is told now, or once the hours beside the frame are collected - the time among it, which stops nothing.</summary>
	private void Defer(Action tell)
	{
		if (_beside)
		{
			lock (_deferred)
			{
				_deferred.Add(tell);
			}
			return;
		}
		// Told from inside a running command, a screen still only looks (CGameState.LooksOnly).
		State.Telling(begins: true);
		try
		{
			tell();
		}
		finally
		{
			State.Telling(begins: false);
		}
	}

	/// <summary>
	/// Whether commands from outside are taken: not while a recording is played back - but by a game following another's
	/// as it runs, a client's, whose player's orders go to the game it follows (<see cref="ToHost"/>).
	/// </summary>
	public bool TakesCommands => State.Replay is not { IsReplaying: true } || ToHost is not null;

	/// <summary>
	/// To be called as the game is about to be written as a save: where the game is recorded beside its saves, the place
	/// is marked in the recording and kept on the game for the save to hold (<c>docs/replay.md</c>, section 10).
	/// </summary>
	public void MarkSaved()
	{
		if (State.Replay is { IsReplaying: false, Segment: not null } recording)
		{
			State.ReplayLink = recording.Saved(_turn);
		}
		else if (State.Replay is { IsReplaying: true, Segment: not null } playback)
		{
			// A save of a game being played back names where the playback stands, so that the game played on from it
			// is still the game that was recorded, gone another way from there (docs/replay.md, section 13).
			State.ReplayLink = playback.Place();
		}
	}

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
			PumpCore();
		}
	}

	/// <summary>
	/// Stamps a command with its place in the order and queues it - unless its post throws it away. What the hour posts is
	/// queued as soon as the hour has run, before anything posted with it runs, so the test reads the game as the post did.
	/// </summary>
	private void Enqueue(CCommand command)
	{
		if (!command.MayPost(State))
		{
			return;
		}
		command.Serial = _nextSerial++;
		command.Turn = _turn;
		_queue.Enqueue(command);
	}

	/// <summary>
	/// Executes what is queued, in the order it was posted, and returns how many
	/// commands ran. A command posted while this is running waits for the next call,
	/// which is what makes a drain a turn.
	/// </summary>
	/// <summary>
	/// How many commands have run since the game was made: a figure a screen can watch to show the game as a command left
	/// it at once - a unit renamed or reorganised while paused - where it would otherwise wait for the next hour.
	/// </summary>
	public long Executed { get; private set; }

	public int Pump()
	{
		// From outside, hours running beside the frame are let finish first; the hours drain the queue themselves.
		if (_running is not null && Environment.CurrentManagedThreadId != _runningThread)
		{
			Finish();
		}
		return IdlePump();
	}

	private int PumpCore()
	{
		// The turn moves on as the drain begins, so a command posted from inside one
		// carries the next turn's number - it will run in the next drain.
		_turn++;
		int executed = 0;
		// Only what runs from here to the end may make what the game keeps (CGameState.ScreensLook).
		State.Executing(begins: true);
		try
		{
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
				Executed++;

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
		}
		finally
		{
			State.Executing(begins: false);
		}
		return executed;
	}

	private void RaiseTimeEvents(CDate before, CDate now)
	{
		// What shows the game hears of it on its own thread, after hours run beside the frame.
		Defer(() => TellTime(before, now));
	}

	private void TellTime(CDate before, CDate now)
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
	/// Called once a frame with the frame's length. Runs the hour the clock says has come
	/// due - at most one a frame, as the original's - and everything else waiting in the
	/// queue, and returns the hours run.
	/// </summary>
	public int Advance(double realSeconds)
	{
		if (!Clock.HourDue(realSeconds) || HourGate?.Invoke() == false)
		{
			// Orders given while paused still happen; a replay drains the queue only as its recording did, before each hour.
			if (State.Replay is not { IsReplaying: true })
			{
				IdlePump();
			}
			return 0;
		}

		RunOneHour();
		return 1;
	}

	/// <summary>Runs hours with no regard to real time or to the pause: for tests and tools.</summary>
	public void RunHours(int hours)
	{
		// From the thread that runs hours beside the frame they are these hours themselves (BeginFollowing).
		if (Environment.CurrentManagedThreadId != _runningThread)
		{
			Finish();
		}
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
		PumpCore();
	}
}
