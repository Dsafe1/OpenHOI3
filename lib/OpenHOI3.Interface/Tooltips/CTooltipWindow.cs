namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The lines of a tooltip that are shown where it has more than fit (<see cref="CTooltipScroll"/>): from which line,
/// how many, and how many are left out above and below - each of which, where it is not nought, has a line of its own
/// saying so in the box.
/// </summary>
public readonly record struct CTooltipWindow(int First, int Count, int Above, int Below)
{
	/// <summary>Every line of a text of so many.</summary>
	public static CTooltipWindow Whole(int lines) => new(0, lines, 0, 0);
}
