using System;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;

namespace OpenHOI3.Loading;

/// <summary>
/// <c>Loading_Font</c>, the font of the loading screen's line and of the notices drawn over a game
/// (<c>docs/mechanics/loadingscreen.md</c>, sections 2 and 6): found among the <c>font</c> entries first, then the
/// bitmap fonts, as the original looks it up. A <c>font</c> entry's typeface is drawn in the shipped one that stands
/// for it, at the size whose cell is the entry's <c>height</c>, as Windows takes a positive height.
/// </summary>
public sealed class CLoadingFont
{
	public const string FontName = "Loading_Font";

	private readonly Font? _font;
	private readonly int _fontSize;
	private readonly Color _fontColour = Colors.White;
	private readonly CGuiFont? _bitmapFont;

	public CLoadingFont(CDirectorySettings directories)
	{
		if (CTrueTypeFontReader.Read(directories).TryGetValue(FontName, out CTrueTypeFont? entry))
		{
			_font = new CShippedTypefaces().For(entry.FontName, bold: false, italic: false, out _);
			if (_font is not null && entry.Height > 0)
			{
				const int probe = 100;
				float cell = _font.GetHeight(probe);
				_fontSize = Math.Max(1, (int)Math.Round(entry.Height * probe / cell));
				_fontColour = entry.Color.ToGodot();
				return;
			}
			_font = null;
		}
		_bitmapFont = CGuiFonts.Load(directories).Find(FontName);
	}

	/// <summary>A line centred across and down a box, in the text's own bytes.</summary>
	public void DrawCentred(CanvasItem on, Vector2 boxAt, Vector2 boxSize, string line)
	{
		if (line.Length == 0)
		{
			return;
		}
		if (_font is not null)
		{
			float lineWidth = _font.GetStringSize(line, HorizontalAlignment.Left, -1f, _fontSize).X;
			float cell = _font.GetHeight(_fontSize);
			var baseline = new Vector2(Mathf.Round(boxAt.X + (boxSize.X - lineWidth) / 2f),
				Mathf.Round(boxAt.Y + (boxSize.Y - cell) / 2f + _font.GetAscent(_fontSize)));
			on.DrawString(_font, baseline, Interface.Text.CWindows1252.ToDisplay(line), HorizontalAlignment.Left, -1f, _fontSize,
				_fontColour);
		}
		else if (_bitmapFont is not null)
		{
			float lineWidth = _bitmapFont.Width(line);
			var baseline = new Vector2(Mathf.Round(boxAt.X + (boxSize.X - lineWidth) / 2f),
				Mathf.Round(boxAt.Y + (boxSize.Y - _bitmapFont.LineHeight) / 2f + _bitmapFont.Ascent));
			_bitmapFont.Draw(on, baseline, line, _bitmapFont.Color);
		}
	}
}
