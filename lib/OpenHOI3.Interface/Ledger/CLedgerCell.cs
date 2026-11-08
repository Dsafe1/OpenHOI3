using System;

namespace OpenHOI3.Interface.Ledger;

/// <summary>
/// One cell of a ledger table: its words and what it sorts by - a whole number, a figure or text, tried in that order as the original's cell does. A cell may
/// carry a tooltip. See <c>docs/mechanics/statistics.md</c>, section 3.
/// </summary>
public sealed record CLedgerCell(string Text, long? SortInt = null, float? SortFloat = null, string? Tooltip = null)
{
	public static CLedgerCell Of(string text) => new(text);

	public static CLedgerCell Int(long value) => new(value.ToString(System.Globalization.CultureInfo.InvariantCulture), SortInt: value);

	/// <summary>A figure written to one place.</summary>
	public static CLedgerCell Float(float value) => new(value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), SortFloat: value);

	/// <summary>A share written as a whole percent.</summary>
	public static CLedgerCell Percent(float value) =>
		new(((int)(value * 100f)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%", SortFloat: value);

	/// <summary>Below, equal to or above another, as the column's cells compare.</summary>
	public int CompareTo(CLedgerCell other)
	{
		if (SortInt is { } whole && other.SortInt is { } otherWhole)
		{
			return whole.CompareTo(otherWhole);
		}
		if (SortFloat is { } figure && other.SortFloat is { } otherFigure)
		{
			return figure.CompareTo(otherFigure);
		}
		return string.CompareOrdinal(Text, other.Text);
	}
}
