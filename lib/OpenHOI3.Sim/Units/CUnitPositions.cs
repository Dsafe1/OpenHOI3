using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// Which units stand in which province. A unit knows where it is; this is the other
/// way about, made afresh at the start of every hour and kept up as units move during
/// it, so that a unit walking into a province finds who is there.
///
/// Within a province the units are in the order they came, as the original's list of a province's units is (read in the running
/// game): a table made afresh keeps the units its last one had there in their order, and whoever else is there, a unit just made
/// among them, joins the end. A game's first table is country by country.
/// </summary>
public sealed class CUnitPositions
{
	private static readonly IReadOnlyList<CUnit> Nobody = Array.Empty<CUnit>();

	private readonly Dictionary<int, List<CUnit>> _byProvince = new();

	public CUnitPositions(CGameState state, CUnitPositions? previous = null)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Location != 0 && !unit.IsRemoved)
				{
					In(unit.Location).Add(unit);
				}
			}
		}
		if (previous is not null)
		{
			foreach ((int province, List<CUnit> units) in _byProvince)
			{
				if (units.Count > 1 && previous._byProvince.TryGetValue(province, out List<CUnit>? before))
				{
					KeepOrder(units, before);
				}
			}
		}
	}

	/// <summary>Puts those that stood there before first, in the order they stood, and the others after them as they are.</summary>
	private static void KeepOrder(List<CUnit> units, List<CUnit> before)
	{
		var here = new HashSet<CUnit>(units);
		var ordered = new List<CUnit>(units.Count);
		var placed = new HashSet<CUnit>();
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
		units.Clear();
		units.AddRange(ordered);
	}

	/// <summary>The units in a province, in the order they came. Not to be kept.</summary>
	public IReadOnlyList<CUnit> Of(int province)
	{
		return _byProvince.TryGetValue(province, out List<CUnit>? units) ? units : Nobody;
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

	public void Remove(CUnit unit, int from)
	{
		if (_byProvince.TryGetValue(from, out List<CUnit>? units))
		{
			units.Remove(unit);
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
