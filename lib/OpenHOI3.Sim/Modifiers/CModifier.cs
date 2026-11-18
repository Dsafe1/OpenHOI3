using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// One value for each kind of modifier, nearly all of them zero - so only the ones
/// that are not are held.
///
/// A law is a modifier, and so is a minister's type, a building's worth and a country's
/// or a province's running total. Totals are never adjusted as things happen: they are
/// cleared and summed again from whatever currently applies. See
/// <c>docs/mechanics/modifiers.md</c>.
///
/// The kinds are held by their keys (<see cref="CModifierKey"/>), in the order they were first given, each with the
/// spelling it was first given: a few are found by looking along them, more by an index of the keys. What asks every
/// hour asks by key; asking by name is the same answer, the name matched without case.
/// </summary>
public sealed class CModifier
{
	/// <summary>Up to this many kinds are found by looking along them; past it an index by key is made.</summary>
	private const int LookedAlong = 8;

	private int[] _ids = Array.Empty<int>();
	private string[] _keys = Array.Empty<string>();
	private CFixedPoint[] _values = Array.Empty<CFixedPoint>();
	private int _count;

	/// <summary>Each key's place plus one, nought where it is not held: made once more than a few kinds are held.</summary>
	private int[]? _slots;

	private CValues? _view;

	public CModifier(string name = "")
	{
		Name = name;
	}

	/// <summary>What it is called in the files, when it came from one.</summary>
	public string Name { get; }

	/// <summary>
	/// A count that moves on with every change to the values, so that what keeps figures worked out of them
	/// (<see cref="Units.CSubUnitStats"/>) can tell they are still the same.
	/// </summary>
	public int Version { get; private set; }

	/// <summary>The kinds this holds a value for, and the values, in the order the kinds were first given.</summary>
	public IReadOnlyDictionary<string, CFixedPoint> Values => _view ??= new CValues(this);

	/// <summary>The value for a kind of modifier, which is zero for any kind not held.</summary>
	public CFixedPoint this[string kind] =>
		CModifierKey.TryFind(kind, out CModifierKey key) ? this[key] : CFixedPoint.Zero;

	/// <summary>The value for a kind of modifier by its key, which is zero for any kind not held.</summary>
	public CFixedPoint this[CModifierKey key]
	{
		get
		{
			int slot = Find(key.Id);
			return slot < 0 ? CFixedPoint.Zero : _values[slot];
		}
	}

	public void Set(string kind, CFixedPoint value)
	{
		Set(CModifierKey.Of(kind), kind, value);
	}

	public void Set(CModifierKey key, CFixedPoint value)
	{
		Set(key, null, value);
	}

	public void Add(string kind, CFixedPoint value)
	{
		CModifierKey key = CModifierKey.Of(kind);
		Set(key, kind, this[key] + value);
	}

	public void Add(CModifierKey key, CFixedPoint value)
	{
		Set(key, null, this[key] + value);
	}

	/// <summary>Adds every value of another modifier to this one, in its order.</summary>
	public void Add(CModifier other)
	{
		// Room for all of them at once, where it would otherwise grow a doubling at a time.
		if (_count + other._count > _ids.Length)
		{
			int size = Math.Max(_count + other._count, _ids.Length * 2);
			Array.Resize(ref _ids, size);
			Array.Resize(ref _keys, size);
			Array.Resize(ref _values, size);
		}
		for (int i = 0; i < other._count; i++)
		{
			var key = new CModifierKey(other._ids[i]);
			Set(key, other._keys[i], this[key] + other._values[i]);
		}
	}

	public void Clear()
	{
		if (_slots is not null)
		{
			for (int i = 0; i < _count; i++)
			{
				_slots[_ids[i]] = 0;
			}
		}
		Array.Clear(_ids, 0, _count);
		Array.Clear(_keys, 0, _count);
		Array.Clear(_values, 0, _count);
		_count = 0;
		Version++;
	}

	public override string ToString()
	{
		return Name.Length == 0 ? $"{_count} values" : Name;
	}

	/// <summary>A kind's place, or -1 where it is not held.</summary>
	private int Find(int id)
	{
		if (_slots is not null)
		{
			return id < _slots.Length ? _slots[id] - 1 : -1;
		}
		for (int i = 0; i < _count; i++)
		{
			if (_ids[i] == id)
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>A kind's value set: where it is held, in its place and spelling; else at the end, spelt as given.</summary>
	private void Set(CModifierKey key, string? spelling, CFixedPoint value)
	{
		Version++;
		int slot = Find(key.Id);
		if (slot >= 0)
		{
			_values[slot] = value;
			return;
		}
		if (_count == _ids.Length)
		{
			int size = Math.Max(8, _count * 2);
			Array.Resize(ref _ids, size);
			Array.Resize(ref _keys, size);
			Array.Resize(ref _values, size);
		}
		_ids[_count] = key.Id;
		_keys[_count] = spelling ?? key.ToString();
		_values[_count] = value;
		_count++;
		if (_slots is not null || _count > LookedAlong)
		{
			Index(key.Id);
		}
	}

	/// <summary>The index made, or grown to take a new key, and the last kind added put in it.</summary>
	private void Index(int newest)
	{
		if (_slots is null)
		{
			int most = 0;
			for (int i = 0; i < _count; i++)
			{
				most = Math.Max(most, _ids[i]);
			}
			_slots = new int[Math.Max(most + 1, 64)];
			for (int i = 0; i < _count; i++)
			{
				_slots[_ids[i]] = i + 1;
			}
			return;
		}
		if (newest >= _slots.Length)
		{
			Array.Resize(ref _slots, Math.Max(newest + 1, _slots.Length * 2));
		}
		_slots[newest] = _count;
	}

	/// <summary>The values as a dictionary by name, the name matched without case, in the order the kinds were first given.</summary>
	private sealed class CValues : IReadOnlyDictionary<string, CFixedPoint>
	{
		private readonly CModifier _modifier;

		public CValues(CModifier modifier)
		{
			_modifier = modifier;
		}

		public int Count => _modifier._count;

		public CFixedPoint this[string key] =>
			TryGetValue(key, out CFixedPoint value) ? value : throw new KeyNotFoundException($"no value for '{key}'");

		public IEnumerable<string> Keys
		{
			get
			{
				for (int i = 0; i < _modifier._count; i++)
				{
					yield return _modifier._keys[i];
				}
			}
		}

		IEnumerable<CFixedPoint> IReadOnlyDictionary<string, CFixedPoint>.Values
		{
			get
			{
				for (int i = 0; i < _modifier._count; i++)
				{
					yield return _modifier._values[i];
				}
			}
		}

		public bool ContainsKey(string key) => TryGetValue(key, out _);

		public bool TryGetValue(string key, out CFixedPoint value)
		{
			int slot = CModifierKey.TryFind(key, out CModifierKey found) ? _modifier.Find(found.Id) : -1;
			value = slot < 0 ? CFixedPoint.Zero : _modifier._values[slot];
			return slot >= 0;
		}

		public IEnumerator<KeyValuePair<string, CFixedPoint>> GetEnumerator()
		{
			for (int i = 0; i < _modifier._count; i++)
			{
				yield return new KeyValuePair<string, CFixedPoint>(_modifier._keys[i], _modifier._values[i]);
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
