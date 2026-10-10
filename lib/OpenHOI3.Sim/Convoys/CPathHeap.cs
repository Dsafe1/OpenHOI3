using System.Collections.Generic;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// The heap a convoy's route is searched with: the entry with the least weight comes
/// out first, and of two with one weight the heap's own order decides - which is why it
/// is written out here and not borrowed. See <c>docs/mechanics/convoys.md</c>, section 4:
///
///     a new entry goes to the end and changes places with the one above it while it
///     weighs strictly less;
///     the least is taken from the top, the last entry is put in its place and sinks:
///     the left of the two below is looked at where it weighs no more than the right,
///     and comes up only where it weighs strictly less than the one sinking.
/// </summary>
public sealed class CPathHeap
{
	// The top is at 1, and the two below an entry at twice its place and one more.
	private readonly List<(long Weight, int Item)> _entries = new() { (0, 0) };

	public int Count => _entries.Count - 1;

	public void Push(long weight, int item)
	{
		_entries.Add((weight, item));
		int at = Count;
		while (at > 1 && _entries[at].Weight < _entries[at / 2].Weight)
		{
			(_entries[at], _entries[at / 2]) = (_entries[at / 2], _entries[at]);
			at /= 2;
		}
	}

	/// <summary>Takes the least out. There has to be one.</summary>
	public int Pop()
	{
		int least = _entries[1].Item;
		(long Weight, int Item) sinking = _entries[Count];
		_entries.RemoveAt(Count);
		int count = Count;
		if (count == 0)
		{
			return least;
		}

		int at = 1;
		while (true)
		{
			int left = at * 2;
			int right = left + 1;
			int below;
			if (left > count)
			{
				break;
			}
			if (right > count)
			{
				below = left;
			}
			else if (_entries[left].Weight <= _entries[right].Weight && _entries[left].Weight < sinking.Weight)
			{
				below = left;
			}
			else
			{
				below = right;
			}
			if (_entries[below].Weight >= sinking.Weight)
			{
				break;
			}
			_entries[at] = _entries[below];
			at = below;
		}
		_entries[at] = sinking;
		return least;
	}
}
