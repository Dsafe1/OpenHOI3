namespace OpenHOI3.Sim.Supply;

/// <summary>
/// The order a day's supply pass takes the provinces in: the furthest from its depot
/// first. Provinces equally far come in the order the original's own sort leaves them
/// in, which is no order anybody chose and decides who is served first where a
/// province runs short - so the sort is done here the way the original's is done, step
/// for step. See <c>docs/mechanics/supply.md</c>, section 11.
/// </summary>
public static class CSupplyOrder
{
	/// <summary>A run this short is sorted by picking out its last again and again.</summary>
	private const int ShortRun = 8;

	/// <summary>Every province's number, the furthest from its depot first.</summary>
	public static int[] FarthestFirst(CSupplyNetwork network)
	{
		var order = new int[network.ProvinceCount];
		for (int i = 0; i < order.Length; i++)
		{
			order[i] = i;
		}
		Sort(order, network);
		return order;
	}

	/// <summary>Above nothing where the first belongs after the second: where it is the nearer.</summary>
	private static int Compare(CSupplyNetwork network, int first, int second)
	{
		int a = network.DistanceOf(first);
		int b = network.DistanceOf(second);
		return a < b ? 1 : a == b ? 0 : -1;
	}

	private static void Sort(int[] order, CSupplyNetwork network)
	{
		if (order.Length < 2)
		{
			return;
		}

		// The stretches still to be sorted. The longer half always waits, so there are
		// never more waiting than the count has binary places.
		var lowWaiting = new int[64];
		var highWaiting = new int[64];
		int waiting = 0;
		int low = 0;
		int high = order.Length - 1;

		while (true)
		{
			int size = high - low + 1;
			if (size <= ShortRun)
			{
				SortShort(order, network, low, high);
			}
			else
			{
				// The middle one of the first, the middle and the last is what the rest
				// is split about.
				int middle = low + size / 2;
				if (Compare(network, order[low], order[middle]) > 0)
				{
					Swap(order, low, middle);
				}
				if (Compare(network, order[low], order[high]) > 0)
				{
					Swap(order, low, high);
				}
				if (Compare(network, order[middle], order[high]) > 0)
				{
					Swap(order, middle, high);
				}

				int up = low;
				int down = high;
				while (true)
				{
					if (middle > up)
					{
						do
						{
							up++;
						}
						while (up < middle && Compare(network, order[up], order[middle]) <= 0);
					}
					if (middle <= up)
					{
						do
						{
							up++;
						}
						while (up <= high && Compare(network, order[up], order[middle]) <= 0);
					}
					do
					{
						down--;
					}
					while (down > middle && Compare(network, order[down], order[middle]) > 0);

					if (down < up)
					{
						break;
					}
					Swap(order, up, down);
					if (middle == down)
					{
						middle = up;
					}
				}

				// Whatever is level with the middle one is already in its place.
				down++;
				if (middle < down)
				{
					do
					{
						down--;
					}
					while (down > middle && Compare(network, order[down], order[middle]) == 0);
				}
				if (middle >= down)
				{
					do
					{
						down--;
					}
					while (down > low && Compare(network, order[down], order[middle]) == 0);
				}

				if (down - low >= high - up)
				{
					if (low < down)
					{
						lowWaiting[waiting] = low;
						highWaiting[waiting] = down;
						waiting++;
					}
					if (up < high)
					{
						low = up;
						continue;
					}
				}
				else
				{
					if (up < high)
					{
						lowWaiting[waiting] = up;
						highWaiting[waiting] = high;
						waiting++;
					}
					if (low < down)
					{
						high = down;
						continue;
					}
				}
			}

			waiting--;
			if (waiting < 0)
			{
				return;
			}
			low = lowWaiting[waiting];
			high = highWaiting[waiting];
		}
	}

	private static void SortShort(int[] order, CSupplyNetwork network, int low, int high)
	{
		while (high > low)
		{
			int last = low;
			for (int i = low + 1; i <= high; i++)
			{
				if (Compare(network, order[i], order[last]) > 0)
				{
					last = i;
				}
			}
			Swap(order, last, high);
			high--;
		}
	}

	private static void Swap(int[] order, int a, int b)
	{
		if (a != b)
		{
			(order[a], order[b]) = (order[b], order[a]);
		}
	}
}
