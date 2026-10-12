using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The flags a country, a province or the game holds: names set and cleared by script, matched
/// without case. A cleared flag is kept, unset. See <c>docs/mechanics/effects.md</c>, section 3.
/// </summary>
public sealed class CFlags
{
	private readonly Dictionary<string, bool> _flags = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Every flag held, set or not, by name.</summary>
	public IReadOnlyDictionary<string, bool> All => _flags;

	/// <summary>Sets a flag, making it where it is missing.</summary>
	public void Set(string name)
	{
		_flags[name] = true;
	}

	/// <summary>Clears a flag; one that is not there stays not there.</summary>
	public void Clear(string name)
	{
		if (_flags.ContainsKey(name))
		{
			_flags[name] = false;
		}
	}

	public bool IsSet(string name)
	{
		return _flags.TryGetValue(name, out bool set) && set;
	}
}
