using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Commands;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// A game's AI and players recorded, or played back in their place. Recording, each of the AI's steps in the hour runs as
/// ever and what it posted, drew and numbered is kept, and so is every command posted from outside the game - a player's -
/// with the drain of the queue it came after, and every hour's beginning; replaying, the AI's steps do not run and what
/// was kept is put in their place, and the players' commands are posted where they were. What the game posts itself comes
/// out again by itself. A recording holds only while the rules are the same. Ours; see <c>docs/replay.md</c>.
/// </summary>
public sealed class CReplay
{
	private readonly List<CReplayEntry> _entries = new();
	private readonly Action<CReplayEntry>? _sink;
	private readonly Func<int, CReplayEntry?>? _source;
	private int _next;

	/// <summary>The next entry, read and not yet taken: a file is read once an entry, so a look ahead is kept here.</summary>
	private CReplayEntry? _peeked;

	private CReplay(Action<CReplayEntry>? sink, Func<int, CReplayEntry?>? source)
	{
		_sink = sink;
		_source = source;
	}

	/// <summary>A recording, handed to <paramref name="sink"/> - a file - entry by entry as it is made, or kept in memory.</summary>
	public static CReplay Recording(Action<CReplayEntry>? sink = null)
	{
		return new CReplay(sink, null);
	}

	/// <summary>A playback of what a recording kept.</summary>
	public static CReplay Replaying(IReadOnlyList<CReplayEntry> entries)
	{
		return new CReplay(null, index => index < entries.Count ? entries[index] : null);
	}

	/// <summary>
	/// A playback that reads a recording as it is being made, each entry through <paramref name="through"/> where given:
	/// for a check that runs the recorded game and its replay hour by hour side by side.
	/// </summary>
	public static CReplay Following(CReplay recording, Func<CReplayEntry, CReplayEntry>? through = null)
	{
		return new CReplay(null, index => index < recording._entries.Count
			? through is null ? recording._entries[index] : through(recording._entries[index])
			: null);
	}

	/// <summary>
	/// A playback of entries as they come from elsewhere - another machine: <paramref name="next"/> answers the next one,
	/// or null while none has come, and is asked again.
	/// </summary>
	public static CReplay Following(Func<CReplayEntry?> next)
	{
		return new CReplay(null, _ => next());
	}

	/// <summary>A playback of a recording on disk, read an entry at a time.</summary>
	public static CReplay Replaying(CReplayFile file)
	{
		return new CReplay(null, _ => file.Read());
	}

	public bool IsReplaying => _source is not null;

	/// <summary>What a recording kept in memory, in the order it happened.</summary>
	public IReadOnlyList<CReplayEntry> Entries => _entries;

	/// <summary>The AI's steps of <see cref="Entries"/>.</summary>
	public IEnumerable<CReplayStep> Steps => _entries.OfType<CReplayStep>();

	/// <summary>
	/// Runs one of the AI's steps, or plays it back. Recording, the commands it added to the outbox, the draws it took and
	/// the unit numbers are kept; replaying, the step's kept commands go to the outbox, and the generator and the unit
	/// numbers are moved on as far.
	/// </summary>
	public void RunStep(CGameState state, string name, Action<CGameState> step)
	{
		if (IsReplaying)
		{
			CReplayEntry next = Take(state);
			if (next is not CReplayStep kept || kept.Hour != state.TotalHours || kept.Step != name)
			{
				string found = next is CReplayStep other ? $"hour {other.Hour}'s {other.Step}" : next.GetType().Name;
				throw new InvalidOperationException($"the recording does not have hour {state.TotalHours}'s {name} next but " +
					$"{found}: it was made with other rules or another game");
			}
			PlayBack(state, kept);
			return;
		}

		int posted = state.Outbox.Count;
		long draws = state.Random.Draws;
		long aiDraws = state.AiRandom.Draws;
		int unitIds = state.LastUnitId;
		long positions = state.PositionsMade;
		long networks = state.SupplyNetworksMade;
		List<CCommand>? outer = state.DoneInStep;
		var done = new List<CCommand>();
		state.DoneInStep = done;
		ulong[] marks = CAiMarks.Of(state);
		try
		{
			step(state);
		}
		finally
		{
			state.DoneInStep = outer;
		}
		// What of the AI's own the game's hour reads, kept where the step changed it (CAiMarks).
		CAiMarks.KeepChanged(state, marks, done);
		if (state.Random.Draws < draws || state.AiRandom.Draws < aiDraws)
		{
			throw new InvalidOperationException($"{name} seeded a generator again, which a recording cannot play back");
		}
		var commands = new List<CCommand>(state.Outbox.Count - posted);
		for (int i = posted; i < state.Outbox.Count; i++)
		{
			commands.Add(state.Outbox[i]);
		}
		Keep(new CReplayStep(state.TotalHours, name, commands, state.Random.Draws - draws, state.LastUnitId - unitIds,
			state.AiRandom.Draws - aiDraws, state.PositionsMade != positions, state.SupplyNetworksMade != networks, done));
	}

	/// <summary>
	/// A kept step done in a step's place: its commands to the outbox, the generators and the unit numbers moved on as
	/// far - and what the game makes for whoever asks first made here, where the step's asking made it.
	/// </summary>
	private static void PlayBack(CGameState state, CReplayStep kept)
	{
		if (kept.MadeSupplyNetwork)
		{
			Supply.CSupplyNetworks.Refresh(state);
		}
		if (kept.MadePositions)
		{
			_ = state.Positions;
		}
		// What the step did to the game itself and at once, done here in its order (CGameState.DoInStep).
		foreach (CCommand did in kept.Done)
		{
			did.Execute(state);
		}
		state.Outbox.AddRange(kept.Commands);
		state.Random.Skip(kept.Draws);
		state.AiRandom.Skip(kept.AiDraws);
		state.LastUnitId += kept.UnitIds;
	}

