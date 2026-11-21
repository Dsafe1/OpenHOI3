using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// One country's part of an AI pass the countries run side by side, as the original's AI runs on worker threads: while it
/// is the thread's run, what the country posts goes to a list of its own, its draws on the AI's generator are made on a
/// generator of its own, seeded from the game's seed, the hour and the country, and the numbers it takes for new units are
/// stand-ins, given the next real numbers once the pass is over, the runs in the countries' order - so the numbers are
/// those the countries in turn would have taken. Nothing else of the game may be changed meanwhile: a number taken any other
/// way, or a change made at once, throws. Ours: the original's threads take countries as they come free, their draws and
/// numbers in whatever order that gives (<c>docs/mechanics/agents.md</c>, section 2).
/// </summary>
public sealed class CAiRun
{
	/// <summary>The first stand-in number: far below every real one, which count up from one.</summary>
	public const int FirstStandIn = int.MinValue / 2;

	[ThreadStatic]
	private static CAiRun? t_current;

	private readonly uint _salt;
	private CRandom? _random;

	public CAiRun(CGameState state, CCountry country, uint salt)
	{
		State = state;
		Country = country;
		_salt = salt;
	}

	public CGameState State { get; }

	public CCountry Country { get; }

	/// <summary>What the country posted, in order.</summary>
	public List<CCommand> Posted { get; } = new();

	/// <summary>How many numbers the run took.</summary>
	public int Taken { get; private set; }

	/// <summary>The run going on on this thread for the game given, if any.</summary>
	public static CAiRun? Of(CGameState state)
	{
		return t_current is { } run && ReferenceEquals(run.State, state) ? run : null;
	}

	/// <summary>
	/// Whether a pass going on on this thread, if any, belongs to another country than the unit's - and so sees what the
	/// unit's own pass decides this hour only as it stood when the hour's passes began (<c>docs/mechanics/moving.md</c>,
	/// section 7.3).
	/// </summary>
	public static bool IsForeign(Units.CUnit unit)
	{
		return t_current is { } run && run.Country.Tag != unit.Owner;
	}

	/// <summary>The run's generator, made at its first draw: the game's seed, the hour, the country and the pass.</summary>
	public CRandom Random
	{
		get
		{
			if (_random is null)
			{
				_random = new CRandom();
				_random.Seed(Seed(State, Country, _salt));
				_random.Feed = State.SharedAiRandom.Feed;
			}
			return _random;
		}
	}

	/// <summary>Stand-ins for that many new numbers, one after another: the first.</summary>
	public int Take(int count)
	{
		int first = FirstStandIn + Taken;
		Taken += count;
		return first;
	}

	/// <summary>Whether a number is one of a run's stand-ins.</summary>
	public static bool IsStandIn(int id)
	{
		return id < FirstStandIn / 2;
	}

	/// <summary>Runs the country's part on this thread, as its run.</summary>
	public void Run(Action<CGameState, CCountry> work)
	{
		CAiRun? outer = t_current;
		t_current = this;
		try
		{
			work(State, Country);
		}
		finally
		{
			t_current = outer;
		}
	}

	/// <summary>
	/// The run's end, on the game's own thread: its numbers taken from the game's in turn, every stand-in its commands carry
	/// given its number, and what it posted put after what is posted already.
	/// </summary>
	public void Finish()
	{
		int first = State.LastUnitId + 1;
		State.LastUnitId += Taken;
		Func<int, int> real = id => IsStandIn(id) ? first + (id - FirstStandIn) : id;
		foreach (CCommand command in Posted)
		{
			if (command is INewUnitIds numbered)
			{
				numbered.GiveIds(real);
			}
		}
		State.Outbox.AddRange(Posted);
	}

	private static uint Seed(CGameState state, CCountry country, uint salt)
	{
		unchecked
		{
			return state.Seed * 2654435761u + (uint)state.TotalHours * 40503u + (uint)(state.TotalHours >> 32)
				+ (uint)(country.Index + 1) * 2246822519u + salt * 0x9E3779B9u;
		}
	}
}
