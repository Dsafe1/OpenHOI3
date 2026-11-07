using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// A run of text cut into words and the pictures the original draws in text: a country's
/// flag after <c>@</c> (<see cref="CFlagIcons"/>), a unit counter after <c>£</c>
/// (<see cref="CCounterIcons"/>) and a strength bar for <c>{</c> and two digits (<see cref="CStrengthIcons"/>).
/// </summary>
public static class CTextIcons
{
	/// <summary>
	/// The pieces of a run. The letters after an <c>@</c> are its tag, however many of three
	/// there are; the letters and underscores after a <c>£</c> are its counter's name.
	/// </summary>
	public static List<CTextPiece> Split(string text)
	{
		var pieces = new List<CTextPiece>();
		var words = new StringBuilder();
		for (int index = 0; index < text.Length; index++)
		{
			char character = text[index];
			int? strength = character == CStrengthIcons.Mark ? CStrengthIcons.FrameAt(text, index) : null;
			if (character != CFlagIcons.Mark && character != CCounterIcons.Mark && strength is null)
			{
				words.Append(character);
				continue;
			}
			if (words.Length > 0)
			{
				pieces.Add(new CTextPiece(words.ToString(), null));
				words.Clear();
			}
			if (strength is { } frame)
			{
				pieces.Add(new CTextPiece("", null, null, frame));
				index += 2;
				continue;
			}
			if (character == CFlagIcons.Mark)
			{
				int length = Math.Min(CFlagIcons.TagLength, text.Length - index - 1);
				pieces.Add(new CTextPiece("", text.Substring(index + 1, length)));
				index += length;
				continue;
			}
			int end = index + 1;
			while (end < text.Length && CCounterIcons.IsNameCharacter(text[end]))
			{
				end++;
			}
			pieces.Add(new CTextPiece("", null, text.Substring(index + 1, end - index - 1)));
			index = end - 1;
		}
		if (words.Length > 0)
		{
			pieces.Add(new CTextPiece(words.ToString(), null));
		}
		return pieces;
	}

	/// <summary>How wide a text is, its words as <paramref name="measure"/> has them, each flag 24, each counter 30 and each bar 8.</summary>
	public static float Measure(string text, Func<string, float> measure)
	{
		float width = 0f;
		foreach (CTextPiece piece in Split(text))
		{
			width += piece.Tag is not null ? CFlagIcons.Advance
				: piece.Counter is not null ? CCounterIcons.Advance
				: piece.Strength is not null ? CStrengthIcons.Advance
				: measure(piece.Text);
		}
		return width;
	}
}
