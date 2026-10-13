using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// One war: its two sides, the first of each leading it, its name, whether it is limited, the hour it
/// was last declared into, its war goals by the country holding them, and the calls to arms waiting.
/// See <c>docs/mechanics/war.md</c>, section 1.
/// </summary>
public sealed class CWar
{
	public CWar(string name, CDate started = default)
	{
		Name = name;
		Started = started;
	}

	/// <summary>The name the history gives it; empty for a war made in play, which has <see cref="MadeName"/>.</summary>
	public string Name { get; }

	/// <summary>The name a declaration made it, written out when shown (<c>war.md</c>, section 7); none for the history's.</summary>
	public CWarName? MadeName { get; set; }

	/// <summary>The day it began.</summary>
	public CDate Started { get; }

	public List<CCountryTag> Attackers { get; } = new();

	public List<CCountryTag> Defenders { get; } = new();

	/// <summary>A limited war: its declarer's allies are not called and its faction does not join (section 5).</summary>
	public bool IsLimited { get; set; }

	/// <summary>The hour it was last declared into, the original's <c>action</c>.</summary>
	public long Action { get; set; }

	/// <summary>The war goals, under the country holding them, each list in the order they were added.</summary>
	public Dictionary<CCountryTag, List<CWarGoal>> Goals { get; } = new();

	/// <summary>The countries a call to arms in this war waits on, the original's call map. Not saved.</summary>
	public HashSet<CCountryTag> CallsWaiting { get; } = new();

	public bool Involves(CCountryTag tag)
	{
		return Attackers.Contains(tag) || Defenders.Contains(tag);
	}

	/// <summary>The goals the country holds in this war, none where it holds none.</summary>
	public IReadOnlyList<CWarGoal> GoalsOf(CCountryTag holder)
	{
		return Goals.TryGetValue(holder, out List<CWarGoal>? goals) ? goals : System.Array.Empty<CWarGoal>();
	}

	public override string ToString()
	{
		return $"{(MadeName?.ToString() ?? Name)}: {string.Join(' ', Attackers)} against {string.Join(' ', Defenders)}";
	}
}
