namespace OpenHOI3.Interface.Text;

/// <summary>
/// How a <c>tileSpriteType</c> - the <c>topcolor_*</c> backdrop behind the country screens - is drawn: a quad of the size
/// its declaration gives, its texture repeated a whole number of times each way, the declared size over the file's own,
/// **in integer division**, and each copy stretched to fill (<c>docs/mechanics/interface.md</c>, section 5;
/// <c>reversing/findings/FINDINGS-buttonstatefx.md</c>, section 7). The base game's strips, 64 by 512 declared 2048 by 512,
/// repeat 32 times across and once down at their own scale; BlackICE's, declared 2048 by 1000, 32 times across and once
/// down - 1000 over 512 being 1 - so its strip is stretched to 1000 high.
/// **The quad stands on its foot**: the original builds every sprite's quad up from its origin (y up), and places the
/// origin by the sprite's file height, which the tile sprite does not change - so a tile quad taller than its file rises
/// above where the file would start, its foot where the file's foot would be. BlackICE's, at y 573 in its window, has its
/// foot at 1085 and its top at 85, as the maintainer's screenshot of the original shows (*measured*; the size the original
/// places a tile sprite by was not read).
/// </summary>
public static class CTileSprite
{
	/// <summary>
	/// How many times the texture repeats across and down: the far texture coordinate of the quad. Nought where the declared
	/// size is less than the file's, as the original's division leaves it - the whole quad then shows the first texel's edge.
	/// </summary>
	public static (int Across, int Down) Repeats(int sizeX, int sizeY, int textureWidth, int textureHeight)
	{
		int across = textureWidth <= 0 ? 0 : sizeX / textureWidth;
		int down = textureHeight <= 0 ? 0 : sizeY / textureHeight;
		return (across, down);
	}
}
