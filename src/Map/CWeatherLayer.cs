using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Map;

/// <summary>
/// The weather map mode's pictures: over each province whose weather shows anything, its <c>weatherBillboardType</c>'s
/// plain icon - the type's name with <c>_simple</c> after it, as the original draws with <c>alwaysCounters</c> on, the
/// only way here (<c>docs/compatibility.md</c>, entry 12) - drawn through the install's own <c>weather_billboard.fx</c>,
/// translated for the canvas, by the technique the type's <c>effect</c> names. Only in the weather map mode; a child of
/// the counters, so its coordinates are map pixels, a map unit to a pixel. See <c>docs/mechanics/weather.md</c>, section 6.
/// </summary>
public partial class CWeatherLayer : Node2D
{
	private const string SimpleSuffix = "_simple";

	/// <summary>The effect's texture the picture is: the one each rectangle is drawn with.</summary>
	private const string DrawnTexture = "tex0";

	/// <summary>
	/// How high the picture hangs over the province's anchor, and where its middle is above that: map units. A sea zone's
	/// anchor stands 0.3 off the water, so its picture hangs 1.3 up (<c>reversing/findings/FINDINGS-weathersim.md</c>, C.3.1).
	/// </summary>
	private const float Raised = 1f;
	private const float SeaAnchor = 0.3f;
	private const float Middle = 3f;

	/// <summary>A kind of picture: its type, its texture, the material it is drawn with and the canvas item that holds it.</summary>
	private sealed record Kind(CBillboardType Type, Texture2D Texture, ShaderMaterial? Material, Rid Item);

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
				_kinds[picture] = new Kind(type, texture, Material(gui, billboards, type), RenderingServer.CanvasItemCreate());
			}
		}
		Visible = false;
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
	}

	/// <summary>
	/// The material a type's pictures are drawn with: its <c>effect</c>'s technique of the file that <c>effect</c> is kept
	/// under, fed as the original feeds the upper quad - the picture still, <c>vTime</c> nought, <c>vXOffset</c> nought and
	/// <c>vFlip</c> one - or null, with a line in the log, where the file has no such technique.
	/// </summary>
	private static ShaderMaterial? Material(CGuiContext gui, CBillboardTypes billboards, CBillboardType type)
	{
		string written = billboards.EffectFileOf(type.Effect);
		string file = written.Split('\\', '/').LastOrDefault(part => part.Length > 0) ?? "";
		(Shader? shader, CGodotShader? translation) = file.Length == 0
			? (null, null)
			: gui.Effects.Get(file, type.Effect, EShaderTarget.Canvas, DrawnTexture);
		if (shader is null || translation is null)
		{
			CDiagnostics.Warn($"map: the weather picture '{type.Name}' has no technique '{type.Effect}' to be drawn with, and is drawn plainly");
			return null;
		}
		var material = new ShaderMaterial { Shader = shader };
		foreach ((string fxName, float value) in new[] { ("vTime", 0f), ("vXOffset", 0f), ("vFlip", 1f) })
		{
			foreach (CShaderUniform uniform in translation.Uniforms.Where(u => u.FxName == fxName && !u.Shared))
			{
				material.SetShaderParameter(uniform.Name, value);
			}
		}
		return material;
	}

	public override void _Ready()
	{
		foreach (Kind kind in _kinds.Values)
		{
			RenderingServer.CanvasItemSetParent(kind.Item, GetCanvasItem());
			RenderingServer.CanvasItemSetDefaultTextureFilter(kind.Item, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
			RenderingServer.CanvasItemSetMaterial(kind.Item, kind.Material?.GetRid() ?? new Rid());
		}
	}

	public override void _ExitTree()
	{
		foreach (Kind kind in _kinds.Values)
		{
			RenderingServer.FreeRid(kind.Item);
		}
		_kinds.Clear();
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
					float raised = Raised + (_map.IsSea(province.Id) ? SeaAnchor : 0f);
					_pictures.Add(new Picture(new Vector2(point.X + 0.5f, _map.DisplayRow(point.Y) + 0.5f - raised), kind));
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
	/// units over its raised anchor: up off the map drawn as north, as measured on the original. *Ours:* the pictures of
	/// one kind are drawn together, where the original draws them in the order its scene reaches them.
	/// </summary>
	public override void _Draw()
	{
		foreach (Kind kind in _kinds.Values)
		{
			RenderingServer.CanvasItemClear(kind.Item);
		}
		foreach (Picture picture in _pictures)
		{
			Texture2D texture = picture.Kind.Texture;
			float scale = picture.Kind.Type.Scale;
			float halfWidth = scale * texture.GetWidth() / Math.Max(1f, texture.GetHeight());
			for (int copy = -1; copy <= 1; copy++)
			{
				var here = new Vector2(picture.Place.X + copy * _map.Width, picture.Place.Y - Middle);
				if (_view.Grow(2f * scale + halfWidth).HasPoint(here))
				{
					texture.DrawRect(picture.Kind.Item, new Rect2(here.X - halfWidth, here.Y - scale, 2f * halfWidth, 2f * scale), false);
				}
			}
		}
	}
}
