using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The ten start values a custom map source's shades begin at, and how many provinces have any of it - worked out when
/// the source is chosen and again as the days pass. See <c>docs/mechanics/custommapmodes.md</c>, section 4.
/// </summary>
public sealed class CCustomMapScale
{
	public const int Shades = 10;

	private CCustomMapScale(long[]? starts, int count)
	{
		Starts = starts;
		Count = count;
	}

	/// <summary>The start of each shade, lowest first; none for a level source, and for a map source nothing has any of.</summary>
	public long[]? Starts { get; }

	/// <summary>How many provinces have any, for a map source.</summary>
	public int Count { get; }

	public static CCustomMapScale Of(CGameState state, CCustomMapSource source)
	{
		switch (source.Scale)
		{
			case ECustomMapScale.Level:
				return new CCustomMapScale(null, 0);
			case ECustomMapScale.Bands:
				return new CCustomMapScale(source.Bands, 0);
		}
		var amounts = new List<long>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id > 0 && source.Value(state, province) is { } amount && amount > 0)
			{
				amounts.Add(amount);
			}
		}
		return FromAmounts(amounts);
	}

	/// <summary>
	/// A map source's starts from what the provinces hold: between the entry a hundredth of the way up the sorted
	/// amounts and the one ninety-nine hundredths up, each start the low one times their ratio to the power of its
	/// place over nine, rounded up - so the same step up a shade, not the same amount.
	/// </summary>
	public static CCustomMapScale FromAmounts(List<long> amounts)
	{
		int count = amounts.Count;
		if (count == 0)
		{
			return new CCustomMapScale(null, 0);
		}
		amounts.Sort();
		long low = amounts[Math.Min((int)(0.01 * count), count - 1)];
		long high = amounts[Math.Min((int)(0.99 * count), count - 1)];
		var starts = new long[Shades];
		for (int index = 0; index < Shades; index++)
		{
			starts[index] = high <= low
				? low
				: (long)Math.Ceiling(low * Math.Pow((double)high / low, index / (double)(Shades - 1)) - 1e-9);
		}
		if (high > low)
		{
			starts[0] = low;
			starts[Shades - 1] = high;
		}
		return new CCustomMapScale(starts, count);
	}

	/// <summary>
	/// The shade of a value, 1 to 10: a level itself; else one and how many starts after the first it reaches. Turned
	/// round, eleven less it, for a source where more is worse.
	/// </summary>
	public int Shade(CCustomMapSource source, long value)
	{
		int shade;
		if (source.Scale == ECustomMapScale.Level || Starts is null)
		{
			shade = (int)Math.Clamp(value, 1, Shades);
		}
		else
		{
			shade = 1;
			for (int index = 1; index < Shades; index++)
			{
				if (value >= Starts[index])
				{
					shade = index + 1;
				}
			}
		}
		return source.HigherIsWorse ? Shades + 1 - shade : shade;
	}
}
