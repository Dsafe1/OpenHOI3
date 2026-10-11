namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the tooltip box shows of a tooltip, as the original puts it together each frame. See
/// <c>docs/mechanics/tooltips.md</c>, sections 3 and 6.
/// </summary>
public static class CTooltipText
{
	/// <summary>The line between a tooltip's halves: fourteen dashes.</summary>
	public const string Rule = "--------------";

	/// <summary>What the text is trimmed of at both ends.</summary>
	private static readonly char[] Blank = { '\n', '\t', '\r', ' ' };

	/// <summary>
	/// The text the box shows: <see cref="CTooltip.Text"/>, and once the mouse is still the
	/// two delayed texts, each under the line; trimmed. Empty means no box.
	/// </summary>
	public static string Compose(CTooltip tooltip, bool still)
	{
		string text = tooltip.Text;
		if (still && tooltip.Delayed.Length > 0)
		{
			text += "\n" + Rule + "\n" + tooltip.Delayed;
		}
		if (still && tooltip.DelayedExtra.Length > 0)
		{
			if (text.Length > 0)
			{
				text += "\n" + Rule;
			}
			text += "\n" + tooltip.DelayedExtra;
		}
		return Trim(text);
	}

	/// <summary>The text without newlines, tabs, carriage returns and spaces at either end.</summary>
	public static string Trim(string text)
	{
		return text.Trim(Blank);
	}

	/// <summary>
	/// A button's tooltip with its shortcut in <see cref="CTooltip.DelayedExtra"/> - the
	/// localised <c>SHORTCUT</c> and the shortcut as the file writes it - where that is empty
	/// and the button has one.
	/// </summary>
	public static CTooltip WithShortcut(CTooltip tooltip, string shortcut, string label)
	{
		if (shortcut.Length == 0 || tooltip.DelayedExtra.Length > 0)
		{
			return tooltip;
		}
		return tooltip with { DelayedExtra = label + shortcut };
	}

	/// <summary>How wide a tooltip's lines may be: the box's most, less three times its border.</summary>
	public static int WrapWidth(int maxWidth, int border)
	{
		return maxWidth - 3 * border;
	}
}
