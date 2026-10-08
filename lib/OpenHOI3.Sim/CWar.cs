using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// One war: who started it, who it was started against, and when. Only that much is
/// kept so far, which is what the starting stockpile asks about.
/// </summary>
public sealed class CWar
{
	public CWar(string name, CDate started = default)
	{
		Name = name;
		Started = started;
	}

	public string Name { get; }

	/// <summary>The day it began.</summary>
	public CDate Started { get; }

	public List<CCountryTag> Attackers { get; } = new();

	public List<CCountryTag> Defenders { get; } = new();

	public bool Involves(CCountryTag tag)
	{
		return Attackers.Contains(tag) || Defenders.Contains(tag);
	}

	public override string ToString()
	{
		return $"{Name}: {string.Join(' ', Attackers)} against {string.Join(' ', Defenders)}";
	}
}
