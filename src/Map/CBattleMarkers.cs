using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Map;

/// <summary>
/// The markers of the battles the player has a part in: a round badge half way between
/// the two sides' first units, green, yellow or red by how the battle goes for him,
/// pointing the way of the attack and carrying the attacker's share of the two sides'
/// strength. A click on one brings the battle's window up. A child of the counters, so
/// its coordinates are map pixels. See <c>docs/mechanics/combatview.md</c>, section 8.
/// </summary>
public partial class CBattleMarkers : Node2D
{
	/// <summary>
	/// Where the badge's round part is in its picture and how much of the picture it
	/// fills, from the picture's middle. Measured on the picture.
	/// </summary>
	private static readonly Vector2 BadgeMiddle = new(0f, -0.06f);
	private const float BadgeShare = 0.58f;

	/// <summary>How high a line of the number is, in the badge's half sides. Measured on a screenshot of the original.</summary>
	private const float NumberHeight = 0.8f;

	/// <summary>How far under the middle of the badge's round part the middle of the number's line is, in lines. Measured likewise.</summary>
	private const float NumberDrop = 0.1f;

	/// <summary>Under this many pixels of the screen the number is not written.</summary>
	private const float LeastTextOnScreen = 5f;

	private sealed record Picture(CBillboardType Type, Texture2D Texture, CGuiFont? Font);

	private sealed record Marker(CCombat Combat, Vector2 Place, float Turn, int Frame, string Text);

	private CMap _map = null!;
	private CGameState _state = null!;
	private Picture? _far;
	private Picture? _near;
	private Func<CUnit, Vector2>? _placeOf;
	private readonly List<Marker> _markers = new();
	private float _zoom = 1f;
	private Rect2 _view;

	/// <summary>The country the markers are shown for. A battle it has no part in has none.</summary>
	public CCountryTag? Player { get; set; }

	/// <param name="placeOf">Where a unit's counter stands, on the middle copy of the map.</param>
	public void Setup(CMap map, CGameState state, CGuiContext gui, CBillboardTypes billboards, Func<CUnit, Vector2> placeOf)
	{
		_map = map;
		_state = state;
		_placeOf = placeOf;
		_far = Load(gui, billboards.Find(CBillboardTypes.CombatStatus));
		_near = Load(gui, billboards.Find(CBillboardTypes.CombatStatusClose));
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
	}

	private static Picture? Load(CGuiContext gui, CBillboardType? type)
	{
		if (type is null || type.TextureFile.Length == 0 || !gui.Textures.Exists(type.TextureFile)
			|| gui.Textures.LoadFromGamePath(type.TextureFile) is not { } texture)
		{
			return null;
		}
		return new Picture(type, texture, gui.Fonts.Find(type.Font));
	}

	/// <summary>Works out again which battles there are, where their markers go and what they say.</summary>
	public void Refresh()
	{
		_markers.Clear();
		if (Player is { } player && _placeOf is not null)
		{
			foreach (CCombat combat in _state.Combats)
			{
				if (!CCombatViewText.TakesPart(combat, player) || combat.Attacker.Units.Count == 0
					|| combat.Defender.Units.Count == 0)
				{
					continue;
				}
				CUnit attacker = combat.Attacker.Units[0];
				Vector2 from = _placeOf(attacker);
				Vector2 to = _placeOf(combat.Defender.Units[0]);
				// The short way round: the east edge of the map is beside the west.
				to.X += Mathf.Round((from.X - to.X) / _map.Width) * _map.Width;

				long share = CCombatViewText.Share(combat);
				_markers.Add(new Marker(combat, (from + to) / 2f, Turn(attacker, to - from),
					CCombatViewText.MarkerFrame(share, CCombatViewText.Defends(combat, player)),
					CCombatViewText.MarkerText(share)));
			}
		}
		QueueRedraw();
	}

