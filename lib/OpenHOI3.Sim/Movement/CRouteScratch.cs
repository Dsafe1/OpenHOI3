namespace OpenHOI3.Sim.Movement;

/// <summary>
/// The working arrays of <see cref="CRouteFinder.Find"/>, one of each the size of the map, kept from one search to the
/// next. Ours, and nothing of the game's: a search made them afresh until 2026-11-04, half of everything the simulation
/// allocated, and two of them big enough to land in the large object heap, where they set off every full collection.
///
/// A province is settled in this search when its stamp is the search's own, so nothing is cleared between searches; the
/// other arrays are written before they are read for a settled province, and the start's are set by the search.
/// </summary>
internal sealed class CRouteScratch
{
	private int _stamp;

	private CRouteScratch(int count)
	{
		Settled = new int[count];
		Cost = new long[count];
		Estimate = new long[count];
		CameFrom = new int[count];
		Next = new int[count];
	}

	/// <summary>Whether a search is using these now, so that a second one meanwhile makes its own.</summary>
	public bool InUse { get; private set; }

	/// <summary>The stamp of the search that settled each province.</summary>
	public int[] Settled { get; }

	public long[] Cost { get; }

	public long[] Estimate { get; }

	public int[] CameFrom { get; }

	public int[] Next { get; }

	/// <summary>The stamp of the search begun by <see cref="Take"/>.</summary>
	public int Stamp => _stamp;

	/// <summary>
	/// The state's arrays for a search over this many provinces, with a stamp no province holds yet - or arrays of the
	/// search's own when a search is using the state's already.
	/// </summary>
	public static CRouteScratch Take(CGameState state, int count)
	{
		CRouteScratch? scratch = state.RouteScratch;
		if (scratch is { InUse: true })
		{
			scratch = new CRouteScratch(count);
		}
		else if (scratch is null || scratch.Settled.Length < count)
		{
			scratch = state.RouteScratch = new CRouteScratch(count);
		}
		scratch.InUse = true;
		if (scratch._stamp == int.MaxValue)
		{
			System.Array.Clear(scratch.Settled);
			scratch._stamp = 0;
		}
		scratch._stamp++;
		return scratch;
	}

	/// <summary>Hands the arrays back for the next search.</summary>
	public void Release()
	{
		InUse = false;
	}
}
