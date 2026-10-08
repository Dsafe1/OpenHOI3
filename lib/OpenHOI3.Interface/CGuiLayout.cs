using OpenHOI3.Content.Gui;

namespace OpenHOI3.Interface;

/// <summary>
/// Where an element goes. An interface file gives a position in whole units and names
/// the point of the parent it is measured from; the element's upper left corner is that
/// point plus the position. See <c>docs/mechanics/interface.md</c>, section 3.
/// </summary>
public static class CGuiLayout
{
	/// <summary>The width and height of the canvas an interface file's units are measured on.</summary>
	public const int ReferenceWidth = 1920;
	public const int ReferenceHeight = 1080;

	/// <summary>The point an orientation names, as fractions of the parent's width and height.</summary>
	public static (float X, float Y) Anchor(EGuiOrientation orientation)
	{
		return orientation switch
		{
			EGuiOrientation.UpperRight => (1f, 0f),
			EGuiOrientation.LowerLeft => (0f, 1f),
			EGuiOrientation.LowerRight => (1f, 1f),
			EGuiOrientation.Center => (0.5f, 0.5f),
			EGuiOrientation.CenterUp => (0.5f, 0f),
			EGuiOrientation.CenterDown => (0.5f, 1f),
			_ => (0f, 0f),
		};
	}

	/// <summary>Where an element's upper left corner is inside a parent of that size.</summary>
	public static (float X, float Y) UpperLeft(CGuiType type, float parentWidth, float parentHeight)
	{
		(float x, float y) = Anchor(type.Orientation);
		return (x * parentWidth + type.Position.X, y * parentHeight + type.Position.Y);
	}

	/// <summary>
	/// How far every glyph is drawn from where the numbers put it: one unit to the
	/// right and one up. The original adds half a pixel to both coordinates of every
	/// corner of a glyph, in a space whose second axis points up, and beside its
	/// pictures that comes out as a whole pixel each way.
	/// See <c>docs/mechanics/interface.md</c>, section 6.
	/// </summary>
	public const int GlyphShiftX = 1;
	public const int GlyphShiftY = -1;

	/// <summary>
	/// Where a line of text starts inside its box, from the box's left edge, as the
	/// original's font works it out. Set left it starts at the border. Centred it is
	/// centred in the whole of the box's width from the border - half the box less
	/// half the line, each half cut to a whole number. Set right it ends at the box's
	/// right edge, and the border plays no part. A box measured from the upper right
	/// has the border added once more, whichever way it is set.
	/// </summary>
	public static float TextStart(EGuiTextFormat format, EGuiOrientation orientation, int border, int boxWidth,
		float textWidth)
	{
		int origin = orientation == EGuiOrientation.UpperRight ? border : 0;
		return format switch
		{
			EGuiTextFormat.Centre => origin + border + boxWidth / 2 - (int)(textWidth / 2f),
			EGuiTextFormat.Right => origin + boxWidth - textWidth,
			_ => origin + border,
		};
	}

	/// <summary>
	/// The part of a sprite sheet one frame takes, as fractions of the texture's width:
	/// where it starts and how wide it is. Frames are counted from one, as the original
	/// counts them; a number outside the sheet gets the nearest frame that is in it.
	/// </summary>
	public static (float Start, float Width) FrameRegion(int frame, int frameCount)
	{
		if (frameCount < 1)
		{
			frameCount = 1;
		}
		int index = frame < 1 ? 0 : frame > frameCount ? frameCount - 1 : frame - 1;
		return ((float)index / frameCount, 1f / frameCount);
	}
}
