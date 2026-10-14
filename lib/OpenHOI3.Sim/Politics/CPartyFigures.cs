using System;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// One figure a country keeps for every party - its popularity, or its organisation - in thousandths of a
/// percentage point, one value per ideology by its number, number 0 included; with the month's five tallies
/// of what set each figure (0 the election's scratch, 1 the effects, 2 the history, 3 the spies, 4 the
/// monthly drift - each the new figure, not the change) and the last change the drift made. Only the values
/// are saved. See <c>docs/mechanics/politics.md</c>, sections 1 and 3.
/// </summary>
public sealed class CPartyFigures
{
	public const int TallySlots = 5;

	public const int ScratchSlot = 0;
	public const int EffectSlot = 1;
	public const int HistorySlot = 2;
	public const int SpySlot = 3;
	public const int DriftSlot = 4;

	public CPartyFigures(int count)
	{
		Values = new long[count];
		Tallies = new long[count, TallySlots];
		LastChange = new long[count];
	}

	/// <summary>Each party's figure, by its ideology's number.</summary>
	public long[] Values { get; }

	public long[,] Tallies { get; }

	/// <summary>What the last monthly drift changed each figure by.</summary>
	public long[] LastChange { get; }

	public int Count => Values.Length;

	public long this[int ideology] => ideology >= 0 && ideology < Values.Length ? Values[ideology] : 0;

	/// <summary>Sets a figure and adds it to a tally; a figure below nought does nothing.</summary>
	public void Set(int ideology, long value, int slot)
	{
		if (value < 0 || ideology < 0 || ideology >= Values.Length)
		{
			return;
		}
		Tallies[ideology, slot] += value;
		Values[ideology] = value;
	}

	/// <summary>
	/// Every figure from number 1 scaled so that they come to a hundred: <c>(v x 1000 / sum) x 100000 / 1000</c>,
	/// two truncations, so each is a multiple of 0.1. Number 0 is left out of the sum and of the scaling; a sum of
	/// nought changes nothing.
	/// </summary>
	public void NormaliseToHundred()
	{
		long sum = Sum();
		if (sum == 0)
		{
			return;
		}
		for (int i = 1; i < Values.Length; i++)
		{
			Values[i] = Values[i] * 1000 / sum * 100000 / 1000;
		}
	}

	/// <summary>The figures from number 1 scaled to come to one, <c>v x 1000 / sum</c>; number 0 as it is.</summary>
	public static void NormaliseToOne(long[] values)
	{
		long sum = 0;
		for (int i = 1; i < values.Length; i++)
		{
			sum += values[i];
		}
		if (sum == 0)
		{
			return;
		}
		for (int i = 1; i < values.Length; i++)
		{
			values[i] = values[i] * 1000 / sum;
		}
	}

	/// <summary>The sum from number 1.</summary>
	public long Sum()
	{
		long sum = 0;
		for (int i = 1; i < Values.Length; i++)
		{
			sum += Values[i];
		}
		return sum;
	}

	/// <summary>
	/// The number of the highest figure, from number 1, the first of equals - the first figure taken even below
	/// nought; 0 where there is none.
	/// </summary>
	public static int Leading(long[] values)
	{
		int best = 0;
		for (int i = 1; i < values.Length; i++)
		{
			if (best == 0 || values[i] > values[best])
			{
				best = i;
			}
		}
		return best;
	}

	public void ClearTallies()
	{
		Array.Clear(Tallies);
	}
}
