using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The units selected on the map: a list in the order they were selected, all of one kind and all the player's own.
/// The provinces a selection may hold instead are kept by the map view. See <c>docs/mechanics/selection.md</c>,
/// section 1.
/// </summary>
public sealed class CSelection
{
	private readonly List<CUnit> _units = new();

	public IReadOnlyList<CUnit> Units => _units;

	public int Count => _units.Count;

	/// <summary>The first unit selected, or null - whose kind the rest must be.</summary>
	public CUnit? First => _units.Count > 0 ? _units[0] : null;

	/// <summary>The one unit selected, or null where none or several are.</summary>
	public CUnit? Single => _units.Count == 1 ? _units[0] : null;

	public static ESelectionKind KindOf(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Navy => ESelectionKind.Fleet,
			EUnitKind.Air => ESelectionKind.Air,
			_ => ESelectionKind.Land,
		};
	}

	/// <summary>Whether a unit may be selected at all: it is the player's own.</summary>
	public static bool MaySelect(CUnit unit, CCountryTag? player)
	{
		return player is { } tag && unit.Owner == tag && !unit.IsRemoved;
	}

	public bool Contains(CUnit unit)
	{
		return _units.Contains(unit);
	}

	/// <summary>Whether a unit may join: the player's, not selected yet, and of the first one's kind.</summary>
	public bool MayAdd(CUnit unit, CCountryTag? player)
	{
		return MaySelect(unit, player) && !Contains(unit) && (First is not { } first || KindOf(first) == KindOf(unit));
	}

	/// <summary>Adds a unit where it may join, and answers whether it did.</summary>
	public bool Add(CUnit unit, CCountryTag? player)
	{
		if (!MayAdd(unit, player))
		{
			return false;
		}
		_units.Add(unit);
		return true;
	}

	/// <summary>
	/// Selects a unit where it is not selected yet, whatever its owner or kind - as the original's own call to select
	/// does, which Ctrl's whole command, a panel's row and the developer's switches go through.
	/// </summary>
	public void Force(CUnit unit)
	{
		if (!Contains(unit))
		{
			_units.Add(unit);
		}
	}

	public bool Remove(CUnit unit)
	{
		return _units.Remove(unit);
	}

	public void Clear()
	{
		_units.Clear();
	}

	/// <summary>
	/// Every land unit under a unit joins the selection, each where not selected yet and each followed by its own command
	/// before the next, in the order of battle's order; nothing is let go. Ctrl and a click on a land unit, the single
	/// panel's ribbon and Ctrl on its rows. Nothing asks their kind of the selection, as the original's own call does not
	/// (<c>selection.md</c>, sections 2.1 and 5.3).
	/// </summary>
	public void AddCommand(CUnit unit)
	{
		bool land = KindOf(unit) == ESelectionKind.Land;
		foreach (CUnit child in unit.Children)
		{
			if ((KindOf(child) == ESelectionKind.Land) != land)
			{
				continue;
			}
			if (!child.IsRemoved && !Contains(child))
			{
				_units.Add(child);
			}
			AddCommand(child);
		}
	}

	/// <summary>Takes out every unit that no longer is, and answers whether any went.</summary>
	public bool RemoveWhere(Predicate<CUnit> gone)
	{
		return _units.RemoveAll(gone) > 0;
	}
}
