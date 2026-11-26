namespace OpenHOI3.Content.Windows;

/// <summary>What a column of a sorted table (<see cref="ETableKind.Sorted"/>) does about sorting; nothing in any other table.</summary>
[System.Flags]
public enum ETableColumnSort
{
	/// <summary>A click on its heading sorts by it, the smallest first, and a second click turns that round.</summary>
	Plain = 0,

	/// <summary>The first click sorts the largest first: a column of figures.</summary>
	LargestFirst = 1,

	/// <summary>The column a table is sorted by until a heading is clicked.</summary>
	Starts = 2,

	/// <summary>Its heading sorts nothing.</summary>
	Never = 4,
}
