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
			if (Take(state) is not CReplayStep kept || kept.Hour != state.TotalHours || kept.Step != name)
			{
				throw new InvalidOperationException(
					$"the recording does not have hour {state.TotalHours}'s {name} next: it was made with other rules or another game");
			}
			state.Outbox.AddRange(kept.Commands);
			state.Random.Skip(kept.Draws);
			state.LastUnitId += kept.UnitIds;
			return;
		}

		int posted = state.Outbox.Count;
		long draws = state.Random.Draws;
		int unitIds = state.LastUnitId;
		step(state);
		if (state.Random.Draws < draws)
		{
			throw new InvalidOperationException($"{name} seeded the generator again, which a recording cannot play back");
		}
		var commands = new List<CCommand>(state.Outbox.Count - posted);
		for (int i = posted; i < state.Outbox.Count; i++)
		{
			commands.Add(state.Outbox[i]);
		}
		Keep(new CReplayStep(state.TotalHours, name, commands, state.Random.Draws - draws, state.LastUnitId - unitIds));
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
		return _source!(_next++) ?? throw new CReplayEndedException($"the recording ends before {state.Date} {state.Hour}h");
	}
}
