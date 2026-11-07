namespace OpenHOI3.Interface.Text;

/// <summary>
/// The original's strength bar in text: <c>{</c> and two digits draw that frame of the sprite <c>GFX_tooltip_strength</c>
/// [mod: interface/tooltipicons.gfx] - eleven frames of 16 by 20, nought to ten tenths of a brigade's strength - and the pen
/// moves on 8, so the bars stand 8 apart. See <c>docs/mechanics/tooltips.md</c>, section 7, and
/// <c>reversing/findings/FINDINGS-countertooltip.md</c>, section 3.
/// </summary>
public static class CStrengthIcons
{
	public const char Mark = '{';

	public const string Sprite = "GFX_tooltip_strength";

	public const float Advance = 8f;

	public const float Width = 16f;

	public const float Height = 20f;

	/// <summary>
	/// How far above a line's top the bar's picture starts. *Ours*: where the original's text puts it against the line was
	/// not read; its opaque rows are 2 to 18 of 20, so this stands them on the line.
	/// </summary>
	public const float AboveLineTop = 3f;

	/// <summary>The frame two characters after the mark name, or null where they are not two digits.</summary>
	public static int? FrameAt(string text, int mark)
	{
		if (mark + 2 >= text.Length || !char.IsAsciiDigit(text[mark + 1]) || !char.IsAsciiDigit(text[mark + 2]))
		{
			return null;
		}
		return (text[mark + 1] - '0') * 10 + (text[mark + 2] - '0');
	}
}
