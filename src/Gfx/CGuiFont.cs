using Godot;
using OpenHOI3.Interface;

namespace OpenHOI3.Gfx;

/// <summary>
/// One <c>bitmapfont</c> entry from a <c>.gfx</c> file, ready to draw with. The logical
/// name is what the GUI asks for; the font name selects a descriptor in
/// <c>gfx/fonts</c>, and the colour tints it - several logical fonts differ only by
/// colour.
///
/// The original's glyph texture is not what is drawn. The typeface the descriptor names
/// is, as outlines, at the size that makes text as wide as the original's glyphs made
/// it - so it fits the boxes the interface files give it and stays sharp on any screen.
/// </summary>
public sealed class CGuiFont
{
	public string Name { get; init; } = "";
	public string FontName { get; init; } = "";
	/// <summary>
	/// The colour to draw it in: the one its declaration gives, times the colour the
	/// original's glyphs were painted in. Some of those are white and take their colour
	/// from the declaration, and some carry it themselves.
	/// </summary>
	public Color Color { get; set; } = Colors.White;

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

	/// <summary>How wide a text comes out, in units of the reference canvas.</summary>
	public float Width(string text)
	{
		return Font is null ? 0f
			: Font.GetStringSize(text, HorizontalAlignment.Left, -1f, Size).X * Narrowing;
	}

	/// <summary>
	/// Draws a text on a canvas item with its letters standing on the point given -
	/// and then one unit right and one up of it, as the original draws every glyph.
	/// </summary>
	public void Draw(CanvasItem item, Vector2 baseline, string text, Color colour)
	{
		if (Font is null)
		{
			return;
		}
		baseline += new Vector2(CGuiLayout.GlyphShiftX, CGuiLayout.GlyphShiftY);
		item.DrawSetTransform(baseline, 0f, new Vector2(Narrowing, 1f));
		item.DrawString(Font, Vector2.Zero, text, HorizontalAlignment.Left, -1f, Size, colour);
		item.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
