namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The names the enhanced rows of the game options' Game tab are found by. A mod declares them in a <c>.gui</c> file of
/// its <c>interface/openhoi</c> folder, as the elements of one window, <c>openhoi_game_options</c>, which is made as a
/// part of the Game tab: for each row a label, whose text is the file's own, a value the screen writes, and two
/// arrows. Element names are the contract here as everywhere: a mod may lay them out as it likes and may not rename
/// them. <c>docs/mechanics/gameoptions.md</c>, section 5.
/// </summary>
public static class CEnhancedOptionRows
{
	/// <summary>The window that holds the rows, placed in the Game tab by its own position.</summary>
	public const string WindowName = "openhoi_game_options";

	public static string LabelName(string row) => $"openhoi_{row}_label";

	public static string ValueName(string row) => $"openhoi_{row}_value";

	public static string DecreaseName(string row) => $"decrease_openhoi_{row}_button";

	public static string IncreaseName(string row) => $"increase_openhoi_{row}_button";
}
