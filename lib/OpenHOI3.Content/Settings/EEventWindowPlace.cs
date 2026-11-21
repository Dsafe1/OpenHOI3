namespace OpenHOI3.Content.Settings;

/// <summary>Where a country event's window comes up, an enhanced game's option (<c>docs/mechanics/gameoptions.md</c>, section 5).</summary>
public enum EEventWindowPlace
{
	/// <summary>Where the interface file puts it: the original's, and the start.</summary>
	AsFiled,

	/// <summary>At the screen's left edge, at the file's height.</summary>
	Left,

	/// <summary>In the middle of the screen's width, at the file's height.</summary>
	Centre,
}
