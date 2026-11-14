using OpenHOI3.Content.Gui;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// Where an edit box's line and its caret stand, from the box's upper left corner, in this project's terms - the text
/// drawing adds its own unit right and unit up afterwards (<c>docs/mechanics/interface.md</c>, sections 6 and 8).
///
/// The original hands the line to the object a <c>textBoxType</c> draws through, with the box's whole size, its border,
/// format left and not of fixed size, and puts that object the border in from the box's corner
/// (<c>reversing/findings/FINDINGS-textdraw.md</c>, sections 2.2.1 and 2.2.2). So across, the line starts the border in
/// twice; up and down, it stands the border in twice - or, with a picture behind it, the border and half of what the
/// box's height leaves the line, cut towards nought (<see cref="CTextBoxPlacement.Top"/>).
///
/// <b>Measured</b>, on two screenshots of the original: with a picture behind it the line's glyphs are a unit right and a
/// unit up of that arithmetic, as every text here is drawn - the save dialog's <c>game_name</c>, its capitals 17 below
/// the box's top; with none they are a unit right and <b>not</b> up - the unit panel's <c>unitname</c>, its figures 11
/// below. So a line with no picture is put a unit lower here, against the unit up the drawing adds. Why the two differ
/// was not read. The caret stands where it was read, past the border by the line before it and 3, a line's height down
/// for each line above its own - and with a picture behind the line a unit higher, as the line is.
/// </summary>
public static class CEditLinePlacement
{
	/// <summary>Where the line starts across: the border twice, for the original's line is always set left.</summary>
	public static float Left(int borderX)
	{
		return borderX + CTextBoxPlacement.LineStart(EGuiTextFormat.Left, true, 0, borderX, 0f);
	}

	/// <summary>Where the line's top stands down, as this project's text drawing takes it.</summary>
	/// <param name="maxWidth">The box's <c>size</c> across.</param>
	/// <param name="maxHeight">The box's <c>size</c> down.</param>
	/// <param name="picture">Whether a picture - the box's <c>textureFile</c> - is drawn behind the line.</param>
	public static float Top(int maxWidth, int maxHeight, int borderY, float lineHeight, bool picture)
	{
		bool fits = CTextBoxPlacement.Fits(maxWidth, maxHeight, false);
		float arithmetic = borderY + CTextBoxPlacement.Top(fits, picture, maxHeight, borderY, 1, lineHeight);
		return picture ? arithmetic : arithmetic + 1f;
	}

	/// <summary>Where the caret's picture stands down, its line <paramref name="row"/> lines below the first.</summary>
	public static float CaretTop(int borderY, float lineHeight, int row, bool picture)
	{
		return borderY + lineHeight * row - (picture ? 1f : 0f);
	}
}
