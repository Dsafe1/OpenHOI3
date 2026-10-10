using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// Finds the way a convoy sails from one harbour to another, as the original does -
/// which is not quite the shortest way. See <c>docs/mechanics/convoys.md</c>, section 4:
///
///     the provinces to look at next are kept in a heap, by what each has cost so far
///     plus the straight line still to go, times 1.25;
///     a province is settled the first time a step reaches it;
///     from the harbour any sea beside it will do, and the far harbour is reached from
///     any sea beside that;
///     a step out of water with an enemy's land beside it counts eight times and more.
/// </summary>
public static class CConvoyRoute
{
	/// <summary>What the straight line still to go is multiplied by, in thousandths.</summary>
	private const long StraightLineWeight = 1250;

	/// <summary>What a step out of a province beside an enemy's is multiplied by, in thousandths.</summary>
	private const long BesideAnEnemy = 8000;

	/// <summary>The same where the province has a naval base, before twice its level.</summary>
	private const long BesideAnEnemyWithPort = 13000;

	/// <summary>What an air base there adds, before twice its level.</summary>
	private const long WithAirBase = 5000;

	private const long PerLevel = 2000;

	/// <summary>
	/// The provinces of the way, the start first and the end last, or null where there is
	/// none.
	/// </summary>
	public static List<int>? Find(CGameState state, int from, int to, CCountryTag owner)
	{
		int count = state.Provinces.Count;
		if (from <= 0 || to <= 0 || from >= count || to >= count)
		{
			return null;
		}
		CProvince goal = state.Province(to);
		// Whose straits are shut is asked of whoever holds the harbour it sails from.
		CCountry? sailing = state.Country(state.Province(from).Controller);

		var settled = new bool[count];
		var cost = new long[count];
		var cameFrom = new int[count];
		var heap = new CPathHeap();
		settled[from] = true;
		cameFrom[from] = -1;
		heap.Push(0, from);

		while (heap.Count > 0)
		{
			int current = heap.Pop();
			if (current == to)
			{
				var path = new List<int>();
				for (int at = to; at != -1; at = cameFrom[at])
				{
					path.Add(at);
				}
				path.Reverse();
				return path;
			}

			CProvince here = state.Province(current);
			long dearer = Dearer(state, here, owner);
			foreach (CProvinceEdge edge in here.Edges)
			{
				if (edge.Kind == EProvinceEdgeKind.Impassable || edge.To <= 0 || edge.To >= count)
				{
					continue;
				}
				CProvince there = state.Province(edge.To);
				if (there.Id != to && !(there.IsSea && there.SeaIndex >= 0))
				{
					continue;
				}
				if (sailing is not null && CMovementRules.IsClosedWater(state, sailing, there))
				{
					continue;
				}
				if (settled[there.Id])
				{
					continue;
				}
				settled[there.Id] = true;

				long step = edge.Distance.Raw;
				if (dearer != CFixedPoint.Scale)
				{
					step = dearer * step / CFixedPoint.Scale;
				}
				if (step < 0)
				{
					continue;
				}
				cost[there.Id] = cost[current] + step;
				cameFrom[there.Id] = current;
				long still = CRouteFinder.StraightLine(state, there, goal) * CFixedPoint.Scale
					* StraightLineWeight / CFixedPoint.Scale;
				heap.Push(cost[there.Id] + still, there.Id);
			}
		}
		return null;
	}

	/// <summary>
	/// What a step out of a province is multiplied by, in thousandths: one, or much more
	/// where land an enemy of the owner holds lies beside it - and more again for a
	/// naval base or an air base of the province's own.
	/// </summary>
	private static long Dearer(CGameState state, CProvince here, CCountryTag owner)
	{
		if (!HasEnemyBeside(state, here, owner))
		{
			return CFixedPoint.Scale;
		}
		long times = BesideAnEnemy;
		long port = CMovementRules.BuildingLevel(state, here, CMovementRules.NavalBase).Raw;
		if (port > 0)
		{
			times = port * PerLevel / CFixedPoint.Scale + BesideAnEnemyWithPort;
		}
		long airfield = CMovementRules.BuildingLevel(state, here, CMovementRules.AirBase).Raw;
		if (airfield > 0)
		{
			times += airfield * PerLevel / CFixedPoint.Scale + WithAirBase;
		}
		return times;
	}

	/// <summary>Whether a province beside this one is held by another country at war with the owner.</summary>
	public static bool HasEnemyBeside(CGameState state, CProvince here, CCountryTag owner)
	{
		foreach (CProvinceEdge edge in here.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable || edge.To <= 0 || edge.To >= state.Provinces.Count)
			{
				continue;
			}
			CCountryTag? holder = state.Province(edge.To).Controller;
			if (holder is not null && holder != owner && CMovementRules.IsEnemy(state, owner, holder))
			{
				return true;
			}
		}
		return false;
	}
}
