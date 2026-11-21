using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Map;

/// <summary>
/// The messages written on the map (<c>docs/mechanics/messages.md</c>, section 6): each a text over its province's
/// middle, in the bitmapfont named <c>AnimatedMapText</c> and its type's colour, that rises, fades over the last
/// quarter of its rise while it grows to twice its size, and is gone - its life counted in frames, the game's speed and
/// one a frame, the clock running or not. Several over one province overlap. Seen only below the far view. A child of
/// the map, so its coordinates are map pixels.
/// </summary>
public partial class CMapTexts : Node2D
{
	/// <summary>The map's world units in a map pixel: its world is counted in half pixels.</summary>
	private const float WorldUnit = 0.5f;

	/// <summary>The camera height the texts are seen under [exe].</summary>
	public const float SeenBelow = 500f;

	private sealed record Shown(Vector2 Place, string Row, float Start, CMapTextRise Rise, Color Ink);

	private readonly List<Shown> _texts = new();
	private CMap _map = null!;
	private CGuiFonts _fonts = null!;
	private CAnimatedMapTextTypes _types = null!;
	private (CGuiFont Font, CFontDescriptor Descriptor)? _font;
	private bool _fontLooked;

	/// <summary>The game's speed, nought to four: what a frame adds to every text's time, and one.</summary>
	public Func<int> GameSpeed { get; set; } = () => 0;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CMap map, CGuiFonts fonts, CAnimatedMapTextTypes types)
	{
		_map = map;
		_fonts = fonts;
		_types = types;
	}

	/// <summary>
	/// A message's text put over a province's middle. Nothing where the files declare no <c>MessageMapText</c> - the
	/// original has no test for that and would fail there - or no font of the name; an empty text is still made.
	/// </summary>
	public void Add(Vector2 middle, string text)
	{
		if (_types.Find(CAnimatedMapTextType.Message) is not { } type || Font() is not { } font)
		{
			return;
		}
		string cut = CMapTextLayout.Cut(text);
		float widest = CMapTextLayout.WidestLine(cut, run => CTextIcons.Measure(run, font.Font.Width));
		// Ours: a colour escape is taken out, where the original's draw colours by it.
		string row = CWindows1252.ToDisplay(CColouredText.Strip(CMapTextLayout.Row(cut)));
		_texts.Add(new Shown(middle, row, CMapTextLayout.Start(widest), new CMapTextRise(type.Speed), type.Color.ToGodot()));
		QueueRedraw();
	}

	/// <summary>A frame: every text a step older, the finished taken away, and all hidden in the far view.</summary>
	public void SetView(float cameraHeight)
	{
		Visible = cameraHeight < SeenBelow;
		if (_texts.Count == 0)
		{
			return;
		}
		int speed = GameSpeed();
		foreach (Shown text in _texts)
		{
			text.Rise.Advance(speed);
		}
		_texts.RemoveAll(text => text.Rise.Done);
		QueueRedraw();
	}

	private (CGuiFont Font, CFontDescriptor Descriptor)? Font()
	{
		if (!_fontLooked)
		{
			_fontLooked = true;
			if (_fonts.Find(CAnimatedMapTextType.FontName) is { Font: not null } font && _fonts.Descriptor(font.FontName) is { } descriptor)
			{
				_font = (font, descriptor);
			}
		}
		return _font;
	}

	public override void _Draw()
	{
		if (_texts.Count == 0 || Font() is not { } font)
		{
			return;
		}
		Vector2 view = GetViewport().GetCamera2D()?.GetScreenCenterPosition() ?? Vector2.Zero;
		foreach (Shown text in _texts)
		{
			// The copy of the map nearest the view.
			float copy = MathF.Round((view.X - text.Place.X) / _map.Width) * _map.Width;
			// Ours: straight up the screen by its rise, the camera's tilt not asked.
			var origin = new Vector2(text.Place.X + copy, text.Place.Y - text.Rise.Rise * WorldUnit);
			float k = CMapTextLayout.FontPixel * text.Rise.Scale * WorldUnit;
			DrawSetTransform(origin, 0f, new Vector2(k * font.Font.Narrowing, k));
			// A line's top stands fifteen font pixels above the point; the letters hang from it by the font's base.
			var pen = new Vector2(text.Start / font.Font.Narrowing, font.Descriptor.Base - CMapTextLayout.LineTop);
			DrawString(font.Font.Font!, pen, text.Row, HorizontalAlignment.Left, -1f, font.Font.Size,
				text.Ink with { A = text.Ink.A * text.Rise.Alpha });
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
