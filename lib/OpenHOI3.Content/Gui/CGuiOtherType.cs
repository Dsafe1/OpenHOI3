namespace OpenHOI3.Content.Gui;

/// <summary>
/// An element of a kind that is read and checked but that nothing draws yet: an edit
/// box, a shield, and the three kinds no file of the original's uses. It keeps its name
/// and its place, so that a window's children are all there in their order.
/// </summary>
public sealed class CGuiOtherType : CGuiType
{
	public CGuiPoint Size { get; init; }
}
