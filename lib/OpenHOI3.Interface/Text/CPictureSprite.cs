using System;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// Where a picture given to an icon by code is drawn, and which of its pixels - the original's picture sprite, which the
/// country screens' skin, the top bar's skin, the portraits, the terrain pictures and the technology pictures are made
/// as (<c>docs/mechanics/interface.md</c>, section 5; <c>reversing/findings/FINDINGS-guistatic.md</c>, section 6).
/// It is drawn with its sprite's own quad: the box is the sprite's file size, a frame of it, times the icon's scale, and
/// its texture coordinates are the sprite's - its file's size over the next power of two - laid on the picture, whose
/// texture the original also pads to a power of two with nothing in the padding. So a picture the size of its sprite's
/// file fills the box exactly, and one of another size is stretched or cut: BlackICE's skin sprite is 1280 by 736 and
/// the base game's German skin 1024 by 680, so the skin is drawn 1280 across from its first 640 columns, and its last 56
/// rows are the padding's nothing.
/// </summary>
public static class CPictureSprite
{
	/// <summary>
	/// The part of the box the picture covers, from the box's upper left corner, and the pixels of the picture shown there;
	/// null where nothing is drawn. The rest of the box is the padding, transparent.
	/// </summary>
	/// <param name="spriteWidth">The width of the sprite's own file, all its frames together.</param>
	public static CPicturePart? Place(int spriteWidth, int spriteHeight, int frames, float scale, int pictureWidth,
		int pictureHeight)
	{
		int count = Math.Max(1, frames);
		float boxWidth = spriteWidth / count * scale;
		float boxHeight = spriteHeight * scale;
		if (boxWidth <= 0f || boxHeight <= 0f || pictureWidth <= 0 || pictureHeight <= 0)
		{
			return null;
		}
		double across = (double)spriteWidth / PowerOfTwo(spriteWidth) / count * PowerOfTwo(pictureWidth);
		double down = (double)spriteHeight / PowerOfTwo(spriteHeight) * PowerOfTwo(pictureHeight);
		double shownWidth = Math.Min(across, pictureWidth);
		double shownHeight = Math.Min(down, pictureHeight);
		return new CPicturePart(0f, 0f, (float)(boxWidth * shownWidth / across), (float)(boxHeight * shownHeight / down),
			0, 0, (int)Math.Round(shownWidth), (int)Math.Round(shownHeight));
	}

	/// <summary>The least power of two not below the number, as the original's texture handler pads to.</summary>
	public static int PowerOfTwo(int value)
	{
		int power = 1;
		while (power < value)
		{
			power <<= 1;
		}
		return power;
	}
}