	/// <summary>
	/// How far the badge is turned from pointing north, clockwise on the screen: the way
	/// the attacker's first unit is going, or with nowhere to go the way to the defender.
	/// </summary>
	private float Turn(CUnit attacker, Vector2 towards)
	{
		if (attacker.Path.Count > 0 && attacker.Location > 0)
		{
			CMapPosition here = _map.Positions[attacker.Location];
			CMapPosition next = _map.Positions[attacker.Path[0]];
			return Mathf.Pi / 2f - CCounterLayout.Heading(here.X, here.Y, next.X, next.Y, _map.Width);
		}
		return towards == Vector2.Zero ? 0f : Mathf.Atan2(towards.X, -towards.Y);
	}

	public void ViewChanged(float zoom, Rect2 view)
	{
		_zoom = zoom;
		_view = view;
		QueueRedraw();
	}

	/// <summary>The marker's picture for the present view: one from close by and another from further off.</summary>
	private Picture? Current()
	{
		bool near = CMapCamera.IsNear(CMapCamera.HeightAt(_zoom, GetViewportRect().Size.Y));
		return near ? _near ?? _far : _far ?? _near;
	}

	public override void _Draw()
	{
		if (Current() is not { } picture)
		{
			return;
		}
		float half = picture.Type.Scale;
		float frameWidth = picture.Texture.GetWidth() / (float)picture.Type.Frames;
		foreach (Marker marker in _markers)
		{
			int frame = Math.Clamp(marker.Frame, 0, picture.Type.Frames - 1);
			var source = new Rect2(frame * frameWidth, 0f, frameWidth, picture.Texture.GetHeight());
			for (int copy = -1; copy <= 1; copy++)
			{
				var here = new Vector2(marker.Place.X + copy * _map.Width, marker.Place.Y);
				if (!_view.Grow(half).HasPoint(here))
				{
					continue;
				}
				DrawSetTransform(here, marker.Turn, Vector2.One);
				DrawTextureRectRegion(picture.Texture, new Rect2(-half, -half, 2f * half, 2f * half), source);
				DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
				DrawNumber(picture, marker.Text, here + (BadgeMiddle * half).Rotated(marker.Turn), half);
			}
		}
	}

	/// <summary>Writes the number upright in the middle of the badge's round part.</summary>
	private void DrawNumber(Picture picture, string text, Vector2 middle, float half)
	{
		float lineHeight = NumberHeight * half;
		if (text.Length == 0 || picture.Font?.Font is not { } typeface || picture.Font.LineHeight <= 0f
			|| lineHeight * _zoom < LeastTextOnScreen)
		{
			return;
		}
		float scale = lineHeight / picture.Font.LineHeight;
		Vector2 size = typeface.GetStringSize(text, HorizontalAlignment.Left, -1f, picture.Font.Size);
		float width = size.X * scale * picture.Font.Narrowing;
		// The line a digit stands on is its ascent under the top of the line.
		var start = new Vector2(middle.X - width / 2f,
			middle.Y + (NumberDrop - 0.5f) * lineHeight + picture.Font.Ascent * scale);
		DrawSetTransform(start, 0f, new Vector2(scale * picture.Font.Narrowing, scale));
		DrawString(typeface, Vector2.Zero, text, HorizontalAlignment.Left, -1f, picture.Font.Size, picture.Font.Color);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	/// <summary>The battle whose marker is at a point of the map, or null: of two that overlap, the one drawn later.</summary>
	public CCombat? At(Vector2 point)
	{
		if (!IsVisibleInTree() || Current() is not { } picture)
		{
			return null;
		}
		float half = picture.Type.Scale;
		float reach = half * BadgeShare;
		float x = Mathf.PosMod(point.X, _map.Width);
		CCombat? found = null;
		foreach (Marker marker in _markers)
		{
			Vector2 middle = marker.Place + (BadgeMiddle * half).Rotated(marker.Turn);
			float across = Mathf.Abs(Mathf.PosMod(middle.X, _map.Width) - x);
			across = Mathf.Min(across, _map.Width - across);
			if (across * across + (middle.Y - point.Y) * (middle.Y - point.Y) <= reach * reach)
			{
				found = marker.Combat;
			}
		}
		return found;
	}
}
