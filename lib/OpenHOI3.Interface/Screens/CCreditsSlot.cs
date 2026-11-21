namespace OpenHOI3.Interface.Screens;

/// <summary>One of the credits' thirty-five boxes: the line it shows, where it stands and whether it is shown.</summary>
public sealed class CCreditsSlot
{
	/// <summary>The line it shows, by its place in the list; -1 for none.</summary>
	public int Line { get; internal set; } = -1;

	/// <summary>How far below the screen's middle it stands; it falls as the credits roll.</summary>
	public float Y { get; internal set; }

	/// <summary>Where its box is put: the place cut to a whole number towards nought.</summary>
	public int BoxY => (int)Y;

	public bool Shown { get; internal set; }
}
