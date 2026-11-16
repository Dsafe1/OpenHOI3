namespace OpenHOI3.Interface.Text;

/// <summary>
/// The box a button's caption is laid out in, as the original's button makes it (<c>docs/mechanics/interface.md</c>,
/// section 6; <c>reversing/findings/FINDINGS-textdraw.md</c>, section 1). A button that names a sprite draws its caption
/// the way a <c>textBoxType</c> draws its text, into its sprite's picture: of fixed size, centred, the button's
/// <c>borderSize</c> its border, cut first (<see cref="CFixedSizeCut"/>), broken by <see cref="CTextBoxLineBreak"/> and
/// placed by <see cref="CTextBoxPlacement"/>. A button that names none draws it as an <c>instantTextBoxType</c> at its
/// own place: its <c>size</c> the box, no border, its <c>format</c> and its orientation.
/// </summary>
public static class CButtonCaption
{
	/// <summary>The width and height the object an instantTextBoxType draws through gives a box of nought.</summary>
	public const int DefaultSize = 200;

	/// <summary>
	/// The width and height a sprite button's caption is laid out in, and whether it is cut to them first: the sprite's
	/// picture less the border once - and, for a picture of more than one frame, a quarter of the whole picture's width
	/// and all its height, uncut, whatever the number of frames. No text sprite of the base game or of BlackICE has more
	/// than one.
	/// </summary>
	/// <param name="pictureWidth">The sprite's picture's width, all its frames together.</param>
	public static (int Width, int Height, bool Cut) SpriteBox(int pictureWidth, int pictureHeight, int frames,
		int borderX, int borderY)
	{
		if (frames > 1)
		{
			return ((int)(pictureWidth * 0.25), pictureHeight, false);
		}
		return (pictureWidth - borderX, pictureHeight - borderY, true);
	}

	/// <summary>
	/// Whether a button draws its picture at all. One whose sprite is a <c>textSpriteType</c> and whose <c>buttonFont</c>
	/// is empty draws nothing and is never under the mouse: the original makes its picture from the sprite alone, and a
	/// text sprite's picture is the texture its text is rendered into, which with no font named is never made - so nothing
	/// is drawn and the picture's shown size stays nought. BlackICE's faction sort buttons on the diplomacy screen are the
	/// one such button in the base game and the mod together - "effectively disables it without a trace", its file says
	/// (<c>docs/mechanics/interface.md</c>, section 2; <c>reversing/findings/FINDINGS-guistatic.md</c>, section 6).
	/// </summary>
	public static bool DrawsPicture(bool isTextSprite, string font)
	{
		return !isTextSprite || font.Length > 0;
	}

	/// <summary>The width a button with no sprite lays its caption out in: its <c>size</c> across, 200 where nought.</summary>
	public static int TextWidth(int sizeX)
	{
		return sizeX == 0 ? DefaultSize : sizeX;
	}
}
