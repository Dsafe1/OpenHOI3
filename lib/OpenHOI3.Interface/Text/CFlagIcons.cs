namespace OpenHOI3.Interface.Text;

/// <summary>
/// The original's flag in text: <c>@</c> and the three letters after it draw that country's
/// flag, 23 by 11, from 3 below the top of its line, and the pen moves on 24. See
/// <c>docs/mechanics/tooltips.md</c>, section 7, and
/// <c>reversing/findings/FINDINGS-provincetooltip.md</c>, section 8. A text is cut into words
/// and flags by <see cref="CTextIcons"/>.
/// </summary>
public static class CFlagIcons
{
	public const char Mark = '@';

	public const int TagLength = 3;

	public const float Advance = 24f;

	public const float Width = 23f;

	public const float Height = 11f;

	/// <summary>How far below the top of its line a flag starts.</summary>
	public const float BelowLineTop = 3f;
}
