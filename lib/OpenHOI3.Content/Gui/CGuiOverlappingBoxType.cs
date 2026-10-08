using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// An <c>OverlappingElementsBoxType</c>: a box that lays windows out in a row, and
/// lets them overlap when there are more than fit. What goes in it is for the screen
/// that owns it to decide.
/// </summary>
public sealed class CGuiOverlappingBoxType : CGuiType
{
	public CGuiPoint Size { get; init; }

	/// <summary>The gap left between one element and the next while they fit.</summary>
	public CFixedPoint Spacing { get; init; }

	/// <summary>Which end of the box the row starts from.</summary>
	public EGuiTextFormat Format { get; init; }
}
