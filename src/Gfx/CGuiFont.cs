using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;

namespace OpenHOI3.Gfx;

/// <summary>
/// One <c>bitmapfont</c> entry from a <c>.gfx</c> file, ready to draw with. The logical
/// name is what the GUI asks for; the font name selects a descriptor in
/// <c>gfx/fonts</c>, and the colour tints it - several logical fonts differ only by
/// colour.
///
/// A small font's glyph texture is not what is drawn. The typeface the descriptor names
/// is, as outlines, at the size that makes text as wide as the original's glyphs made
/// it - so it fits the boxes the interface files give it and stays sharp on any screen.
/// A large font is drawn from its own glyphs, made into distance fields
/// (<c>docs/mechanics/interface.md</c>, section 6).
/// </summary>
public sealed class CGuiFont
{
	public string Name { get; init; } = "";
	public string FontName { get; init; } = "";

	/// <summary>
	/// Where a <c>bitmapfont</c> entry names its <c>fontname</c> - the <c>.gfx</c> file and the line - for a report of a
	/// fault in it (<c>docs/mechanics/logs.md</c>, section 4); empty and nought for a font not declared so.
	/// </summary>
	public string File { get; init; } = "";
	public int Line { get; init; }

	/// <summary>
	/// The entry's <c>effect</c>: on unless it says <c>effect = no</c>, as the original's default. An edit box in a font
	/// with it on refuses seven characters (<c>docs/mechanics/interface.md</c>, section 8).
	/// </summary>
	public bool Effect { get; init; } = true;

	/// <summary>
	/// Whether it is made from a <c>font = { }</c> entry - an installed typeface the original draws with Direct3D's text
	/// drawing at the entry's <c>height</c> - and not from a <c>bitmapfont</c>. Such a font's glyphs are not moved the unit
	/// right and up a bitmap font's are, that being the bitmap font's own half pixel (<c>interface.md</c>, section 6).
	/// </summary>
	public bool TrueType { get; init; }
	/// <summary>
	/// The colour to draw it in: the one its declaration gives, times the colour the
	/// original's glyphs were painted in. Some of those are white and take their colour
	/// from the declaration, and some carry it themselves.
	/// </summary>
	public Color Color { get; set; } = Colors.White;

	/// <summary>Its entry's <c>color_override</c>: what an escape letter stands for in this font before the shared table.</summary>
	public CFontColourTable? OwnColours { get; init; }

	/// <summary>The table every font's <c>colorcodes</c> fills, set as the font is handed out.</summary>
	public CFontColourTable? SharedColours { get; set; }

	/// <summary>
	/// The colour of a run of text after the escape letter given: <paramref name="plain"/> before any escape and after
	/// <c>§!</c>, the letter's where this font or the shared table has it, and <paramref name="current"/> - the colour in
	/// force - where neither does (<see cref="CTextColours"/>).
	/// </summary>
	public Color RunColour(char letter, Color current, Color plain)
	{
		if (CTextColours.IsPlain(letter))
		{
			return plain;
		}
		return CTextColours.TryGet(letter, OwnColours, SharedColours, out CColor colour) ? colour.ToGodot() : current;
	}

	/// <summary>Whether an escape letter changes the colour in this font: <c>§!</c>, or a letter one of its tables has.</summary>
	public bool ChangesColour(char letter)
	{
		return CTextColours.IsPlain(letter) || CTextColours.TryGet(letter, OwnColours, SharedColours, out _);
	}

	/// <summary>The typeface. Null until the font is first asked for, and after that only when it could not be made.</summary>
	public Font? Font { get; set; }

	/// <summary>The size to draw it at, in units of the reference canvas.</summary>
	public int Size { get; set; }

	/// <summary>From the top of a line down to the line its letters stand on, in the same units.</summary>
	public float Ascent { get; set; }

	/// <summary>From the top of one line to the top of the next.</summary>
	public float LineHeight { get; set; }

	/// <summary>
	/// How much of its own width the typeface is drawn at. One for the typeface the
	/// original used; less for a stand-in whose letters are wider, so that text still
	/// fits where the interface files put it.
	/// </summary>
	public float Narrowing { get; set; } = 1f;

	/// <summary>
	/// The advance every character of the original's font shared, or nought: such a font is drawn a character to a cell
	/// of that width, each letter in the middle of its own, so that the date widget's figures stand in the boxes of the
	/// picture behind them (<c>docs/mechanics/interface.md</c>, section 6).
	/// </summary>
	public int FixedAdvance { get; set; }

	/// <summary>
	/// How wide the original's glyph of each character was, for a font of fixed width: a letter of the typeface wider
	/// than that is narrowed to it, so that an M keeps to its cell as the original's does (<c>interface.md</c>, section 6).
	/// </summary>
	public Func<char, int>? GlyphWidth { get; set; }

	/// <summary>How wide a text comes out, in units of the reference canvas.</summary>
	public float Width(string text)
	{
		if (FixedAdvance > 0)
		{
			return text.Length * FixedAdvance;
		}
		return Font is null ? 0f
			: Font.GetStringSize(Interface.Text.CWindows1252.ToDisplay(text), HorizontalAlignment.Left, -1f, Size).X * Narrowing;
	}

	/// <summary>
	/// Draws a text on a canvas item with its letters standing on the point given -
	/// and then one unit right and one up of it, as the original draws every glyph of a bitmap font.
	/// </summary>
	public void Draw(CanvasItem item, Vector2 baseline, string text, Color colour)
	{
		if (Font is null)
		{
			return;
		}
		if (!TrueType)
		{
			baseline += new Vector2(CGuiLayout.GlyphShiftX, CGuiLayout.GlyphShiftY);
		}
		// The text is held as its Windows-1252 bytes; its curly quotes and dashes are drawn as themselves.
		string shown = Interface.Text.CWindows1252.ToDisplay(text);
		if (FixedAdvance > 0)
		{
			for (int index = 0; index < text.Length; index++)
			{
				string letter = shown[index].ToString();
				float raw = Font.GetStringSize(letter, HorizontalAlignment.Left, -1f, Size).X;
				float narrowing = Narrowing;
				int glyph = GlyphWidth?.Invoke(text[index]) ?? 0;
				if (glyph > 0 && raw * narrowing > glyph)
				{
					narrowing = glyph / raw;
				}
				float width = raw * narrowing;
				float x = baseline.X + index * FixedAdvance + Mathf.Round((FixedAdvance - width) / 2f);
				item.DrawSetTransform(new Vector2(x, baseline.Y), 0f, new Vector2(narrowing, 1f));
				item.DrawString(Font, Vector2.Zero, letter, HorizontalAlignment.Left, -1f, Size, colour);
			}
			item.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
			return;
		}
		item.DrawSetTransform(baseline, 0f, new Vector2(Narrowing, 1f));
		item.DrawString(Font, Vector2.Zero, shown, HorizontalAlignment.Left, -1f, Size, colour);
		item.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