	/// <summary>
	/// Runs a decision of the AI's that a pass of the game itself calls - as a land unit enters a province in the march -
	/// or plays it back. Such a decision reads the AI's own state, which a replay does not make, so it is kept like a step:
	/// recording, only where it posted, drew or numbered anything, under its name, which says whose it was; replaying, the
	/// kept entry is taken where the next one is this hour's of that name, and nothing is done where it is not - the
	/// decision having done nothing when recorded. The game's pass calls it in the same order both times, so the entries
	/// come in that order (<c>docs/replay.md</c>, section 3).
	/// </summary>
	public void RunCalledStep(CGameState state, string name, Action<CGameState> step)
	{
		if (IsReplaying)
		{
			if (Peek() is CReplayStep kept && kept.Hour == state.TotalHours && kept.Step == name)
			{
				Take(state);
				PlayBack(state, kept);
			}
			return;
		}

		int posted = state.Outbox.Count;
		long draws = state.Random.Draws;
		long aiDraws = state.AiRandom.Draws;
		int unitIds = state.LastUnitId;
		long positions = state.PositionsMade;
		long networks = state.SupplyNetworksMade;
		List<CCommand>? outer = state.DoneInStep;
		var done = new List<CCommand>();
		state.DoneInStep = done;
		ulong[] marks = CAiMarks.Of(state);
		try
		{
			step(state);
		}
		finally
		{
			state.DoneInStep = outer;
		}
		// What of the AI's own the game's hour reads, kept where the step changed it (CAiMarks).
		CAiMarks.KeepChanged(state, marks, done);
		if (state.Outbox.Count == posted && state.Random.Draws == draws && state.AiRandom.Draws == aiDraws
			&& state.LastUnitId == unitIds && state.PositionsMade == positions && state.SupplyNetworksMade == networks
			&& done.Count == 0)
		{
			return;
		}
		var commands = new List<CCommand>(state.Outbox.Count - posted);
		for (int i = posted; i < state.Outbox.Count; i++)
		{
			commands.Add(state.Outbox[i]);
		}
		Keep(new CReplayStep(state.TotalHours, name, commands, state.Random.Draws - draws, state.LastUnitId - unitIds,
			state.AiRandom.Draws - aiDraws, state.PositionsMade != positions, state.SupplyNetworksMade != networks, done));
	}

	/// <summary>Recording: a command posted from outside the game, after that many drains of the queue.</summary>
	public void Posted(int turn, CCommand command)
	{
		Keep(new CReplayPost(turn, command));
	}

	/// <summary>Recording: an hour's tick posted, after that many drains of the queue.</summary>
	public void Ticked(int turn)
	{
		Keep(new CReplayTick(turn));
	}

	/// <summary>Recording: the queue drained with no hour begun, and something ran in the drain (<see cref="CReplayDrain"/>).</summary>
	public void Drained(int turn)
	{
		Keep(new CReplayDrain(turn));
	}

	/// <summary>
	/// Following a game as it runs, between hours: what has come of the orders given while its clock stood - each posted
	/// after as many drains as it was, and the queue drained as far as the game it follows drained it - as far as the
	/// recording has got and no hour begins. Answers whether anything came.
	/// </summary>
	public bool FollowBetweenHours(CSimulation simulation)
	{
		bool any = false;
		while (true)
		{
			switch (Peek())
			{
				case CReplayPost post:
					Take(simulation.State);
					simulation.DrainUntil(post.Turn);
					simulation.PostReplayed(post.Command);
					any = true;
					break;
				case CReplayDrain drain:
					Take(simulation.State);
					simulation.DrainUntil(drain.Turn);
					any = true;
					break;
				default:
					return any;
			}
		}
	}

	/// <summary>Following a game as it runs: whether the recording has an hour's tick next, so that the hour may be run.</summary>
	public bool HasHourNext => Peek() is CReplayTick;

	/// <summary>
	/// Replaying, before an hour's tick is posted: the commands posted from outside before it, each after draining the
	/// queue as often as it had been drained when it was posted, and then the queue drained as far as it had been when the
	/// tick was posted.
	/// </summary>
	public void BeforeHour(CSimulation simulation)
	{
		CGameState state = simulation.State;
		while (true)
		{
			switch (Take(state))
			{
				case CReplayPost post:
					simulation.DrainUntil(post.Turn);
					simulation.PostReplayed(post.Command);
					break;
				case CReplayDrain drain:
					simulation.DrainUntil(drain.Turn);
					break;
				case CReplayTick tick:
					simulation.DrainUntil(tick.Turn);
					return;
				default:
					throw new InvalidOperationException(
						$"the recording does not begin an hour at {state.Date} {state.Hour}h: it was made with other rules or another game");
			}
		}
	}

	private void Keep(CReplayEntry entry)
	{
		// Kept in memory where nothing else keeps it; a file is enough for a long run.
		if (_sink is null)
		{
			_entries.Add(entry);
		}
		else
		{
			_sink(entry);
		}
	}

	private CReplayEntry Take(CGameState state)
	{
		CReplayEntry? next = Peek();
		_peeked = null;
		_next++;
		return next ?? throw new CReplayEndedException($"the recording ends before {state.Date} {state.Hour}h");
	}

	/// <summary>The next entry without taking it, or null where the recording has ended.</summary>
	private CReplayEntry? Peek()
	{
		return _peeked ??= _source!(_next);
	}
}
