using OpenHOI3.Sim;

namespace OpenHOI3.Content.Map;

/// <summary>
/// An <c>animatedmaptext</c> of the interface's <c>.gfx</c> files: a text that rises over a province and fades. Of its
/// declaration only <c>speed</c> and its <c>textblock</c>'s <c>color</c> do anything for a message's text; the block's
/// <c>font</c>, <c>size</c>, <c>position</c> and <c>format</c> are read by the original and used by nothing
/// (<c>reversing/findings/FINDINGS-messageshow.md</c>, section 5.1). See <c>docs/mechanics/messages.md</c>, section 6.
/// </summary>
public sealed class CAnimatedMapTextType
{
	/// <summary>The type a message's text on the map is made from.</summary>
	public const string Message = "MessageMapText";

	/// <summary>The bitmapfont every such text is drawn in, found by this name whatever its block says [exe].</summary>
	public const string FontName = "AnimatedMapText";

	public required string Name { get; init; }

	/// <summary><c>speed</c>: the share of its rise one unit of its time makes. 0.01 unless said.</summary>
	public float Speed { get; init; } = 0.01f;

	/// <summary>Its <c>textblock</c>'s <c>color</c>: white unless said.</summary>
	public CColor Color { get; init; } = CColor.White;
}
