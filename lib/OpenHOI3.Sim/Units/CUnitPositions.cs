using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// Which units stand in which province. A unit knows where it is; this is the other
/// way about, made afresh at the start of every hour and kept up as units move during
/// it, so that a unit walking into a province finds who is there.
///
/// Within a province the units are in the order they came: at the start of an hour
/// that is country by country, and after it whoever arrives joins the end.
/// </summary>
public sealed class CUnitPositions
{
	private static readonly IReadOnlyList<CUnit> Nobody = Array.Empty<CUnit>();

	private readonly Dictionary<int, List<CUnit>> _byProvince = new();

	public CUnitPositions(CGameState state)
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
