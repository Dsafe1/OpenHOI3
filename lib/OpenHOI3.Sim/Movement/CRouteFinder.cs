using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// Finds a unit's way from one province to another, as the original does - which is
/// not quite the shortest way. See <c>docs/mechanics/movement.md</c>, section 3:
///
///     the provinces to look at next are kept in order of what each has cost so far
///     plus the straight line still to go, times 1.25;
///     a province is settled the first time a step reaches it, and a cheaper way to it
///     found later does not replace the first;
///     a province's neighbours are tried in the order of its edges.
/// </summary>
public static class CRouteFinder
{
	/// <summary>What the straight line still to go is multiplied by, in thousandths.</summary>
	private const long StraightLineWeight = 1250;

	/// <summary>
	/// The provinces to go through, the first step first and the goal last, or null
	/// when there is no way. Empty when the unit is to go where it starts.
	/// </summary>
	/// <param name="safe">
	/// A fleet on a mission keeps off an enemy's shores: a step into water beside an
	/// enemy's naval base or airfield costs three times as much, and nine beside both.
	/// </param>
	/// <param name="landSafety">How the way keeps clear of the enemy short of the goal: a land unit by its units, a fleet or an air unit by enemy ships (<see cref="IsSafeStep"/>).</param>
	public static List<int>? Find(CGameState state, CCountry country, CUnit unit, int from, int to, bool safe = false,
		ERouteSafety landSafety = ERouteSafety.Plain)
	{
		int count = state.Provinces.Count;
		if (from <= 0 || to <= 0 || from >= count || to >= count)
		{
			return null;
		}
		CProvince goal = state.Province(to);
		if (!CMovementRules.MayStandIn(state, country, unit, goal))
		{
			return null;
		}
		// A fleet's goal is asked once more before any search: whether the sea it lies in joins the fleet's own past the
		// closed waters, by region (movement.md, section 3). Each step then asks where the fleet may stand.
		if (unit.Kind == EUnitKind.Navy && !CMovementRules.SeaConnected(state, country, goal, state.Province(from)))
		{
			return null;
		}

		// Only a settled province's figures are read, and each is written as it is settled; the start's are set here.
		CRouteScratch scratch = CRouteScratch.Take(state, count);
		try
		{
			int[] settled = scratch.Settled;
			int stamp = scratch.Stamp;
			long[] cost = scratch.Cost;
			long[] estimate = scratch.Estimate;
			int[] cameFrom = scratch.CameFrom;
			int[] next = scratch.Next;
			cost[from] = 0;
			cameFrom[from] = -1;
			next[from] = -1;

			settled[from] = stamp;
			int head = from;
			while (head != -1)
			{
				int current = head;
				head = next[current];
				if (current == to)
				{
					return PathTo(cameFrom, from, to);
				}

				CProvince here = state.Province(current);
				foreach (CProvinceEdge edge in here.Edges)
				{
					if (edge.To <= 0 || edge.To >= count || settled[edge.To] == stamp)
					{
						continue;
					}
					CProvince there = state.Province(edge.To);
					if (!CMovementRules.MayStep(state, country, unit, here, there, edge)
						|| (edge.To != to && !IsSafeStep(state, country, unit, there, landSafety)))
					{
						continue;
					}
					settled[edge.To] = stamp;

					long step = StepCost(state, country, there, edge);
					if (safe)
					{
						step = SafeStepCost(state, country, there, step);
					}
					cost[edge.To] = cost[current] + step;
					estimate[edge.To] = cost[edge.To] + StraightLine(state, there, goal) * StraightLineWeight;
					cameFrom[edge.To] = current;

					// Before the first that is not cheaper, so the newest of equals is taken first.
					int before = -1;
					int after = head;
					while (after != -1 && estimate[edge.To] > estimate[after])
					{
						before = after;
						after = next[after];
					}
					next[edge.To] = after;
					if (before == -1)
					{
						head = edge.To;
					}
					else
					{
						next[before] = edge.To;
					}
				}
			}
			return null;
		}
		finally
		{
			scratch.Release();
		}
	}

	/// <summary>
	/// Whether a unit's way may pass through a province short of its goal, the original's <c>CSafePathFind::MayStep</c> and
	/// <c>CVerySafePathFind::MayStep</c> (reversing/findings/FINDINGS-moving.md, A.4f). A fleet or an air unit, under
	/// either: not where enemy ships - an undeclared war over the province counted, and the rebels - come to a whole point
	/// of strength. A land unit, safe: not where any hostile land or air unit stands, the rebels counted and one off the
	/// country's own land refusing it outright; very safe: not into a province held by a country at war with it, nor one
	/// with a land battle in it. The original sets each figure against a list on the moving unit that is always empty, so
	/// any at all refuses.
	/// </summary>
	public static bool IsSafeStep(CGameState state, CCountry country, CUnit unit, CProvince there, ERouteSafety safety)
	{
		if (safety == ERouteSafety.Plain)
		{
			return true;
		}
		if (unit.Kind is EUnitKind.Navy or EUnitKind.Air)
		{
			return EnemyNavalStrength(state, country, there) <= 0;
		}
		if (safety == ERouteSafety.VerySafe)
		{
			if (CMovementRules.IsEnemy(state, country.Tag, there.Controller))
			{
				return false;
			}
			foreach (Combat.CCombat combat in state.Combats)
			{
				if (combat.Province == there.Id && combat.Kind == Combat.ECombatKind.Land)
				{
					return false;
				}
			}
			return true;
		}
		long hostile = 0;
		foreach (CUnit other in state.Positions.Of(there.Id))
		{
			if (other.Kind == EUnitKind.Navy || !CMovementRules.IsEnemy(state, country.Tag, other.Owner))
			{
				continue;
			}
			if (other.Owner.Value == Revolts.CRebelFactions.Rebels && there.Owner != country.Tag)
			{
				return false;
			}
			foreach (CSubUnit brigade in other.SubUnits)
			{
				hostile += brigade.Strength.Raw;
			}
		}
		return hostile <= 0;
	}

