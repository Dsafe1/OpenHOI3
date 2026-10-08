namespace OpenHOI3.Content.Gui;

/// <summary>
/// A position or a size as an interface file writes one: <c>{ x = 121 y = 1 }</c>, in
/// whole units of the reference canvas.
/// </summary>
public readonly record struct CGuiPoint(int X, int Y);
