namespace OpenHOI3.Interface.Text;

/// <summary>
/// The original's unit counter in text: <c>£</c> and the name after it draw the middle of the
/// sprite <c>GFX_counter_</c> + name - its symbol, without the frame - 30 by 16 from the top of
/// its line, and the pen moves on 30. See <c>docs/mechanics/tooltips.md</c>, section 7, and
/// <c>reversing/findings/FINDINGS-productiontooltip.md</c>, section 7.
/// </summary>
public static class CCounterIcons
{
	public const char Mark = '£';

	public const string SpritePrefix = "GFX_counter_";

	public const float Advance = 30f;

	public const float Width = 30f;

	public const float Height = 16f;

	/// <summary>The part of the counter's picture drawn, as fractions of it across and down.</summary>
	public const float Left = 0.26f;
	public const float Right = 0.75f;
	public const float Top = 0.23f;
	public const float Bottom = 0.55f;

	/// <summary>Whether a character belongs to the name after the mark: a letter or an underscore.</summary>
	public static bool IsNameCharacter(char character)
	{
		return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';
	}
}
