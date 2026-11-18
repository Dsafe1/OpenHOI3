using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// A kind of modifier - or a unit's statistic - as a number: the name given a number the first time it is met, matched
/// without case as the files' kinds are, and the same number for that name for as long as the program runs. The
/// original's kinds are an enum compiled into it; here they come from the files, so they are numbered as they are read.
/// What reads a kind every hour keeps its key (<c>static readonly</c>) and asks a <see cref="CModifier"/> by it, which
/// is an index and not a hash of the name.
///
/// The table is names alone, the same whatever game is played or how often, so it is no game state; it only grows, and
/// may be asked from any thread.
/// </summary>
public readonly record struct CModifierKey(int Id)
{
	private static readonly ConcurrentDictionary<string, int> Ids = new(StringComparer.OrdinalIgnoreCase);
	private static readonly List<string> Names = new();
	private static readonly object Numbering = new();

	/// <summary>The key of a kind, numbered now where it is met for the first time.</summary>
	public static CModifierKey Of(string name)
	{
		if (Ids.TryGetValue(name, out int id))
		{
			return new CModifierKey(id);
		}
		lock (Numbering)
		{
			if (!Ids.TryGetValue(name, out id))
			{
				id = Names.Count;
				Names.Add(name);
				Ids[name] = id;
			}
		}
		return new CModifierKey(id);
	}

	/// <summary>The key of a kind already met, or none: asking does not number it.</summary>
	public static bool TryFind(string name, out CModifierKey key)
	{
		if (Ids.TryGetValue(name, out int id))
		{
			key = new CModifierKey(id);
			return true;
		}
		key = default;
		return false;
	}

	/// <summary>The name the kind was first met by.</summary>
	public override string ToString()
	{
		lock (Numbering)
		{
			return Id >= 0 && Id < Names.Count ? Names[Id] : $"#{Id}";
		}
	}
}
