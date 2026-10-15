using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Map;

/// <summary>
/// The weather map mode's pictures: over each province whose weather shows anything, its <c>weatherBillboardType</c>'s
/// plain icon - the type's name with <c>_simple</c> after it, as the original draws with <c>alwaysCounters</c> on, the
/// only way here (<c>docs/compatibility.md</c>, entry 12). Only in the weather map mode; a child of the counters, so its
/// coordinates are map pixels, a map unit to a pixel. See <c>docs/mechanics/weather.md</c>, section 6.
/// </summary>
public partial class CWeatherLayer : Node2D
{
	private const string SimpleSuffix = "_simple";

	/// <summary>How high the picture hangs over the province's point, and where its middle is above that: map units.</summary>
	private const float Raised = 1f;
	private const float Middle = 3f;

	private sealed record Kind(CBillboardType Type, Texture2D Texture);

	private sealed record Picture(Vector2 Place, Kind Kind);

	private CMap _map = null!;
	private CGameState _state = null!;
	private readonly Dictionary<EWeatherPicture, Kind> _kinds = new();
	private readonly List<Picture> _pictures = new();
	private Rect2 _view;

	public void Setup(CMap map, CGameState state, CGuiContext gui, CBillboardTypes billboards)
	{
		_map = map;
		_state = state;
		foreach (EWeatherPicture picture in Enum.GetValues<EWeatherPicture>())
		{
			if (CWeatherMapText.Billboard(picture) is { } name
				&& billboards.Find(name + SimpleSuffix) is { TextureFiles.Count: > 0 } type
				&& gui.Textures.Exists(type.TextureFiles[0])
				&& gui.Textures.LoadFromGamePath(type.TextureFiles[0]) is { } texture)
			{
				_kinds[picture] = new Kind(type, texture);
			}
		}
		Visible = false;
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
	}

	/// <summary>Takes again the picture each province's weather shows, as the hour has left it.</summary>
	public void Refresh()
	{
		_pictures.Clear();
		if (Visible)
		{
			foreach (CProvince province in _state.Provinces)
			{
				if (province.Id > 0 && _kinds.TryGetValue(province.WeatherPicture, out Kind? kind))
				{
					CMapPosition point = _map.Positions[province.Id];
					_pictures.Add(new Picture(new Vector2(point.X + 0.5f, _map.DisplayRow(point.Y) + 0.5f), kind));
				}
			}
		}
		QueueRedraw();
	}

	public void ViewChanged(Rect2 view)
	{
		_view = view;
		QueueRedraw();
	}

	/// <summary>
	/// Each picture as tall as twice its scale and as wide as its texture's shape makes that, hung with its middle three
	/// units over the province's point raised by one: up off the map drawn as north, as measured on the original.
	/// </summary>
	public override void _Draw()
	{
		foreach (Picture picture in _pictures)
		{
			Texture2D texture = picture.Kind.Texture;
			float scale = picture.Kind.Type.Scale;
			float halfWidth = scale * texture.GetWidth() / Math.Max(1f, texture.GetHeight());
			for (int copy = -1; copy <= 1; copy++)
			{
				var here = new Vector2(picture.Place.X + copy * _map.Width, picture.Place.Y - Raised - Middle);
				if (_view.Grow(2f * scale + halfWidth).HasPoint(here))
				{
					DrawTextureRect(texture, new Rect2(here.X - halfWidth, here.Y - scale, 2f * halfWidth, 2f * scale), false);
				}
			}
		}
	}
}
