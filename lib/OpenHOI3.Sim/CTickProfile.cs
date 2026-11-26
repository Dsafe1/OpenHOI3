using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenHOI3.Sim;

/// <summary>
/// How long each step of the tick has taken, for finding what makes a game slow. It is no
/// part of a game: nothing here is saved, and a game runs the same with one or without.
/// <see cref="CGameState.Profile"/> is null unless a tool sets it, or a mod's page asks for the engine's timing
/// (<c>docs/mechanics/modwindows.md</c>, section 6.5).
/// </summary>
public sealed class CTickProfile
{
	/// <summary>Each step by its name, with the stopwatch ticks it has taken and how often it ran.</summary>
	public Dictionary<string, (long Ticks, int Calls)> Steps { get; } = new();

	/// <summary>What a step inside another is named with in front, by those that time one.</summary>
	public const string PartPrefix = "     of which ";

	private const string PartWords = "of which ";

	/// <summary>
	/// The step the orders an hour drains are timed as - the players' and the AI's, each kind a part of it - beside
	/// the hour's own tick, whose steps are timed one by one (<c>CSimulation</c>).
	/// </summary>
	public const string OrdersStep = "the orders drained";

	/// <summary>Whether a name is a part's: "of which" and what, however far it is set in.</summary>
	public static bool IsPart(string name) => name.TrimStart().StartsWith(PartWords, System.StringComparison.Ordinal);

	/// <summary>A part's name without the words in front of it.</summary>
	public static string PartName(string name) => IsPart(name) ? name.TrimStart()[PartWords.Length..] : name;

	private string _current = "";
	private readonly Dictionary<string, string> _parents = new();

	/// <summary>
	/// A step begins: the parts timed until it ends are its own, those timed on other threads it started among them.
	/// Answers the step that was running, to be given back to <see cref="End"/>.
	/// </summary>
	public string Begin(string step)
	{
		lock (Steps)
		{
			string was = _current;
			_current = step;
			return was;
		}
	}

	public void End(string was)
	{
		lock (Steps)
		{
			_current = was;
		}
	}

	/// <summary>How many of the last hours are kept one by one.</summary>
	public const int HoursKept = 240;

	private readonly Queue<long> _last = new();
	private long _plain, _plainTicks, _plainMost, _turning, _turningTicks, _turningMost;

	/// <summary>When it began, or was last cleared: a stopwatch's count.</summary>
	public long Started { get; private set; } = Stopwatch.GetTimestamp();

	/// <summary>A whole hour as the simulation ran it, the orders drained with it; apart where it turned a day.</summary>
	public void AddHour(long ticks, bool turnedDay)
	{
		lock (Steps)
		{
			if (turnedDay)
			{
				_turning++;
				_turningTicks += ticks;
				_turningMost = System.Math.Max(_turningMost, ticks);
			}
			else
			{
				_plain++;
				_plainTicks += ticks;
				_plainMost = System.Math.Max(_plainMost, ticks);
			}
			_last.Enqueue(ticks);
			if (_last.Count > HoursKept)
			{
				_last.Dequeue();
			}
		}
	}

	public CTickProfileHours Hours()
	{
		lock (Steps)
		{
			return new CTickProfileHours(_plain, _plainTicks, _plainMost, _turning, _turningTicks, _turningMost, _last.ToArray());
		}
	}

	/// <summary>The steps as they stand, the one that took longest first; a part with the step it was timed in, where it was in one.</summary>
	public List<(string Name, long Ticks, int Calls, string Parent)> Snapshot()
	{
		lock (Steps)
		{
			return Steps.Select(step => (step.Key, step.Value.Ticks, step.Value.Calls, _parents.GetValueOrDefault(step.Key, "")))
				.OrderByDescending(step => step.Ticks).ThenBy(step => step.Key, System.StringComparer.Ordinal).ToList();
		}
	}

	/// <summary>Everything counted is forgotten, and the count begins again from now.</summary>
	public void Clear()
	{
		lock (Steps)
		{
			Steps.Clear();
			_parents.Clear();
			_last.Clear();
			_plain = _plainTicks = _plainMost = _turning = _turningTicks = _turningMost = 0;
			Started = Stopwatch.GetTimestamp();
		}
	}

	/// <summary>Adds a step's ticks; from any thread, an AI pass's countries running side by side (<see cref="Ai.CAiRun"/>).</summary>
	public void Add(string step, long ticks)
	{
		lock (Steps)
		{
			Steps.TryGetValue(step, out var was);
			Steps[step] = (was.Ticks + ticks, was.Calls + 1);
			// A part belongs to the step it was first timed in.
			if (_current.Length != 0 && IsPart(step))
			{
				_parents.TryAdd(step, _current);
			}
		}
	}
}
