using OpenHOI3.Content.Gui;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// Where the original's font places a <c>textBoxType</c>'s lines in the picture the box shows, and how much of that
/// picture is shown (<c>docs/mechanics/interface.md</c>, section 6; <c>reversing/findings/FINDINGS-textdraw.md</c>, section
/// 2.2). Not an <c>instantTextBoxType</c>'s, which its font draws straight to the screen and places by another rule
/// (<see cref="CGuiLayout.TextStart"/>).
///
/// A box <b>fits</b> where it says <c>fixedsize</c>, or gives both a width and a height. A box that fits is drawn into a
/// picture of its own size: a line set left starts at the border, a centred line at half the box less half the line -
/// the border no part of it - and a line set right ends one short of the border on the right. A box that does not fit is
/// drawn into a picture as wide as its widest line and its border on both sides, and every line starts at the border
/// whatever the box's format. The lines step down a line's height from the border at the top - or, in a box that fits
/// and has a picture behind its text, from half the room its height leaves the lines. Of the picture only the box's
/// width less its border on the right and its height less its border at the bottom are shown, where it fits and gives
/// them; the rest of the text is cut off. The box's orientation places the box and plays no part in its text.
/// </summary>
public static class CTextBoxPlacement
{
	/// <summary>The width a box of no <c>maxWidth</c> is laid out at.</summary>
	public const int DefaultWidth = 320;

	/// <summary>
	/// Whether a box's text is laid out in a picture of the box's own size: where it is of fixed size, or gives both a
	/// width and a height.
	/// </summary>
	public static bool Fits(int maxWidth, int maxHeight, bool fixedSize)
	{
		return fixedSize || (maxWidth != 0 && maxHeight != 0);
	}

	/// <summary>
	/// The width and height the font is given for a box, before anything else: those of its file, but where the box is
	/// of fixed size and gives a dimension as nought and it has a picture behind its text, that dimension is the
	/// picture's less the border - once, not twice.
	/// </summary>
	/// <param name="backgroundWidth">The picture's width, or null for a box with none.</param>
	public static (int Width, int Height) Size(int maxWidth, int maxHeight, bool fixedSize, int borderX, int borderY,
		int? backgroundWidth, int? backgroundHeight)
	{
		if (fixedSize && backgroundWidth is { } width && backgroundHeight is { } height)
		{
			if (maxHeight == 0)
			{
				maxHeight = height - borderY;
			}
			if (maxWidth == 0)
			{
				maxWidth = width - borderX;
			}
		}
		return (maxWidth, maxHeight);
	}

	/// <summary>Where a line starts, from the left edge of the picture, which is the box's.</summary>
	/// <param name="lineWidth">The line's width as measured.</param>
	public static float LineStart(EGuiTextFormat format, bool fits, int maxWidth, int borderX, float lineWidth)
	{
		if (!fits)
		{
			return borderX;
		}
		int width = maxWidth == 0 ? DefaultWidth : maxWidth;
		return format switch
		{
			EGuiTextFormat.Centre => width / 2 - (int)(lineWidth / 2f),
			EGuiTextFormat.Right => width - borderX - 1 - lineWidth,
			_ => borderX,
		};
	}

	/// <summary>
	/// Where the first line's top is, from the top of the picture: the border, or, in a box that fits and has a
	/// picture behind its text, half of what the box's height - the lines' own and the border where it gives none -
	/// leaves the lines, cut to a whole number towards nought, so that lines taller than the box start above it.
	/// </summary>
	public static float Top(bool fits, bool background, int maxHeight, int borderY, int lines, float lineHeight)
	{
		if (!fits || !background)
		{
			return borderY;
		}
		float text = lines * lineHeight;
		float height = maxHeight != 0 ? maxHeight : text + 2f * borderY;
		return (int)((height - text) / 2f);
	}

	/// <summary>
	/// How much of the picture is shown, from its upper left corner; what is drawn outside it is cut off. A box that
	/// fits: its width less the border, and its height less the border, or where it gives none the lines and the border
	/// above and below. A box that does not: its widest line and the border on both sides, and the lines and the border
	/// above and below.
	/// </summary>
	public static (float Width, float Height) Shown(bool fits, int maxWidth, int maxHeight, int borderX, int borderY,
		float widest, int lines, float lineHeight)
	{
		float text = lines * lineHeight + 2f * borderY;
		if (!fits)
		{
			return (widest + 2f * borderX, text);
		}
		int width = maxWidth == 0 ? DefaultWidth : maxWidth;
		return (width - borderX, maxHeight != 0 ? maxHeight - borderY : text);
	}
}
