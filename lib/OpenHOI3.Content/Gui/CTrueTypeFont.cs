using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// A <c>font = { }</c> entry of a <c>fonts</c> block in <c>interface/*.gfx</c>: a font the original makes from an installed
/// typeface rather than a bitmap - the loading screen's <c>Loading_Font</c> among them (<c>docs/mechanics/loadingscreen.md</c>,
/// section 2; <c>reversing/findings/FINDINGS-loadingscreen.md</c>, 2).
/// </summary>
/// <param name="Height">The <c>height</c>: the cell's height in pixels, as Windows takes a positive height.</param>
public sealed record CTrueTypeFont(string Name, string FontName, int Height, CColor Color);
