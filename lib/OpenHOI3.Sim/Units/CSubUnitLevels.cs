using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The level a brigade holds of each of its technologies, by the technology's name matched without case - a dictionary
/// that also counts its changes (<see cref="Version"/>), so that what is worked out of the levels every hour
/// (<see cref="CSubUnit.LevelsHeld"/>) is worked out again only when one has changed.
/// </summary>
public sealed class CSubUnitLevels : IDictionary<string, int>, IReadOnlyDictionary<string, int>
{
	private readonly Dictionary<string, int> _levels = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>A count that moves on with every change.</summary>
	public int Version { get; private set; }

	public int this[string key]
	{
		get => _levels[key];
		set
		{
			_levels[key] = value;
			Version++;
		}
	}

	public int Count => _levels.Count;

	public bool IsReadOnly => false;

	public Dictionary<string, int>.KeyCollection Keys => _levels.Keys;

	public Dictionary<string, int>.ValueCollection Values => _levels.Values;

	ICollection<string> IDictionary<string, int>.Keys => _levels.Keys;

	ICollection<int> IDictionary<string, int>.Values => _levels.Values;

	IEnumerable<string> IReadOnlyDictionary<string, int>.Keys => _levels.Keys;

	IEnumerable<int> IReadOnlyDictionary<string, int>.Values => _levels.Values;

	public void Add(string key, int value)
	{
		_levels.Add(key, value);
		Version++;
	}

	public bool Remove(string key)
	{
		Version++;
		return _levels.Remove(key);
	}

	public void Clear()
	{
		_levels.Clear();
		Version++;
	}

	public bool ContainsKey(string key) => _levels.ContainsKey(key);

	public bool TryGetValue(string key, [MaybeNullWhen(false)] out int value) => _levels.TryGetValue(key, out value);

	public Dictionary<string, int>.Enumerator GetEnumerator() => _levels.GetEnumerator();

	IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator() => _levels.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _levels.GetEnumerator();

	void ICollection<KeyValuePair<string, int>>.Add(KeyValuePair<string, int> item) => Add(item.Key, item.Value);

	bool ICollection<KeyValuePair<string, int>>.Contains(KeyValuePair<string, int> item) =>
		((ICollection<KeyValuePair<string, int>>)_levels).Contains(item);

	void ICollection<KeyValuePair<string, int>>.CopyTo(KeyValuePair<string, int>[] array, int arrayIndex) =>
		((ICollection<KeyValuePair<string, int>>)_levels).CopyTo(array, arrayIndex);

	bool ICollection<KeyValuePair<string, int>>.Remove(KeyValuePair<string, int> item)
	{
		Version++;
		return ((ICollection<KeyValuePair<string, int>>)_levels).Remove(item);
	}
}
