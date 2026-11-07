using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Map;

/// <summary>
/// The near map's province names (<c>docs/mechanics/mapdraw.md</c>, section 4; <c>reversing/findings/FINDINGS-farmap.md</c>,
/// B.7): below camera height 100, in every mode, each province's name in the font called <c>tahoma_60</c>, laid out by
/// <see cref="CProvinceNameLayout"/> on its text position. A name is shown only while it holds one of the 1000 slots: each
/// tile in view, in the tiles' order, gives its provinces' names a free slot, and they fade in over 1.5 seconds; a tile that
/// leaves the view frees its names' slots, so they fade in again when it comes back [exe]. Every province has a name, land
/// and sea, one positions.txt gives no text position at the centre of its bounding box.
/// </summary>
public partial class CProvinceNames : Node2D
{
	public const string FontName = "tahoma_60";

	/// <summary>Names are drawn and their fade runs only under this height [exe].</summary>
	private const float NameHeight = 100f;

	/// <summary>The view's tiles are kept up to date under this height, the original's highest less 30 [exe].</summary>
	private const float TileHeight = 970f;

	private const int Slots = 1000;

	/// <summary>Alpha a second, so 1.5 seconds to full [exe].</summary>
	private const double FadeRate = 170.0;

	private CMap _map = null!;
	private CGameState _state = null!;
	private CGuiFonts _fonts = null!;
	private int _columns;

	/// <summary>Each tile's provinces, by id.</summary>
	private List<int>[] _tileProvinces = Array.Empty<List<int>>();

	/// <summary>The provinces holding a slot: when they took it and their letters, laid out as they took it.</summary>
	private readonly Dictionary<int, (double Start, List<CMapNameLetter> Letters)> _held = new();

