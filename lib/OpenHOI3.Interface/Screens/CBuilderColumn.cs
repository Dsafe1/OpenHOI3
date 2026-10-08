namespace OpenHOI3.Interface.Screens;

/// <summary>
/// One column of a builder window's list: the element of the entry it is written into,
/// the statistic it shows, and how. The button that sorts by it is <c>sort_</c> and the
/// element's name. See <c>docs/mechanics/builders.md</c>, section 3.
/// </summary>
public sealed class CBuilderColumn
{
	public CBuilderColumn(string element, string statistic, EBuilderFigure figure = EBuilderFigure.Plain, int decimals = 0)
	{
		Element = element;
		Statistic = statistic;
		Figure = figure;
		Decimals = decimals;
	}

	/// <summary>The text box of the entry: <c>sa</c>.</summary>
	public string Element { get; }

	/// <summary>The statistic by the key the unit files give it: <c>soft_attack</c>. It is also what the column sorts by.</summary>
	public string Statistic { get; }

	public EBuilderFigure Figure { get; }

	public int Decimals { get; }

	/// <summary>The button that sorts the list by this column.</summary>
	public string SortButton => CBuilderLists.SortPrefix + Element;
}
