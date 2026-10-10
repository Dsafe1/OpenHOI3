using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Map;

/// <summary>
/// What the air map mode draws over the map: a ring of range round the player's air bases
/// for each range of aircraft there, the air bases and anti-aircraft guns as large pictures
/// sized by their level, and the icons of the past week's air battles and bombings. Shown
/// only while the mode is on. See <c>docs/mechanics/airmap.md</c>.
/// </summary>
public partial class CAirMapLayer : Node2D
{
	private sealed record Picture(CBillboardType Type, Texture2D Texture);

	private readonly record struct Large(int Province, string Picture, float X, float Y, float Size, int Frame);

	private CMap _map = null!;
	private CGameState _state = null!;
	private CGuiContext _gui = null!;
	private CBillboardTypes _billboards = null!;
	private readonly Dictionary<string, Picture?> _pictures = new(StringComparer.OrdinalIgnoreCase);
	private Texture2D? _ring;
	private bool _ringLoaded;

	private List<CAirBaseCircle> _circles = new();
	private readonly List<Large> _large = new();
	private CAirMapCount[] _counts = Array.Empty<CAirMapCount>();
	private Rect2 _view;

	/// <summary>The country being played, whose air bases are ringed and whose battles are sorted into ours and theirs.</summary>
	public CCountryTag? Player { get; set; }

	public void Setup(CMap map, CGameState state, CGuiContext gui, CBillboardTypes billboards)
	{
		_map = map;
		_state = state;
		_gui = gui;
		_billboards = billboards;
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		Visible = false;
	}

