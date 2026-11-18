using System;
using System.Collections;
using System.Collections.Generic;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What each named circumstance does to a brigade's figures - a terrain, <c>river</c>, <c>fort</c>, <c>night</c>,
/// <c>amphibious</c> - by its name, matched without case, in the order the names were first given. Each is also held by
/// the name's key (<see cref="CModifierKey"/>), so what asks every hour - a march over its ground - finds it by an
/// index and not by hashing the name once a brigade.
/// </summary>
public sealed class CAdjusters : IEnumerable<KeyValuePair<string, CModifier>>
{
	private readonly Dictionary<string, CModifier> _byName = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Each name's adjuster by its key's number, null where it has none.</summary>
	private CModifier?[] _byKey = Array.Empty<CModifier?>();

	/// <summary>The adjusters in the order their names were first given, as the names are walked.</summary>
	private readonly List<CModifier> _inOrder = new();

	/// <summary>How many names are held.</summary>
	public int Count => _byName.Count;

	/// <summary>The adjuster of a name, which must be held.</summary>
	public CModifier this[string name]
	{
		get => _byName[name];
		set
		{
			if (_byName.TryGetValue(name, out CModifier? had))
			{
				_inOrder[_inOrder.IndexOf(had)] = value;
			}
			else
			{
				_inOrder.Add(value);
			}
			_byName[name] = value;
			int id = CModifierKey.Of(name).Id;
			if (id >= _byKey.Length)
			{
				Array.Resize(ref _byKey, Math.Max(id + 1, Math.Max(16, _byKey.Length * 2)));
			}
			_byKey[id] = value;
		}
	}

	public bool TryGetValue(string name, out CModifier adjuster)
	{
		return _byName.TryGetValue(name, out adjuster!);
	}

	/// <summary>The adjuster of a name by its key, for what asks every hour.</summary>
	public bool TryGetValue(CModifierKey key, out CModifier adjuster)
	{
		CModifier? found = key.Id < _byKey.Length ? _byKey[key.Id] : null;
		adjuster = found!;
		return found is not null;
	}

	public void Clear()
	{
		_byName.Clear();
		_inOrder.Clear();
		Array.Clear(_byKey);
	}

	/// <summary>
	/// Made a copy of another's, name for name in its order, each adjuster a copy of the other's. What a brigade's figures
	/// are made again from many times a day, so an adjuster this already had in the same place under the same name is
	/// emptied and filled again rather than made anew - the same names, values and order as copies made anew.
	/// </summary>
	internal void CopyOf(CAdjusters other)
	{
		List<CModifier> had = new(_inOrder);
		Clear();
		int place = 0;
		foreach ((string name, CModifier adjuster) in other._byName)
		{
			CModifier copy;
			if (place < had.Count && string.Equals(had[place].Name, name, StringComparison.Ordinal))
			{
				copy = had[place];
				copy.Clear();
			}
			else
			{
				copy = new CModifier(name);
			}
			copy.Add(adjuster);
			this[name] = copy;
			place++;
		}
	}

	public Dictionary<string, CModifier>.Enumerator GetEnumerator() => _byName.GetEnumerator();

	IEnumerator<KeyValuePair<string, CModifier>> IEnumerable<KeyValuePair<string, CModifier>>.GetEnumerator() =>
		_byName.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _byName.GetEnumerator();
}
