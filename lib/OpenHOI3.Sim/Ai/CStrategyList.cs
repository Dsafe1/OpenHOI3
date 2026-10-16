using System.Collections.Generic;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// One of a strategy's weighted lists - countries it fears or courts, provinces it means to take - as the original keeps
/// them: highest first, a key added to again moving below any equal, a total under 1 dropping out, and the lowest entries
/// cut after every add until the list is within its cap. See <c>docs/mechanics/aistrategy.md</c>, section 2.
/// </summary>
public sealed class CStrategyList<TKey> where TKey : notnull
{
	private readonly List<(TKey Key, int Weight)> _entries = new();

	/// <summary>The entries, highest weight first.</summary>
	public IReadOnlyList<(TKey Key, int Weight)> Entries => _entries;

	public int Count => _entries.Count;

	/// <summary>The key's weight, or nought where it is not listed.</summary>
	public int WeightOf(TKey key)
	{
		foreach ((TKey listed, int weight) in _entries)
		{
			if (EqualityComparer<TKey>.Default.Equals(listed, key))
			{
				return weight;
			}
		}
		return 0;
	}

	/// <summary>
	/// The original's weighted add: nothing for a weight of nought; a listed key's weight added to, and the key taken out
	/// where the total is under 1 or put back below its equals; a new key put in where its weight is above nought; then
	/// the lowest entries cut until no more than <paramref name="cap"/> are left - none where the cap is nought.
	/// </summary>
	public void Add(TKey key, int weight, int cap)
	{
		if (weight == 0)
		{
			return;
		}
		int at = IndexOf(key);
		int total = weight;
		if (at >= 0)
		{
			total += _entries[at].Weight;
			_entries.RemoveAt(at);
			if (total < 1)
			{
				return;
			}
		}
		else if (weight <= 0)
		{
			return;
		}
		Insert(key, total);
		while (cap > 0 && _entries.Count > cap)
		{
			_entries.RemoveAt(_entries.Count - 1);
		}
	}

	public void Clear()
	{
		_entries.Clear();
	}

	/// <summary>
	/// The original's scaling: where the first weight is above 400 it becomes 400, and every other is scaled by the same
	/// ratio - the ratio truncated to thousandths, each weight truncated to thousandths over it and then rounded.
	/// </summary>
	public void ScaleTo400()
	{
		if (_entries.Count == 0 || _entries[0].Weight <= CStrategyFigures.Top)
		{
			return;
		}
		long ratio = (long)_entries[0].Weight * 1000 * 1000 / (CStrategyFigures.Top * 1000);
		_entries[0] = (_entries[0].Key, CStrategyFigures.Top);
		for (int i = 1; i < _entries.Count; i++)
		{
			long scaled = (long)_entries[i].Weight * 1000 * 1000 / ratio;
			_entries[i] = (_entries[i].Key, CStrategyFigures.Rounded(scaled));
		}
	}

	private int IndexOf(TKey key)
	{
		for (int i = 0; i < _entries.Count; i++)
		{
			if (EqualityComparer<TKey>.Default.Equals(_entries[i].Key, key))
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>From the end back to the first entry at least as heavy, and in after it; at the front where there is none.</summary>
	private void Insert(TKey key, int weight)
	{
		int i = _entries.Count - 1;
		while (i >= 0 && _entries[i].Weight < weight)
		{
			i--;
		}
		_entries.Insert(i + 1, (key, weight));
	}
}
