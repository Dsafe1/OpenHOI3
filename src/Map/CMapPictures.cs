using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gui;

namespace OpenHOI3.Map;

/// <summary>
/// The pictures of <c>interface/mapitems.gfx</c> a map mode's layer draws, found by name once each, and drawn as the
/// buildings' are: a square of twice the type's scale times a size a side, about its point, on each copy of the map the
/// view reaches. The layer drawing them carries the install's billboard.fx as its material.
/// </summary>
public sealed class CMapPictures
{
	public sealed record Picture(CBillboardType Type, Texture2D Texture);

	private readonly CGuiContext _gui;
	private readonly CBillboardTypes _billboards;
	private readonly Dictionary<string, Picture?> _pictures = new(StringComparer.OrdinalIgnoreCase);

	public CMapPictures(CGuiContext gui, CBillboardTypes billboards)
	{
		_gui = gui;
		_billboards = billboards;
	}

	public Picture? Find(string name)
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

	/// <summary>Draws a picture on a canvas item at a point of its own coordinates, a frame of it, where the view reaches.</summary>
	public void Draw(CanvasItem canvas, string name, Vector2 place, float size, int frame, Rect2 view, float mapWidth)
	{
		if (Find(name) is not { } picture)
		{
			return;
		}
		float half = picture.Type.Scale * size;
		float frameWidth = picture.Texture.GetWidth() / (float)picture.Type.Frames;
		var source = new Rect2(Math.Min(frame, picture.Type.Frames - 1) * frameWidth, 0f, frameWidth, picture.Texture.GetHeight());
		for (int copy = -1; copy <= 1; copy++)
		{
			var here = new Vector2(place.X + copy * mapWidth, place.Y);
			if (view.Grow(half).HasPoint(here))
			{
				canvas.DrawTextureRectRegion(picture.Texture, new Rect2(here.X - half, here.Y - half, 2f * half, 2f * half), source);
			}
		}
	}
}
