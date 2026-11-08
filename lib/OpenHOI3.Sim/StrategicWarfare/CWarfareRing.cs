using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.StrategicWarfare;

/// <summary>
/// One ring of a warfare record's category: so many elements of eight columns, in 15-place fixed point, written round and round. The original's
/// <c>StrategicWarfareRing</c>. See <c>docs/mechanics/strategicwarfare.md</c>, section 2.
/// </summary>
public sealed class CWarfareRing
{
	public const int Columns = 8;

	private readonly long[] _data;

	public CWarfareRing(int count, int offset = 0)
	{
		Count = count;
		Offset = offset;
		_data = new long[count * Columns];
	}

	public int Count { get; }

	/// <summary>The element being filled next, which stays empty until the ring moves on.</summary>
	public int Offset { get; set; }

	/// <summary>Whether it has gone round at least once.</summary>
	public bool IsFull { get; set; }

	public long this[int element, int column]
	{
		get => _data[element * Columns + column];
		set => _data[element * Columns + column] = value;
	}

	/// <summary>The element a tally adds to: the one before <see cref="Offset"/>.</summary>
	public int Current => (Offset == 0 ? Count : Offset) - 1;

	/// <summary>Moves on one; true where that went back to the first. What it now stands on is left, for the sums to read before it is emptied.</summary>
	public bool Advance()
	{
		Offset++;
		bool wrapped = Offset >= Count;
		if (wrapped)
		{
			Offset = 0;
			IsFull = true;
		}
		return wrapped;
	}

	/// <summary>The element it stands on, emptied.</summary>
	public void EmptyCurrent()
	{
		for (int column = 0; column < Columns; column++)
		{
			this[Offset, column] = 0;
		}
	}

	public void Zero()
	{
		Array.Clear(_data);
	}

	/// <summary>One column over the whole ring.</summary>
	public long Sum(int column)
	{
		long total = 0;
		for (int element = 0; element < Count; element++)
		{
			total += this[element, column];
		}
		return total;
	}

	/// <summary>Each column's sum, written into an element of the next ring up: every element when full, otherwise those below the offset and the one it stands on.</summary>
	public void SumInto(CWarfareRing other, int element)
	{
		int last = IsFull ? Count - 1 : Offset;
		for (int column = 0; column < Columns; column++)
		{
			long total = 0;
			for (int each = 0; each <= last; each++)
			{
				total += this[each, column];
			}
			other[element, column] = total;
		}
	}

	/// <summary>One column newest first: not yet full, from the offset down to the first; full, from the one before the offset round to the offset; noughts to the ring's size.</summary>
	public List<long> Series(int column)
	{
		var values = new List<long>(Count);
		if (IsFull)
		{
			for (int step = 1; step <= Count; step++)
			{
				values.Add(this[(Offset - step + Count) % Count, column]);
			}
		}
		else
		{
			for (int element = Offset; element >= 0; element--)
			{
				values.Add(this[element, column]);
			}
		}
		while (values.Count < Count)
		{
			values.Add(0);
		}
		return values;
	}
}
