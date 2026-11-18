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

	/// <summary>How many names are held.</summary>
	public int Count => _byName.Count;

	/// <summary>The adjuster of a name, which must be held.</summary>
	public CModifier this[string name]
	{
		get => _byName[name];
		set
		{
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
		Array.Clear(_byKey);
	}

	public Dictionary<string, CModifier>.Enumerator GetEnumerator() => _byName.GetEnumerator();

	IEnumerator<KeyValuePair<string, CModifier>> IEnumerable<KeyValuePair<string, CModifier>>.GetEnumerator() =>
		_byName.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _byName.GetEnumerator();
}
