using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// How long each step of the tick has taken, for finding what makes a game slow. It is no
/// part of a game: nothing here is saved, and a game runs the same with one or without.
/// <see cref="CGameState.Profile"/> is null unless a tool sets it.
/// </summary>
public sealed class CTickProfile
{
	/// <summary>Each step by its name, with the stopwatch ticks it has taken and how often it ran.</summary>
	public Dictionary<string, (long Ticks, int Calls)> Steps { get; } = new();

	/// <summary>Adds a step's ticks; from any thread, an AI pass's countries running side by side (<see cref="Ai.CAiRun"/>).</summary>
	public void Add(string step, long ticks)
	{
		lock (Steps)
		{
			Steps.TryGetValue(step, out var was);
			Steps[step] = (was.Ticks + ticks, was.Calls + 1);
		}
	}
}
