namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// The straight line between two provinces as the original measures it where it asks
/// how far apart they are: between the centres of their bounding boxes, the short way
/// round the map, in quarters of a pixel and rounded down. An air unit's range is set
/// against it, and the distances by sea are sums of it. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 3.
/// </summary>
public static class CMapDistance
{
	/// <summary>Four times the pixels between the two centres, rounded down.</summary>
	public static long Between(CProvince a, CProvince b, int mapWidth)
	{
		return WholeRoot(Squared(a, b, mapWidth));
	}

	/// <summary>
	/// The square of four times the pixels between the two centres, which is what the
	/// original sets a radius's square against before it takes any root.
	/// </summary>
	public static long Squared(CProvince a, CProvince b, int mapWidth)
	{
		long across = System.Math.Abs((long)a.CentreX - b.CentreX);
		long up = (long)a.CentreY - b.CentreY;
		if (mapWidth > 0 && across > mapWidth / 2)
		{
			across = mapWidth - across;
		}
		return (across * across + up * up) * 16;
	}

	/// <summary>
	/// The least of some squares as the original's distance to a theatre's fronts takes it: in 32 bits, from a first best
	/// of a million, the root only at the end - so nothing comes out over 1000, and no squares at all come out at 1000.
	/// See <c>docs/mechanics/agents.md</c>, section 10.2.
	/// </summary>
	public static long LeastFromAMillion(System.Collections.Generic.IEnumerable<long> squares)
	{
		int least = 1_000_000;
		foreach (long square in squares)
		{
			int wrapped = unchecked((int)square);
			if (wrapped < least)
			{
				least = wrapped;
			}
		}
		return least < 2 ? least : WholeRoot(least);
	}

	/// <summary>The square root of a whole number, rounded down, with no fractions on the way.</summary>
	public static long WholeRoot(long value)
	{
		if (value < 2)
		{
			return value < 0 ? 0 : value;
		}
		long root = value / 2;
		while (true)
		{
			long better = (value / root + root) / 2;
			if (better >= root)
			{
				return root;
			}
			root = better;
		}
	}
}