	/// <summary>Works out again everything the layer shows - what the original does each day in this mode.</summary>
	public void Refresh(CUnit? selected)
	{
		_counts = CAirMapIcons.Count(_state, Player);
		_large.Clear();
		var atHome = new HashSet<int>();
		foreach (CCountry country in _state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (CMapBuildings.IsAtAirfield(unit))
				{
					atHome.Add(unit.Location);
				}
			}
		}
		for (int id = 1; id < _state.Provinces.Count && id < _map.ProvinceCount; id++)
		{
			CProvince province = _state.Provinces[id];
			AddLarge(province, CMovementRules.AirBase, CAirMapIcons.AirBasePicture, atHome.Contains(id) ? 1 : 0);
			AddLarge(province, CAirMapIcons.AntiAir, CAirMapIcons.AntiAirPicture, 0);
		}
		RefreshCircles(selected);
	}

	private void AddLarge(CProvince province, string building, string picture, int frame)
	{
		int level = CAirMapIcons.Level(_state, province, building, Player);
		if (level < 1)
		{
			return;
		}
		(float x, float y) = CMapBuildings.PlaceOf(_map.BuildingPositions, _map.Positions, province.Id, building);
		_large.Add(new Large(province.Id, picture, x, y, CAirMapIcons.Size(level), frame));
	}

	/// <summary>Works out the rings again, which depend on what is selected.</summary>
	public void RefreshCircles(CUnit? selected)
	{
		_circles = CAirBaseCircles.Build(_state, _map, Player, selected);
		QueueRedraw();
	}

	public void ViewChanged(Rect2 view)
	{
		_view = view;
		QueueRedraw();
	}

	private Picture? PictureOf(string name)
	{
		if (!_pictures.TryGetValue(name, out Picture? picture))
		{
			picture = _billboards.Find(name) is { TextureFile.Length: > 0 } type && _gui.Textures.Exists(type.TextureFile)
				&& _gui.Textures.LoadFromGamePath(type.TextureFile) is { } texture
				? new Picture(type, texture)
				: null;
			_pictures[name] = picture;
		}
		return picture;
	}

	/// <summary>
	/// The ring's picture as white whose alpha is the original's grey: the file holds one
	/// channel, which its shader reads as how much of the ring's colour a point takes.
	/// </summary>
	private Texture2D? Ring()
	{
		if (_ringLoaded)
		{
			return _ring;
		}
		_ringLoaded = true;
		if (_gui.Textures.LoadImage(CAirBaseCircles.Picture) is not { } source)
		{
			return null;
		}
		var picture = (Image)source.Duplicate();
		if (picture.IsCompressed())
		{
			picture.Decompress();
		}
		picture.Convert(Image.Format.Rgba8);
		var ring = Image.CreateEmpty(picture.GetWidth(), picture.GetHeight(), false, Image.Format.Rgba8);
		for (int y = 0; y < picture.GetHeight(); y++)
		{
			for (int x = 0; x < picture.GetWidth(); x++)
			{
				ring.SetPixel(x, y, new Color(1f, 1f, 1f, picture.GetPixel(x, y).R));
			}
		}
		ring.GenerateMipmaps();
		_ring = ImageTexture.CreateFromImage(ring);
		return _ring;
	}

	/// <summary>A point of positions.txt, which counts y north, in this node's coordinates.</summary>
	private Vector2 PlaceOf(float x, float y)
	{
		return new Vector2(x, _map.Height - y);
	}

	public override void _Draw()
	{
		DrawCircles();
		foreach (Large large in _large)
		{
			DrawPicture(large.Picture, PlaceOf(large.X, large.Y), large.Size, large.Frame);
		}
		for (int id = 1; id < _counts.Length && id < _map.Positions.Count; id++)
		{
			CAirMapCount count = _counts[id];
			Vector2 point = PlaceOf(_map.Positions[id].X + 0.5f, _map.Positions[id].Y + 0.5f);
			float step = CAirMapIcons.IconOffset;
			if (count.BombedUs > 0)
			{
				DrawPicture(CAirMapIcons.BombedUsPicture, point + new Vector2(0f, -step), 1f, 0);
			}
			if (count.BombedThem > 0)
			{
				DrawPicture(CAirMapIcons.BombedThemPicture, point + new Vector2(0f, step), 1f, 0);
			}
			if (count.Battles > 0)
			{
				DrawPicture(CAirMapIcons.BattlePicture, point + new Vector2(step, 0f), 1f, 0);
			}
		}
	}

	/// <summary>
	/// Each ring as the original draws it: its picture four times about the base, a quarter
	/// in each quarter, the base at the picture's corner and the radius at its edges.
	/// </summary>
	private void DrawCircles()
	{
		if (_circles.Count == 0 || Ring() is not { } ring)
		{
			return;
		}
		foreach (CAirBaseCircle circle in _circles)
		{
			Color colour = CAirBaseCircles.ColourOf(circle).ToGodot();
			Vector2 centre = PlaceOf(circle.X, circle.Y);
			float radius = circle.Radius;
			for (int copy = -1; copy <= 1; copy++)
			{
				var here = new Vector2(centre.X + copy * _map.Width, centre.Y);
				if (!_view.Grow(radius).HasPoint(here))
				{
					continue;
				}
				foreach (Vector2 mirror in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
				{
					DrawSetTransform(here, 0f, mirror);
					DrawTextureRect(ring, new Rect2(-radius, -radius, radius, radius), false, colour);
				}
			}
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	/// <summary>A picture of mapitems.gfx at a point, as a square of twice its scale times the size a side.</summary>
	private void DrawPicture(string name, Vector2 place, float size, int frame)
	{
		if (PictureOf(name) is not { } picture)
		{
			return;
		}
		float half = picture.Type.Scale * size;
		float frameWidth = picture.Texture.GetWidth() / (float)picture.Type.Frames;
		var source = new Rect2(Math.Min(frame, picture.Type.Frames - 1) * frameWidth, 0f, frameWidth, picture.Texture.GetHeight());
		for (int copy = -1; copy <= 1; copy++)
		{
			var here = new Vector2(place.X + copy * _map.Width, place.Y);
			if (_view.Grow(half).HasPoint(here))
			{
				DrawTextureRectRegion(picture.Texture, new Rect2(here.X - half, here.Y - half, 2f * half, 2f * half), source);
			}
		}
	}

	/// <summary>
	/// The province whose large air base picture is at a point of the map, or 0: what a click
	/// in this mode asks before any counter.
	/// </summary>
	public int AirBaseAt(Vector2 point)
	{
		if (!Visible || PictureOf(CAirMapIcons.AirBasePicture) is not { } picture)
		{
			return 0;
		}
		float x = Mathf.PosMod(point.X, _map.Width);
		int found = 0;
		foreach (Large large in _large)
		{
			if (large.Picture != CAirMapIcons.AirBasePicture)
			{
				continue;
			}
			Vector2 place = PlaceOf(large.X, large.Y);
			float across = Mathf.Abs(Mathf.PosMod(place.X, _map.Width) - x);
			across = Mathf.Min(across, _map.Width - across);
			float reach = picture.Type.Scale * large.Size * CBuildingIcons.BadgeShare;
			if (across * across + (place.Y - point.Y) * (place.Y - point.Y) <= reach * reach)
			{
				found = large.Province;
			}
		}
		return found;
	}
}
