namespace OpenHOI3.Interface.Map;

/// <summary>
/// The sixteen map mode buttons beside the minimap, <c>mapmode_1</c> to <c>mapmode_16</c>: their names, what each says
/// over itself and the frames they show. See <c>docs/mechanics/minimap.md</c>, section 4, and
/// <c>reversing/findings/FINDINGS-minimap.md</c>, section 5.
/// </summary>
public static class CMapModeButtons
{
	public const int Count = 16;

	/// <summary>The frame of a button whose mode is the map's, and of every other.</summary>
	public const int PressedFrame = 2;
	public const int ReleasedFrame = 1;

	/// <summary>A button's name in <c>menubar.gui</c>, from one.</summary>
	public static string Name(int button) => "mapmode_" + button;

	/// <summary>
	/// The localisation key of a button's tooltip, a single line: <c>MAPMODE_</c> and its number - but the seventh's,
	/// the region mode's, is <c>MAPMODE_7G</c>.
	/// </summary>
	public static string TooltipKey(int button) => button == 7 ? "MAPMODE_7G" : "MAPMODE_" + button;
}
