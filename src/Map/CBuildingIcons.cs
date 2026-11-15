using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Map;

/// <summary>
/// The buildings on the map: a small picture for every building a province has that is
/// declared to show there - its airfield, its port, its forts and the rest - each where
/// <c>positions.txt</c> puts it. The port's and the airfield's are how a fleet in port
/// and an air unit at its base are seen and picked: their pictures change when
/// something is there, and a click on one goes through the units in it. And, under them, the
/// capital marker on every country's acting capital, at the province's city point, which a click
/// goes through. See <c>docs/mechanics/mapunits.md</c>, section 9.
/// </summary>
public partial class CBuildingIcons : Node2D
{
	/// <summary>
	/// How much of a picture's square its round badge fills, from the middle: what a
	/// click has to be inside. Measured on the two bases' pictures.
	/// </summary>
	public const float BadgeShare = 0.66f;

	private sealed record Picture(CBillboardType Type, Texture2D Texture);

	private CMap _map = null!;
	private CGameState _state = null!;
	private CGuiContext _gui = null!;
	private CBillboardTypes _billboards = null!;
	private readonly Dictionary<string, Picture?> _pictures = new(StringComparer.OrdinalIgnoreCase);
	private List<CMapBuilding> _buildings = new();
	private List<CMapCapital> _capitals = new();
	private Rect2 _view;

	public void Setup(CMap map, CGameState state, CGuiContext gui, CBillboardTypes billboards)
	{
		_map = map;
		_state = state;
		_gui = gui;
		_billboards = billboards;
		// Drawn through the install's billboard.fx, as the original draws a plain billboard.
		Material = CBillboardMaterial.Make(gui, billboards);
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		ShowBehindParent = true;
	}

	/// <summary>The country playing, whose knowledge of each province decides what of its buildings shows.</summary>
	public CCountryTag? Player { get; set; }

	/// <summary>Works out again which buildings there are, which bases have something in them and where the capitals are.</summary>
	public void Refresh()
	{
		_buildings = CMapBuildings.Build(_state, _map, Player);
		_capitals = CMapCapitals.Build(_state, _map);
		QueueRedraw();
	}

	public void ViewChanged(Rect2 view)
	{
		_view = view;
		QueueRedraw();
	}

	/// <summary>
	/// The picture of a building, or null where it has none - and for every building in
	/// the far view, where the map is bare. The counters layer hides this one then.
	/// </summary>
	private Picture? PictureOf(string building)
	{
		return PictureNamed(CMapBuildings.PicturePrefix + building);
	}

	/// <summary>The billboard of that name, or null where there is none - and for everything in the far view.</summary>
	private Picture? PictureNamed(string name)
	{
		if (!Visible)
		{
			return null;
		}
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
	/// The middle of a building's picture in this node's coordinates, on the middle copy of the map: its place, a world
	/// point, where the 2D map draws one - half a pixel east and south of it (mapdraw.md, section 3).
	/// </summary>
	private Vector2 PlaceOf(CMapBuilding building)
	{
		return CWorldPoint.ToMap(building.X, building.Y, _map.Height).ToGodot();
	}

	/// <summary>The middle of a capital marker in this node's coordinates: its city point, a world point, drawn as a building's.</summary>
	private Vector2 PlaceOf(CMapCapital capital)
	{
		return CWorldPoint.ToMap(capital.X, capital.Y, _map.Height).ToGodot();
	}

	public override void _Draw()
	{
		// The capital markers first, so that a building's picture on the same point lies over one (mapunits.md, section 9).
		if (PictureNamed(CMapCapitals.Picture) is { } marker)
		{
			foreach (CMapCapital capital in _capitals)
			{
				DrawPicture(marker, PlaceOf(capital), 0);
			}
		}
		foreach (CMapBuilding building in _buildings)
		{
			if (PictureOf(building.Building) is { } picture)
			{
				DrawPicture(picture, PlaceOf(building), building.Frame);
			}
		}
	}

	/// <summary>A picture's frame as a square of twice its type's scale a side, on each copy of the map that is in view.</summary>
	private void DrawPicture(Picture picture, Vector2 place, int frame)
	{
		float half = picture.Type.Scale;
		float frameWidth = picture.Texture.GetWidth() / (float)picture.Type.Frames;
		frame = Math.Min(frame, picture.Type.Frames - 1);
		var source = new Rect2(frame * frameWidth, 0f, frameWidth, picture.Texture.GetHeight());
		for (int copy = -1; copy <= 1; copy++)
		{
			var here = new Vector2(place.X + copy * _map.Width, place.Y);
			if (_view.Grow(half).HasPoint(here))
			{
				DrawTextureRectRegion(picture.Texture, new Rect2(here.X - half, here.Y - half, 2f * half, 2f * half), source);
			}
		}
	}

	/// <summary>The building whose picture is at a point of the map, or null: of two that overlap, the one drawn later.</summary>
	public CMapBuilding? At(Vector2 point)
	{
		float x = Mathf.PosMod(point.X, _map.Width);
		CMapBuilding? found = null;
		foreach (CMapBuilding building in _buildings)
		{
			if (PictureOf(building.Building) is not { } picture)
			{
				continue;
			}
			Vector2 place = PlaceOf(building);
			float across = Mathf.Abs(Mathf.PosMod(place.X, _map.Width) - x);
			across = Mathf.Min(across, _map.Width - across);
			float reach = picture.Type.Scale * BadgeShare;
			if (across * across + (place.Y - point.Y) * (place.Y - point.Y) <= reach * reach)
			{
				found = building;
			}
		}
		return found;
	}
}
