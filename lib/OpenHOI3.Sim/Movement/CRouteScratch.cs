using System.Threading;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// The working arrays of a walk over the map - <see cref="CRouteFinder.Find"/>, <c>CConvoyRoute.Find</c> and
/// <c>CWeatherFronts.PropagateHighPressure</c> - one of each the size of the map, kept from one walk to the next. Ours,
/// and nothing of the game's: each walk made them afresh until 2026-11-04, half of everything the simulation allocated,
/// and big enough to land in the large object heap, where they set off every full collection.
///
/// A province has figures from this walk when its stamp is the walk's own, so nothing is cleared between walks; what a
/// walk reads of a province it has not stamped is its own default, never what the arrays hold.
/// </summary>
internal sealed class CRouteScratch
{
	private int _stamp;

	private int _inUse;

	private CRouteScratch(int count)
	{
		Settled = new int[count];
		Cost = new long[count];
		Estimate = new long[count];
		CameFrom = new int[count];
		Next = new int[count];
	}

	/// <summary>The stamp of the walk that last gave each province its figures.</summary>
	public int[] Settled { get; }

	public long[] Cost { get; }

	public long[] Estimate { get; }

	public int[] CameFrom { get; }

	public int[] Next { get; }

	/// <summary>The stamp of the walk begun by <see cref="Take"/>.</summary>
	public int Stamp => _stamp;

	/// <summary>
	/// The state's arrays for a walk over this many provinces, with a stamp no province holds yet - or arrays of the
	/// walk's own when a walk is using the state's already, whether on this thread or another.
	/// </summary>
	public static CRouteScratch Take(CGameState state, int count)
	{
		CRouteScratch? scratch = state.RouteScratch;
		if (scratch is null || scratch.Settled.Length < count)
		{
			scratch = state.RouteScratch = new CRouteScratch(count);
		}
		if (Interlocked.CompareExchange(ref scratch._inUse, 1, 0) != 0)
		{
			scratch = new CRouteScratch(count);
			scratch._inUse = 1;
		}
		if (scratch._stamp == int.MaxValue)
		{
			System.Array.Clear(scratch.Settled);
			scratch._stamp = 0;
		}
		scratch._stamp++;
		return scratch;
	}

	/// <summary>Hands the arrays back for the next walk.</summary>
	public void Release()
	{
		Volatile.Write(ref _inUse, 0);
	}
}
