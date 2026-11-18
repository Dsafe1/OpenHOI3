using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Map;

/// <summary>
/// The far view's country names (<c>docs/mechanics/mapnames.md</c>): one for every country owning land but the rebels, laid
/// over the land it controls joined to its capital by <see cref="CCountryMapNameLayout"/>, in the font of its faction's
/// icon - its letters drawn from the typeface the font stands for, at the place and the size its glyphs have in the
/// original. A name is laid out again only where its land, capital, faction or text has changed, and only once it is to
/// be seen.
/// </summary>
public partial class CCountryMapNames : Node2D
{
	/// <summary>Its redraws, put off while the game's hours run beside the frame (<see cref="CHeldRedraw"/>).</summary>
	public CHeldRedraw Redraw => _redraw ??= new CHeldRedraw(this);

	private CHeldRedraw? _redraw;

	/// <summary>The fonts the names are written in, by the faction's icon: none, then the three of TFH [exe the names].</summary>
	private static readonly string[] FontNames = { "neutral_country", "comintern_country", "axis_country", "allies_country" };

	private const string Rebels = "REB";

	private CMap _map = null!;
	private CGameState _state = null!;
	private CGuiFonts _fonts = null!;
	private CMapNameWorld _world = null!;

	private readonly Dictionary<CCountryTag, (int Signature, int Font, CCountryMapName? Name)> _names = new();
	private readonly Dictionary<CCountryTag, (int Signature, int Font, HashSet<int> Piece, string Text)> _wanted = new();
	private readonly (CGuiFont? Font, CFontDescriptor? Descriptor, Color Ink)?[] _fontCache = new (CGuiFont?, CFontDescriptor?, Color)?[FontNames.Length];


	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CMap map, CGameState state, CGuiFonts fonts)
	{
		_map = map;
		_state = state;
		_fonts = fonts;

		ushort[] raster = map.Raster.ToArray();
		_world = new CMapNameWorld
		{
			Width = map.Width,
			Height = map.Height,
			ProvinceAt = (x, y) => raster[y * map.Width + x],
			IsLand = id => id > 0 && id < state.Provinces.Count && !state.Provinces[id].IsSea,
			Controller = id => id > 0 && id < state.Provinces.Count && state.Provinces[id].Controller is { } tag
				? (state.Country(tag)?.Index ?? -1) + 1 : 0,
			Shape = id => id > 0 && id < map.ProvinceCount ? map.Shapes[id] : default,
			Neighbours = id => id > 0 && id < map.ProvinceCount ? map.Edges[id].Select(e => e.To) : Enumerable.Empty<int>(),
		};
	}

	/// <summary>
	/// Which names are wanted and what each rests on - the piece of land, the font and the text - so that a name whose
	/// signature changed is laid out again when next drawn (<c>mapnames.md</c>, section 4).
	/// </summary>
	public void Refresh()
	{
		var owning = new HashSet<CCountryTag>();
		foreach (CProvince province in _state.Provinces)
		{
			if (province.Owner is { } owner)
			{
				owning.Add(owner);
			}
		}
		_wanted.Clear();
		foreach (CCountry country in _state.Countries)
		{
			if (!owning.Contains(country.Tag) || country.Tag.Value == Rebels)
			{
				continue;
			}
			HashSet<int> piece = CCountryMapNameLayout.Piece(_world, country.Capital, country.Index + 1);
			int icon = CAlignment.FactionGroup(_state, country)?.FactionIcon ?? 0;
			// An icon past the table reads past the original's table (FINDINGS-farmap.md, B.1); here it takes no faction's font.
			int font = icon >= 0 && icon < FontNames.Length ? icon : 0;
			var hash = new HashCode();
			foreach (int id in piece.OrderBy(id => id))
			{
				hash.Add(id);
			}
			hash.Add(font);
			hash.Add(country.Name);
			_wanted[country.Tag] = (hash.ToHashCode(), font, piece, country.Name);
		}
		foreach (CCountryTag gone in _names.Keys.Where(tag => !_wanted.ContainsKey(tag)).ToList())
		{
			_names.Remove(gone);
		}
		Redraw.Request();
	}

	private (CGuiFont? Font, CFontDescriptor? Descriptor, Color Ink) Font(int index)
	{
		if (_fontCache[index] is { } known)
		{
			return known;
		}
		CGuiFont? font = _fonts.Find(FontNames[index]);
		CFontDescriptor? descriptor = font is null ? null : _fonts.Descriptor(font.FontName);
		// The atlas's own ink: the font's colour key is not read on this path (FINDINGS-farmap.md, B.1).
		Color ink = font is null ? Colors.Black : _fonts.Find(font.FontName)?.Color ?? Colors.Black;
		(CGuiFont?, CFontDescriptor?, Color) made = (font, descriptor, ink);
		_fontCache[index] = made;
		return made;
	}

	public override void _Draw()
	{
		foreach ((CCountryTag tag, (int signature, int fontIndex, HashSet<int> piece, string text)) in _wanted)
		{
			(CGuiFont? font, CFontDescriptor? descriptor, Color ink) = Font(fontIndex);
			if (font?.Font is null || descriptor is null)
			{
				continue;
			}
			if (!_names.TryGetValue(tag, out var laid) || laid.Signature != signature)
			{
				string shown = font.Effect ? CCountryMapNameLayout.Upper(text) : text;
				laid = (signature, fontIndex, CCountryMapNameLayout.Lay(_world, piece, shown, descriptor));
				_names[tag] = laid;
			}
			if (laid.Name is { } name)
			{
				foreach (float copy in new[] { -_map.Width, 0f, _map.Width })
				{
					DrawName(name, font, descriptor, ink, copy);
				}
			}
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	/// <summary>
	/// Each letter drawn so that its glyph's box stands where the layout put it: the pen and the line the letters stand on
	/// found from the box's middle by the glyph's offsets, turned along the way the name reads, in the map's 2D space -
	/// a world point (x, z) is the 2D point (x + 0.5, H − 0.5 − z) (<c>mapdraw.md</c>, section 3).
	/// </summary>
	private void DrawName(CCountryMapName name, CGuiFont font, CFontDescriptor descriptor, Color ink, float copy)
	{
		foreach (CMapNameLetter letter in name.Letters)
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
			DrawString(font.Font, Vector2.Zero, shown, HorizontalAlignment.Left, -1f, font.Size, ink);
		}
	}
}
