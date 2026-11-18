using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// Which units stand in which province. A unit knows where it is; this is the other
/// way about, kept up as units move, so that a unit walking into a province finds who is there - made afresh when a
/// unit is put somewhere outside the march, and checked at the start of every hour against where every unit stands,
/// made afresh only where it no longer agrees (<see cref="CGameState.Positions"/>).
///
/// Within a province the units are in the order they came, as the original's list of a province's units is (read in the running
/// game): a table made afresh keeps the units its last one had there in their order, and whoever else is there, a unit just made
/// among them, joins the end. A game's first table is country by country.
/// </summary>
public sealed class CUnitPositions
{
	private static readonly IReadOnlyList<CUnit> Nobody = Array.Empty<CUnit>();

	/// <summary>Up to this many units in a province are put in order by looking along them, past it by sets.</summary>
	private const int LookedAlong = 16;

	private readonly Dictionary<int, List<CUnit>> _byProvince;

	public CUnitPositions(CGameState state, CUnitPositions? previous = null)
	{
		// Made the size the last table was, each province's list as long as it was there, so that nothing grows a
		// doubling at a time: the table is made again many times a day.
		_byProvince = new Dictionary<int, List<CUnit>>(previous?._byProvince.Count ?? 0);
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Location != 0 && !unit.IsRemoved)
				{
					In(unit.Location, previous).Add(unit);
				}
			}
		}
		if (previous is not null)
		{
			var ordering = new COrdering();
			foreach ((int province, List<CUnit> units) in _byProvince)
			{
				if (units.Count > 1 && previous._byProvince.TryGetValue(province, out List<CUnit>? before))
				{
					ordering.KeepOrder(units, before);
				}
			}
		}
	}

	/// <summary>What putting the provinces' units in order needs, made once a table and used for every province.</summary>
	private sealed class COrdering
	{
		private readonly List<CUnit> _ordered = new();
		private HashSet<CUnit>? _here;
		private HashSet<CUnit>? _placed;

		/// <summary>
		/// Puts those that stood there before first, in the order they stood, and the others after them as they are -
		/// each once. A few are looked for along the lists; many through sets, as the same order.
		/// </summary>
		public void KeepOrder(List<CUnit> units, List<CUnit> before)
		{
			List<CUnit> ordered = _ordered;
			ordered.Clear();
			if (units.Count <= LookedAlong)
			{
				foreach (CUnit unit in before)
				{
					if (units.Contains(unit) && !ordered.Contains(unit))
					{
						ordered.Add(unit);
					}
				}
				foreach (CUnit unit in units)
				{
					if (!ordered.Contains(unit))
					{
						ordered.Add(unit);
					}
				}
			}
			else
			{
				HashSet<CUnit> here = _here ??= new HashSet<CUnit>();
				HashSet<CUnit> placed = _placed ??= new HashSet<CUnit>();
				here.Clear();
				placed.Clear();
				here.UnionWith(units);
				foreach (CUnit unit in before)
				{
					if (here.Contains(unit) && placed.Add(unit))
					{
						ordered.Add(unit);
					}
				}
				foreach (CUnit unit in units)
				{
					if (placed.Add(unit))
					{
						ordered.Add(unit);
					}
				}
			}
			units.Clear();
			units.AddRange(ordered);
		}
	}

	/// <summary>A province's list, made where there is none as long as the last table's list there was.</summary>
	private List<CUnit> In(int province, CUnitPositions? previous)
	{
		if (!_byProvince.TryGetValue(province, out List<CUnit>? units))
		{
			int room = previous is not null && previous._byProvince.TryGetValue(province, out List<CUnit>? before) ? before.Count : 0;
			_byProvince[province] = units = new List<CUnit>(Math.Max(room, 1));
		}
		return units;
	}

	/// <summary>The units in a province, in the order they came. Not to be kept.</summary>
	public IReadOnlyList<CUnit> Of(int province)
	{
		return _byProvince.TryGetValue(province, out List<CUnit>? units) ? units : Nobody;
	}

	/// <summary>
	/// Whether the table, with <paramref name="adding"/> put in where they stand, holds every unit on the map where it
	/// stands and nothing else - each once - so that making it again from itself would give it back unchanged. A walk of
	/// the units as the table is made, each looked for in its province, and the entries counted; the units of
	/// <paramref name="adding"/> the walk meets go in <paramref name="walked"/> in the order it meets them.
	/// </summary>
	public bool HoldsExactly(CGameState state, List<CUnit>? adding, List<CUnit>? walked)
	{
		int standing = 0;
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Location != 0 && !unit.IsRemoved)
				{
					standing++;
					if (Holds(unit.Location, unit))
					{
						continue;
					}
					if (adding is null || !adding.Contains(unit))
					{
						return false;
					}
					walked?.Add(unit);
				}
			}
		}
		int entries = 0;
		foreach (List<CUnit> units in _byProvince.Values)
		{
			entries += units.Count;
		}
		if (adding is not null)
		{
			foreach (CUnit unit in adding)
			{
				if (unit.Location != 0 && !unit.IsRemoved)
				{
					entries++;
				}
			}
		}
		return entries == standing;
	}

	/// <summary>Whether the unit is among a province's.</summary>
	public bool Holds(int province, CUnit unit)
	{
		if (!_byProvince.TryGetValue(province, out List<CUnit>? units))
		{
			return false;
		}
		// A unit is itself and nothing else, so it is looked for as itself.
		for (int i = 0; i < units.Count; i++)
		{
			if (ReferenceEquals(units[i], unit))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A unit has gone from one province to another.</summary>
	public void Move(CUnit unit, int from, int to)
	{
		Remove(unit, from);
		if (to != 0)
		{
			In(to).Add(unit);
		}
	}

	/// <summary>
	/// A unit has gone from a province. A province left with nobody is dropped, as a table made afresh has none such -
	/// the table is kept for hours at a time now, and would otherwise gather every province a unit has left.
	/// </summary>
	public void Remove(CUnit unit, int from)
	{
		if (_byProvince.TryGetValue(from, out List<CUnit>? units) && units.Remove(unit) && units.Count == 0)
		{
			_byProvince.Remove(from);
		}
	}

	private List<CUnit> In(int province)
	{
		if (!_byProvince.TryGetValue(province, out List<CUnit>? units))
		{
			_byProvince[province] = units = new List<CUnit>();
		}
		return units;
	}
}
