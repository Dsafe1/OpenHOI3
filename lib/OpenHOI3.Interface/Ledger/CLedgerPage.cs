using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Interface.Ledger;

/// <summary>
/// One page of the Statistics screen: its heading and either a table or four pies. A table starts sorted by its rows' names and is sorted again by a column on
/// demand. See <c>docs/mechanics/statistics.md</c>, sections 2 and 3.
/// </summary>
public sealed class CLedgerPage
{
	public CLedgerPage(string title, IReadOnlyList<CLedgerColumn> columns, IEnumerable<CLedgerRow> rows)
	{
		Title = title;
		Columns = columns;
		Rows = rows.OrderBy(row => row.Key, StringComparer.Ordinal).ToList();
	}

	public CLedgerPage(string title, IReadOnlyList<CLedgerPie> pies)
	{
		Title = title;
		Pies = pies;
		Columns = Array.Empty<CLedgerColumn>();
		Rows = new List<CLedgerRow>();
	}

	public string Title { get; }

	public IReadOnlyList<CLedgerColumn> Columns { get; }

	/// <summary>Where each column stands from the table's left edge.</summary>
	public IReadOnlyList<(int Left, int Right)> Spans { get; set; } = Array.Empty<(int, int)>();

	/// <summary>The rows, in the order they stand.</summary>
	public List<CLedgerRow> Rows { get; private set; }

	/// <summary>The four pies of a pie page; null on a table.</summary>
	public IReadOnlyList<CLedgerPie>? Pies { get; }

	/// <summary>The rows put in the order of one column, ties left as they stood.</summary>
	public void Sort(int column, bool ascending)
	{
		if (column < 0 || column >= Columns.Count)
		{
			return;
		}
		Comparison<CLedgerRow> compare = (a, b) => a.Cells[column].CompareTo(b.Cells[column]);
		IEnumerable<CLedgerRow> sorted = ascending
			? Rows.OrderBy(row => row, Comparer<CLedgerRow>.Create(compare))
			: Rows.OrderByDescending(row => row, Comparer<CLedgerRow>.Create(compare));
		Rows = sorted.ToList();
	}
}
