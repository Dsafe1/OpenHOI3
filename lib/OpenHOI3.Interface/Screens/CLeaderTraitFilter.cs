using System.Collections.Generic;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// Which traits the window a leader is chosen in shows leaders with, while it is open:
/// a flag for each trait, and one for "no trait" - a leader with none is shown by that
/// alone, one with traits where any of his is on. **A trait asked about that has no flag
/// is given one, off**, as the original's map gives it. What the game keeps between one
/// window and the next is the list of those that were on, for each kind of leader
/// (<see cref="Save"/> and <see cref="Load"/>). See <c>docs/mechanics/leaders.md</c>,
/// section 12.
/// </summary>
public sealed class CLeaderTraitFilter
{
	private readonly Dictionary<CTrait, bool> _traits = new();
	private bool? _noTrait;

	/// <summary>
	/// The filter as the game left it for a kind of leader. Where it kept nothing - the
	/// first time, **or every trait was off** - every trait of the game is on, and "no
	/// trait" too, which the original's null trait is allowed for every kind.
	/// </summary>
	public static CLeaderTraitFilter Load(IReadOnlyList<CTrait?>? saved, IEnumerable<CTrait> traits)
	{
		var filter = new CLeaderTraitFilter();
		if (saved is null || saved.Count == 0)
		{
			filter._noTrait = true;
			foreach (CTrait trait in traits)
			{
				filter._traits[trait] = true;
			}
			return filter;
		}
		foreach (CTrait? trait in saved)
		{
			filter.Set(trait, true);
		}
		return filter;
	}

	/// <summary>The traits that are on, "no trait" as null, for the game to keep.</summary>
	public List<CTrait?> Save()
	{
		var on = new List<CTrait?>();
		if (_noTrait == true)
		{
			on.Add(null);
		}
		foreach ((CTrait trait, bool value) in _traits)
		{
			if (value)
			{
				on.Add(trait);
			}
		}
		return on;
	}

	/// <summary>Whether a trait is on, "no trait" as null; one never set is added, off.</summary>
	public bool IsOn(CTrait? trait)
	{
		if (trait is null)
		{
			_noTrait ??= false;
			return _noTrait.Value;
		}
		if (!_traits.TryGetValue(trait, out bool on))
		{
			_traits[trait] = false;
		}
		return on;
	}

	public void Set(CTrait? trait, bool on)
	{
		if (trait is null)
		{
			_noTrait = on;
		}
		else
		{
			_traits[trait] = on;
		}
	}

	/// <summary>Whether a leader passes: with no traits by "no trait", otherwise by any of his.</summary>
	public bool Passes(CLeader leader)
	{
		if (leader.Traits.Count < 1)
		{
			return IsOn(null);
		}
		foreach (CTrait trait in leader.Traits)
		{
			if (IsOn(trait))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The filter button's other click: every trait of the game on, "no trait" with them.</summary>
	public void AllOn(IEnumerable<CTrait> traits)
	{
		_noTrait = true;
		foreach (CTrait trait in traits)
		{
			_traits[trait] = true;
		}
	}

	/// <summary>
	/// A filter row's other click: where anything else is on, that trait alone; where it
	/// is the only one on, everything that has a flag.
	/// </summary>
	public void Solo(CTrait? trait)
	{
		IsOn(trait);
		bool othersOn = false;
		if (trait is not null && _noTrait == true)
		{
			othersOn = true;
		}
		foreach ((CTrait key, bool value) in _traits)
		{
			if (value && key != trait)
			{
				othersOn = true;
			}
		}
		if (_noTrait is not null)
		{
			_noTrait = othersOn ? trait is null : true;
		}
		foreach (CTrait key in new List<CTrait>(_traits.Keys))
		{
			_traits[key] = othersOn ? key == trait : true;
		}
	}
}
