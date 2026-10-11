using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// The original's flag in text: <c>@</c> and the three letters after it draw that country's
/// flag, 23 by 11, from 3 below the top of its line, and the pen moves on 24. See
/// <c>docs/mechanics/tooltips.md</c>, section 7, and
/// <c>reversing/findings/FINDINGS-provincetooltip.md</c>, section 8.
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

	/// <summary>A run of text cut into words and flags. The letters after an <c>@</c> are its tag, however many of three there are.</summary>
	public static List<CTextPiece> Split(string text)
	{
		var pieces = new List<CTextPiece>();
		var words = new StringBuilder();
		for (int index = 0; index < text.Length; index++)
		{
			if (text[index] != Mark)
			{
				words.Append(text[index]);
				continue;
			}
			if (words.Length > 0)
			{
				pieces.Add(new CTextPiece(words.ToString(), null));
				words.Clear();
			}
			int length = Math.Min(TagLength, text.Length - index - 1);
			pieces.Add(new CTextPiece("", text.Substring(index + 1, length)));
			index += length;
		}
		if (words.Length > 0)
		{
			pieces.Add(new CTextPiece(words.ToString(), null));
		}
		return pieces;
	}

	/// <summary>How wide a text is, its words as <paramref name="measure"/> has them and each flag 24.</summary>
	public static float Measure(string text, Func<string, float> measure)
	{
		float width = 0f;
		foreach (CTextPiece piece in Split(text))
		{
			width += piece.Tag is null ? measure(piece.Text) : Advance;
		}
		return width;
	}
}