	private HashSet<int> _visible = new();
	private bool _shown;
	private double _clock;
	private (CGuiFont Font, CFontDescriptor Descriptor, Color Ink)? _font;
	private bool _fontLooked;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CMap map, CGameState state, CGuiFonts fonts)
	{
		_map = map;
		_state = state;
		_fonts = fonts;
		_columns = map.Width / CQuadTile.Size + 1;
		int rows = map.Height / CQuadTile.Size + 1;
		_tileProvinces = Enumerable.Range(0, _columns * rows).Select(_ => new List<int>()).ToArray();
		// A province's tile by the middle of its bounding box, as the map's meshes are given out (FINDINGS-mapdraw.md, A.1).
		for (int id = 1; id < map.ProvinceCount; id++)
		{
			CProvinceShape shape = map.Shapes[id];
			if (shape.IsEmpty)
			{
				continue;
			}
			int tile = shape.CentreX / CQuadTile.Size + _columns * (shape.CentreY / CQuadTile.Size);
			if (tile >= 0 && tile < _tileProvinces.Length)
			{
				_tileProvinces[tile].Add(id);
			}
		}
	}

	/// <summary>
	/// The view as the 2D camera has it: the tiles in view kept under height 970, their names given slots, faded and drawn
	/// under 100.
	/// </summary>
	public void SetView(Vector2 centre, float zoom, Vector2 screen, double delta)
	{
		_clock += delta;
		float height = screen.Y / zoom / (2f * Mathf.Tan(CMapSurfaceCamera.FieldOfView / 2f));
		if (height < TileHeight)
		{
			HashSet<int> visible = VisibleTiles(centre, zoom, screen);
			// A tile gone from view gives its names' slots back.
			foreach (int tile in _visible)
			{
				if (!visible.Contains(tile))
				{
					foreach (int province in _tileProvinces[tile])
					{
						_held.Remove(province);
					}
				}
			}
			_visible = visible;
		}
		bool shown = height < NameHeight;
		if (shown)
		{
			TakeSlots();
		}
		if (shown || shown != _shown)
		{
			_shown = shown;
			QueueRedraw();
		}
	}

	/// <summary>Each tile in view, in the tiles' order, its names taking free slots until none is left [exe].</summary>
	private void TakeSlots()
	{
		if (Font() is not { } font)
		{
			return;
		}
		foreach (int tile in _visible.OrderBy(t => t))
		{
			foreach (int province in _tileProvinces[tile])
			{
				if (_held.ContainsKey(province))
				{
					continue;
				}
				if (_held.Count >= Slots)
				{
					return;
				}
				string name = province < _state.Provinces.Count ? _state.Provinces[province].Name : "";
				_held[province] = (_clock, CProvinceNameLayout.Lay(name, font.Descriptor, _map.ProvinceLabels[province]));
			}
		}
	}

	/// <summary>The tiles under the view, the map wrapping east and west, x a column and y counted from the south.</summary>
	private HashSet<int> VisibleTiles(Vector2 centre, float zoom, Vector2 screen)
	{
		float halfWidth = screen.X / zoom / 2f, halfHeight = screen.Y / zoom / 2f;
		float worldX = centre.X - 0.5f, worldZ = _map.Height - 0.5f - centre.Y;
		int rows = _tileProvinces.Length / _columns;
		int rowFrom = Math.Max(0, (int)MathF.Floor((worldZ - halfHeight) / CQuadTile.Size));
		int rowTo = Math.Min(rows - 1, (int)MathF.Floor((worldZ + halfHeight) / CQuadTile.Size));
		var columns = new HashSet<int>();
		for (int x = (int)MathF.Floor(worldX - halfWidth); x <= (int)MathF.Ceiling(worldX + halfWidth); x++)
		{
			columns.Add(((x % _map.Width) + _map.Width) % _map.Width / CQuadTile.Size);
			if (columns.Count >= _columns)
			{
				break;
			}
		}
		var tiles = new HashSet<int>();
		for (int row = rowFrom; row <= rowTo; row++)
		{
			foreach (int column in columns)
			{
				tiles.Add(column + _columns * row);
			}
		}
		return tiles;
	}

	private (CGuiFont Font, CFontDescriptor Descriptor, Color Ink)? Font()
	{
		if (!_fontLooked)
		{
			_fontLooked = true;
			if (_fonts.Find(FontName) is { Font: not null } font && _fonts.Descriptor(font.FontName) is { } descriptor)
			{
				// The atlas's own ink, as the country names take it: the font's colour key is not read (B.7.3).
				_font = (font, descriptor, _fonts.Find(font.FontName)?.Color ?? Colors.Black);
			}
		}
		return _font;
	}

	public override void _Draw()
	{
		if (!_shown || Font() is not { } font)
		{
			return;
		}
		Vector2 view = GetViewport().GetCamera2D()?.GetScreenCenterPosition() ?? Vector2.Zero;
		foreach ((int province, (double start, List<CMapNameLetter> letters)) in _held)
		{
			// Alpha nought is not drawn: a name first shows the frame after it took its slot [exe].
			int alpha = (int)Math.Min(255, Math.Truncate((_clock - start) * FadeRate));
			if (alpha <= 0 || letters.Count == 0)
			{
				continue;
			}
			// The copy of the map nearest the view, by the name's first letter.
			float copy = MathF.Round((view.X - (letters[0].X + 0.5f)) / _map.Width) * _map.Width;
			Color ink = font.Ink with { A = font.Ink.A * alpha / 255f };
			foreach (CMapNameLetter letter in letters)
			{
				DrawLetter(letter, font.Font, font.Descriptor, ink, copy);
			}
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	/// <summary>A letter drawn so that its glyph's box stands where the layout put it, as the country names' are.</summary>
	private void DrawLetter(CMapNameLetter letter, CGuiFont font, CFontDescriptor descriptor, Color ink, float copy)
	{
		CFontGlyph glyph = descriptor.Glyph(letter.Character) ?? default;
		float k = letter.Scale;
		float upX = -letter.AlongZ, upZ = letter.AlongX;
		float along = -(glyph.XOffset + glyph.Width / 2f) * k;
		float up = (glyph.YOffset + glyph.Height / 2f - descriptor.Base) * k;
		float x = letter.X + letter.AlongX * along + upX * up;
		float z = letter.Z + letter.AlongZ * along + upZ * up;
		var origin = new Vector2(x + 0.5f + copy, _map.Height - 0.5f - z);
		float angle = Mathf.Atan2(-letter.AlongZ, letter.AlongX);
		DrawSetTransform(origin, angle, new Vector2(k * font.Narrowing, k));
		string shown = Interface.Text.CWindows1252.ToDisplay(letter.Character.ToString());
		DrawString(font.Font!, Vector2.Zero, shown, HorizontalAlignment.Left, -1f, font.Size, ink);
	}
}
