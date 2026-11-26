namespace OpenHOI3.Content.Windows;

/// <summary>What kind of table a page begins (<see cref="IModWindowCanvas.BeginTable"/>).</summary>
public enum ETableKind
{
	/// <summary>Rows under headings, bordered and striped.</summary>
	Rows,

	/// <summary>
	/// No table of rows but columns side by side with a divider between them that is dragged: no outer border, no
	/// striped rows, no heading.
	/// </summary>
	Panes,

	/// <summary>
	/// Rows whose headings are clicked to sort by them. The table sorts nothing itself: it says which heading and
	/// which way (<see cref="IModWindowCanvas.TableSort"/>), and the page gives its rows in that order.
	/// </summary>
	Sorted,
}
