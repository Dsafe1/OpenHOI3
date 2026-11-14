using System.Collections.Generic;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// The picture a <c>textBoxType</c> or an <c>editBoxType</c> draws behind its text - its <c>textureFile</c> - as the
/// original's font draws it into the picture its lines are drawn into (<c>docs/mechanics/interface.md</c>, section 6;
/// <c>reversing/findings/FINDINGS-textdraw.md</c>, section 2.2.2).
///
/// It is cut in nine by the box's <c>borderSize</c>: the four corners are the border's size in the picture and on the
/// screen, the edges are stretched along their length, the middle both ways - stretched and not repeated, each part one
/// quad. It is drawn from the upper left corner of what the box shows, as large as what is shown less two pixels across
/// where the border across is not nought, and less two down where the border down is not.
/// </summary>
public static class CTextBoxPicture
{
	/// <summary>How much a border that is not nought takes off the picture's far side.</summary>
	public const int Short = 2;

	/// <summary>How large the picture is drawn, from the size of what the box shows.</summary>
	public static (float Width, float Height) Size(float shownWidth, float shownHeight, int borderX, int borderY)
	{
		return (borderX != 0 ? shownWidth - Short : shownWidth, borderY != 0 ? shownHeight - Short : shownHeight);
	}

	/// <summary>
	/// The nine parts, row by row from the top: across, the picture's columns 0, the border, its width less the border
	/// and its width put at 0, the border, the drawn width less the border and the drawn width; down, the same with the
	/// heights. A part that comes out of no size, or less, is left out.
	/// </summary>
	/// <param name="width">How wide the picture is drawn (<see cref="Size"/>).</param>
	/// <param name="pictureWidth">How wide the picture's file is.</param>
	public static List<CPicturePart> Parts(float width, float height, int borderX, int borderY, int pictureWidth,
		int pictureHeight)
	{
		float[] across = { 0f, borderX, width - borderX, width };
		float[] down = { 0f, borderY, height - borderY, height };
		int[] columns = { 0, borderX, pictureWidth - borderX, pictureWidth };
		int[] rows = { 0, borderY, pictureHeight - borderY, pictureHeight };
		var parts = new List<CPicturePart>(9);
		for (int row = 0; row < 3; row++)
		{
			for (int column = 0; column < 3; column++)
			{
				var part = new CPicturePart(across[column], down[row], across[column + 1] - across[column],
					down[row + 1] - down[row], columns[column], rows[row], columns[column + 1] - columns[column],
					rows[row + 1] - rows[row]);
				if (part.Width > 0f && part.Height > 0f && part.SourceWidth > 0 && part.SourceHeight > 0)
				{
					parts.Add(part);
				}
			}
		}
		return parts;
	}
}
