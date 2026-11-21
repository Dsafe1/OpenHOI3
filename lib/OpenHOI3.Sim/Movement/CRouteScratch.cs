using System.Collections.Concurrent;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// The working arrays of a walk over the map - <see cref="CRouteFinder.Find"/>, <c>CConvoyRoute.Find</c> and
/// <c>CWeatherFronts.PropagateHighPressure</c> - one of each the size of the map, kept from one walk to the next. Ours,
/// and nothing of the game's: each walk made them afresh until 2026-11-04, half of everything the simulation allocated,
/// and big enough to land in the large object heap, where they set off every full collection.
///
/// A province has figures from this walk when its stamp is the walk's own, so nothing is cleared between walks; what a
/// walk reads of a province it has not stamped is its own default, never what the arrays hold. The state keeps the sets not
/// in use, so that walks at once - the AI's countries side by side - each take one of their own and hand it back.
/// </summary>
internal sealed class CRouteScratch
{
	private readonly ConcurrentBag<CRouteScratch> _pool;

	private int _stamp;

	private CRouteScratch(ConcurrentBag<CRouteScratch> pool, int count)
	{
		_pool = pool;
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
	/// A set of the state's arrays for a walk over this many provinces, with a stamp no province holds yet: one not in use,
	/// or a new one where every set is in use, whether on this thread or another. A set too small is let go.
	/// </summary>
	public static CRouteScratch Take(CGameState state, int count)
	{
		ConcurrentBag<CRouteScratch> pool = state.RouteScratch;
		CRouteScratch? scratch = null;
		while (pool.TryTake(out CRouteScratch? kept))
		{
			if (kept.Settled.Length >= count)
			{
				scratch = kept;
				break;
			}
		}
		scratch ??= new CRouteScratch(pool, count);
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
		_pool.Add(this);
	}
}
