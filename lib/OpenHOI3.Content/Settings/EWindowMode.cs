namespace OpenHOI3.Content.Settings;

/// <summary>
/// How the game's window stands on the screen, the player's choice on the game options' Video tab
/// (<c>docs/mechanics/gameoptions.md</c>, section 3). Ours: the original has full screen or not, by its
/// <c>fullScreen</c> setting, taken at its start.
/// </summary>
public enum EWindowMode
{
	/// <summary>The screen is the game's alone.</summary>
	Fullscreen,

	/// <summary>A window with its frame and title, among the others.</summary>
	Windowed,

	/// <summary>A window with no frame that covers the whole screen, the other programs a key away.</summary>
	Borderless,
}
