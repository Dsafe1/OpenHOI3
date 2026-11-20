using System.Collections.Generic;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// Watches, across one of the AI's steps, the two things of a country's AI that the game's own hour reads - whether its
/// theatres are to be made again and the wars it prepares - and keeps a <see cref="CSetAiMarksCommand"/> with the step
/// for each country either changed for. They are written from many places - the AI's day, its foreign minister's
/// scripts through the Lua, the pruning - so they are watched here, where every step passes, and not at each write.
/// </summary>
public static class CAiMarks
{
	/// <summary>Each country's marks as one figure, in the countries' order.</summary>
	public static ulong[] Of(CGameState state)
	{
		var marks = new ulong[state.Countries.Count];
		for (int i = 0; i < marks.Length; i++)
		{
			marks[i] = Of(state.Countries[i]);
		}
		return marks;
	}

	/// <summary>The countries whose marks are no longer what they were, each as the command that sets them, added to <paramref name="done"/>.</summary>
	public static void KeepChanged(CGameState state, ulong[] before, List<CCommand> done)
	{
		for (int i = 0; i < before.Length && i < state.Countries.Count; i++)
		{
			CCountry country = state.Countries[i];
			if (Of(country) != before[i])
			{
				done.Add(new CSetAiMarksCommand(country));
			}
		}
	}

	private static ulong Of(CCountry country)
	{
		var hash = new CHasher();
		hash.Add(country.Strategy.TheatresDirty);
		foreach (CWarTarget entry in country.WarTargets)
		{
			hash.Add(entry.Target.Value);
			hash.Add(entry.Decision?.Name);
			hash.Add(entry.Limited);
			hash.Add(entry.Hour);
			hash.Add(entry.ValueBits);
		}
		return hash.Value;
	}
}
