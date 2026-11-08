using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// One of the order of battle browser's two lists: rows in the tree's order, a row open exactly where the next is deeper
/// and nothing else remembering it. See <c>docs/mechanics/oobbrowser.md</c>, section 4.
/// </summary>
public sealed class COobTree
{
	private readonly List<COobRow> _rows = new();

	public IReadOnlyList<COobRow> Rows => _rows;

	/// <summary>Moves on whenever the rows change.</summary>
	public int Version { get; private set; }

	/// <summary>A row for every top-level unit of the country, as made.</summary>
	public void FillRoots(CCountry country)
	{
		_rows.Clear();
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.Parent is null)
			{
				_rows.Add(new COobRow(unit, 0));
			}
		}
		Version++;
	}

	/// <summary>The roots again, with everything under each.</summary>
	public void ExpandAll(CCountry country)
	{
		FillRoots(country);
		var whole = new List<COobRow>(_rows.Count * 4);
		foreach (COobRow root in _rows)
		{
			AddSubtree(whole, root);
		}
		_rows.Clear();
		_rows.AddRange(whole);
		Version++;
	}

	private static void AddSubtree(List<COobRow> into, COobRow row)
	{
		into.Add(row);
		foreach (CUnit child in row.Unit.Children)
		{
			AddSubtree(into, new COobRow(child, row.Level + 1));
		}
	}

	/// <summary>Whether a row is open: the next row is deeper.</summary>
	public bool IsOpen(int index)
	{
		return index + 1 < _rows.Count && _rows[index + 1].Level > _rows[index].Level;
	}

	/// <summary>Whether any row is indented, which decides which of collapse and expand the window offers.</summary>
	public bool AnyIndented
	{
		get
		{
			foreach (COobRow row in _rows)
			{
				if (row.Level > 0)
				{
					return true;
				}
			}
			return false;
		}
	}

	public int IndexOf(CUnit unit)
	{
		for (int index = 0; index < _rows.Count; index++)
		{
			if (ReferenceEquals(_rows[index].Unit, unit))
			{
				return index;
			}
		}
		return -1;
	}

	/// <summary>Opens a row: its direct children and no further.</summary>
	public void Open(int index)
	{
		COobRow row = _rows[index];
		int at = index + 1;
		foreach (CUnit child in row.Unit.Children)
		{
			_rows.Insert(at++, new COobRow(child, row.Level + 1));
		}
		Version++;
	}

	/// <summary>Closes a row: every deeper row following it goes.</summary>
	public void Close(int index)
	{
		_rows.RemoveRange(index + 1, RowsBelow(index));
		Version++;
	}

	private int RowsBelow(int index)
	{
		int level = _rows[index].Level;
		int count = 0;
		while (index + 1 + count < _rows.Count && _rows[index + 1 + count].Level > level)
		{
			count++;
		}
		return count;
	}

	public void Toggle(int index)
	{
		if (IsOpen(index))
		{
			Close(index);
		}
		else
		{
			Open(index);
		}
	}

	/// <summary>
	/// A unit whose place in the tree changed: its row and the rows under it go, and it comes back at the end of its
	/// commander's children if his row is open; at the end of the list with no commander; and otherwise not at all.
	/// </summary>
	public void Reposition(CUnit unit)
	{
		int old = IndexOf(unit);
		if (old >= 0)
		{
			_rows.RemoveRange(old, 1 + RowsBelow(old));
		}
		if (unit.Parent is not { } parent)
		{
			_rows.Add(new COobRow(unit, 0));
		}
		else
		{
			int above = IndexOf(parent);
			if (above >= 0 && IsOpen(above))
			{
				_rows.Insert(above + 1 + RowsBelow(above), new COobRow(unit, _rows[above].Level + 1));
			}
		}
		Version++;
	}

	/// <summary>Every row above a unit opened, top down, so that it has one.</summary>
	public int EnsureRow(CUnit unit)
	{
		int index = IndexOf(unit);
		if (index >= 0 || unit.Parent is not { } parent)
		{
			return index;
		}
		int above = EnsureRow(parent);
		if (above < 0)
		{
			return -1;
		}
		if (!IsOpen(above))
		{
			Open(above);
		}
		return IndexOf(unit);
	}

	/// <summary>The first row to show so that a row is the last in view, moved on for as many of its children as leave two rows.</summary>
	public int ScrollFor(int index, int visible)
	{
		int children = Math.Min(RowsBelow(index), Math.Max(visible - 2, 0));
		return Math.Max(index + 1 - visible + children, 0);
	}

	/// <summary>The other list's rows, row for row.</summary>
	public void CopyFrom(COobTree other)
	{
		_rows.Clear();
		_rows.AddRange(other._rows);
		Version++;
	}
}