	/// <summary>
	/// The enemy ships in a province in whole points of strength, cut: the original's <c>EnemyNavalStrengthIn</c>, which
	/// asks of each fleet's owner whether it is an enemy where it stands.
	/// </summary>
	private static long EnemyNavalStrength(CGameState state, CCountry country, CProvince there)
	{
		long strength = 0;
		foreach (CUnit other in state.Positions.Of(there.Id))
		{
			if (other.Kind != EUnitKind.Navy || !CMovementRules.IsEnemyIn(state, other.Owner, country.Tag, there.Id))
			{
				continue;
			}
			foreach (CSubUnit ship in other.SubUnits)
			{
				strength += ship.Strength.Raw;
			}
		}
		return strength / CFixedPoint.Scale;
	}

	/// <summary>
	/// What a step costs the search: the edge's distance - a tenth more into ground an enemy holds, and twice as much
	/// where the country knows that ground at intelligence 2 or more and units stand in it. The original's
	/// <c>CPathFind::StepCost</c> (<c>docs/mechanics/movement.md</c>, section 3): so a way home keeps round an enemy's
	/// stack, as the Soviet 7th Air wing's from 1306 does on 16 July 1941.
	/// </summary>
	public static long StepCost(CGameState state, CCountry country, CProvince to, CProvinceEdge edge)
	{
		long distance = edge.Distance.Raw;
		if (!CMovementRules.IsEnemy(state, country.Tag, to.Controller))
		{
			return distance;
		}
		bool seen = Intelligence.CProvinceIntel.Level(state, to, country.Tag) >= 2 && state.UnitsIn(to.Id).Count > 0;
		return distance * (seen ? 2000 : 1100) / 1000;
	}

	/// <summary>What a step beside an enemy's base costs a fleet that keeps to safe water, in thousandths.</summary>
	private const long BesideAnEnemyBase = 3000;

	/// <summary>
	/// What a step into a sea province costs a fleet that keeps off an enemy's shores:
	/// the plain cost, three times over where a province beside it has a naval base and
	/// once more where one has an airfield. As the original has it, the neighbours are
	/// gone through in order and **once one is found to be held by a country at war with
	/// the fleet's, every neighbour after it counts as well**, whoever holds it.
	/// </summary>
	public static long SafeStepCost(CGameState state, CCountry country, CProvince to, long cost)
	{
		if (!to.IsSea)
		{
			return cost;
		}
		bool hostile = false;
		bool navalBase = false;
		bool airBase = false;
		foreach (CProvinceEdge edge in to.Edges)
		{
			if (hostile && airBase && navalBase)
			{
				break;
			}
			if (edge.To <= 0 || edge.To >= state.Provinces.Count)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			hostile = hostile || CMovementRules.IsEnemy(state, country.Tag, beside.Controller);
			if (!hostile)
			{
				continue;
			}
			if (!navalBase && CMovementRules.BuildingLevel(state, beside, CMovementRules.NavalBase) > CFixedPoint.Zero)
			{
				cost = cost * BesideAnEnemyBase / CFixedPoint.Scale;
				navalBase = true;
			}
			if (!airBase && CMovementRules.BuildingLevel(state, beside, CMovementRules.AirBase) > CFixedPoint.Zero)
			{
				cost = cost * BesideAnEnemyBase / CFixedPoint.Scale;
				airBase = true;
			}
		}
		return cost;
	}

	/// <summary>
	/// The whole pixels between the centres of two provinces, the short way round the
	/// map, rounded down.
	/// </summary>
	public static long StraightLine(CGameState state, CProvince a, CProvince b)
	{
		long across = b.CentreX - a.CentreX;
		long up = b.CentreY - a.CentreY;
		int width = state.MapWidth;
		if (width > 0)
		{
			if (across < -(width / 2))
			{
				across += width;
			}
			if (across > width / 2)
			{
				across -= width;
			}
		}
		return WholeRoot(across * across + up * up);
	}

	/// <summary>The square root of a whole number, rounded down, with no fractions on the way.</summary>
	public static long WholeRoot(long value)
	{
		if (value <= 1)
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

	private static List<int> PathTo(int[] cameFrom, int from, int to)
	{
		var path = new List<int>();
		for (int at = to; at != from && at != -1; at = cameFrom[at])
		{
			path.Add(at);
		}
		path.Reverse();
		return path;
	}
}
