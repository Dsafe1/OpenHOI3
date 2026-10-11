namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What an element, or the map, says in a tooltip: three texts and a point. See
/// <c>docs/mechanics/tooltips.md</c>, section 2.
/// </summary>
public sealed record CTooltip
{
	/// <summary>Shown at once.</summary>
	public string Text { get; init; } = "";

	/// <summary>Shown once the mouse has been still, under a line.</summary>
	public string Delayed { get; init; } = "";

	/// <summary>Shown with <see cref="Delayed"/>, under a line of its own when anything comes first. Where a button's shortcut goes.</summary>
	public string DelayedExtra { get; init; } = "";

	/// <summary>
	/// The point, across. What an element answers is an offset from its corner; what the
	/// placing makes of it is the point itself.
	/// </summary>
	public int X { get; init; }

	/// <summary>The point, down.</summary>
	public int Y { get; init; }

	/// <summary>Whether the box hangs below the point. Otherwise it stands above it.</summary>
	public bool Below { get; init; } = true;

	/// <summary>Nothing to say.</summary>
	public static CTooltip Empty { get; } = new();
}
