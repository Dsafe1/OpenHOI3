using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Map;

/// <summary>
/// The map on screen: draws it, moves over it, and says which province the mouse is on.
///
/// The node's own coordinates are map pixels with north at the top, so a point here
/// and a point on the raster differ only in which way y counts. The picture repeats
/// to the east and west, because the map is a cylinder: the rectangle is drawn three
/// maps wide and the camera is folded back onto the middle one as it leaves it.
/// </summary>
public partial class CMapView : Node2D
{
	/// <summary>What a province's colour says about it.</summary>
	public enum MapMode
	{
		/// <summary>Who owns it.</summary>
		Political,
		/// <summary>What the ground is.</summary>
		Terrain,
	}

	[Signal]
	public delegate void ProvinceHoveredEventHandler(int provinceId);

	[Signal]
	public delegate void ProvinceSelectedEventHandler(int provinceId);

	/// <summary>A unit's counter was clicked, or with 0 the unit that was selected no longer is.</summary>
	[Signal]
	public delegate void UnitSelectedEventHandler(int unitId);

	/// <summary>
	/// The right button was clicked on a province while a unit was selected: the unit
	/// is sent there, and with <c>append</c> - the shift key - after whatever it was
	/// already told.
	/// </summary>
	[Signal]
	public delegate void UnitOrderedEventHandler(int unitId, int provinceId, bool append);

	/// <summary>
	/// How far the pointer may travel with the right button down and its release still
	/// be a click and not the end of a drag, in pixels of the screen.
	/// </summary>
	private const float ClickSlack = 6f;

	private const string ShaderPath = "res://src/Map/PoliticalMap.gdshader";
	private const int PaletteWidth = 256;

	private const float MaxZoom = 16f;
	private const float ZoomStep = 1.25f;
	/// <summary>Map pixels a second at zoom 1, for the keyboard.</summary>
	private const float KeyScrollSpeed = 900f;

	private static readonly Color SeaColour = new(0.24f, 0.36f, 0.50f);
	private static readonly Color UnownedColour = new(0.55f, 0.55f, 0.52f);
	/// <summary>
	/// For the points that belong to no province. On the shipped map those are the
	/// lakes - painted white in the province bitmap, which no province's colour is -
	/// so they are drawn as water.
	/// </summary>
	private static readonly Color NothingColour = SeaColour;

	/// <summary>What a province a unit may be placed in is tinted with, and how strongly.</summary>
	private static readonly Color PlaceColour = new(0.2f, 0.9f, 0.25f);
	private const float PlaceStrength = 0.55f;

	private CMap _map = null!;
	private CGameState _state = null!;

	private ShaderMaterial _material = null!;
	private Image _paletteImage = null!;
	private ImageTexture _paletteTexture = null!;
	private Camera2D _camera = null!;

	private bool _dragging;
	private float _dragged;
	private int _hovered;
	private int _selected;
	private MapMode _mode = MapMode.Political;
	private CUnitCounters? _counters;
	private HashSet<int>? _places;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CMap map, CGameState state)
	{
		_map = map;
		_state = state;
	}

	/// <summary>
	/// Gives the map its units, drawn as counters. Before the node enters the tree, and
	/// after <see cref="Setup"/>.
	/// </summary>
	public void SetupCounters(CGuiContext gui, CCounterTypes types, CBillboardTypes billboards, CArrowTypes arrows)
	{
		_counters = new CUnitCounters { Name = "Counters" };
		_counters.Setup(_map, _state, gui, types, billboards, arrows);
	}

	/// <summary>The units on the map, or null when it was given none to draw.</summary>
	public CUnitCounters? Counters => _counters;

	public int SelectedProvince => _selected;

	/// <summary>
	/// Marks the provinces a unit being placed may go in, or with null marks none.
	/// How the original shows them was not read; here they are tinted green.
	/// </summary>
	public void ShowPlaces(IReadOnlyCollection<int>? places)
	{
		_places = places is null || places.Count == 0 ? null : new HashSet<int>(places);
		RefreshColours();
	}

	public MapMode Mode
	{
		get => _mode;
		set
		{
			_mode = value;
			RefreshColours();
		}
	}

	public override void _Ready()
	{
		_material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		_material.SetShaderParameter("province_ids", BuildProvinceTexture());
		_material.SetShaderParameter("map_size", new Vector2I(_map.Width, _map.Height));
		Material = _material;

		int rows = (_map.ProvinceCount + PaletteWidth - 1) / PaletteWidth;
		_paletteImage = Image.CreateEmpty(PaletteWidth, rows, false, Image.Format.Rgba8);
		_paletteTexture = ImageTexture.CreateFromImage(_paletteImage);
		_material.SetShaderParameter("palette", _paletteTexture);
		RefreshColours();

		_camera = new Camera2D { Position = new Vector2(_map.Width / 2f, _map.Height / 2f) };
		AddChild(_camera);
		_camera.MakeCurrent();
		if (_counters is not null)
		{
			AddChild(_counters);
		}
		SetZoom(MinZoom());
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(-_map.Width, 0, _map.Width * 3, _map.Height), Colors.White);
	}

	/// <summary>
	/// The id raster as a texture. Each id is split over two channels, and the rows are
	/// turned over on the way: the raster counts y north and a texture counts it down.
	/// </summary>
	private ImageTexture BuildProvinceTexture()
	{
		int width = _map.Width;
		int height = _map.Height;
		ReadOnlySpan<ushort> raster = _map.Raster;
		var bytes = new byte[width * height * 2];

		for (int y = 0; y < height; y++)
		{
			ReadOnlySpan<ushort> source = raster.Slice(y * width, width);
			Span<byte> target = bytes.AsSpan(_map.DisplayRow(y) * width * 2, width * 2);
			for (int x = 0; x < width; x++)
			{
				target[x * 2] = (byte)source[x];
				target[x * 2 + 1] = (byte)(source[x] >> 8);
			}
		}

		return ImageTexture.CreateFromImage(
			Image.CreateFromData(width, height, false, Image.Format.Rg8, bytes));
	}

	/// <summary>
	/// Recolours every province from the game state, by the current mode: the owner's
	/// colour or the terrain's on land, one colour for all the sea. Call it again
	/// whenever what the mode shows changes.
	/// </summary>
	public void RefreshColours()
	{
		for (int id = 0; id < _map.ProvinceCount; id++)
		{
			CProvince province = _state.Provinces[id];
			Color colour;
			if (id == 0)
			{
				colour = NothingColour;
			}
			else if (province.IsSea)
			{
				colour = SeaColour;
			}
			else if (_mode == MapMode.Terrain)
			{
				colour = province.Terrain?.Color.ToGodot() ?? UnownedColour;
			}
			else
			{
				colour = _state.Country(province.Owner)?.Color.ToGodot() ?? UnownedColour;
			}
			if (_places is not null && _places.Contains(id))
			{
				colour = colour.Lerp(PlaceColour, PlaceStrength);
			}
			_paletteImage.SetPixel(id % PaletteWidth, id / PaletteWidth, colour);
		}
		_paletteTexture.Update(_paletteImage);
	}

	// --- the camera ---

	/// <summary>The zoom at which the whole height of the map just fits the screen.</summary>
	private float MinZoom()
	{
		return GetViewportRect().Size.Y / _map.Height;
	}

	private void SetZoom(float zoom)
	{
		zoom = Mathf.Clamp(zoom, MinZoom(), MaxZoom);
		_camera.Zoom = new Vector2(zoom, zoom);
		_material.SetShaderParameter("province_border", Mathf.SmoothStep(1.2f, 2.5f, zoom));
		ClampCamera();
	}

	/// <summary>
	/// Keeps the view on the map from north to south, and folds the camera back onto
	/// the middle copy of the map from east to west. The fold moves it by exactly one
	/// map width, so nothing on screen changes.
	/// </summary>
	private void ClampCamera()
	{
		float halfHeight = GetViewportRect().Size.Y / _camera.Zoom.Y / 2f;
		Vector2 position = _camera.Position;
		position.X = Mathf.PosMod(position.X, _map.Width);
		position.Y = halfHeight * 2f >= _map.Height
			? _map.Height / 2f
			: Mathf.Clamp(position.Y, halfHeight, _map.Height - halfHeight);
		_camera.Position = position;

		Vector2 seen = GetViewportRect().Size / _camera.Zoom;
		_counters?.ViewChanged(_camera.Zoom.X, new Rect2(position - seen / 2f, seen));
	}

	/// <summary>Where a point on the screen is on the map, in this node's coordinates.</summary>
	private Vector2 ScreenToMap(Vector2 screen)
	{
		return _camera.Position + (screen - GetViewportRect().Size / 2f) / _camera.Zoom;
	}

	private void ZoomAt(Vector2 screen, float factor)
	{
		// Whatever is under the mouse stays under it.
		Vector2 anchor = ScreenToMap(screen);
		SetZoom(_camera.Zoom.X * factor);
		_camera.Position = anchor - (screen - GetViewportRect().Size / 2f) / _camera.Zoom;
		ClampCamera();
	}

	/// <summary>Puts a province in the middle of the screen, using its bounding box.</summary>
	public void CentreOn(int provinceId, float? zoom = null)
	{
		if (provinceId <= 0 || provinceId >= _map.ProvinceCount || _map.Shapes[provinceId].IsEmpty)
		{
			return;
		}
		CProvinceShape shape = _map.Shapes[provinceId];
		_camera.Position = new Vector2(shape.CentreX + 0.5f, _map.DisplayRow(shape.CentreY) + 0.5f);
		SetZoom(zoom ?? _camera.Zoom.X);
	}

	public void Select(int provinceId)
	{
		_selected = provinceId;
		_material.SetShaderParameter("selected_province", provinceId);
		EmitSignal(SignalName.ProvinceSelected, provinceId);
	}

	/// <summary>Selects a unit on the map - its counter wears a ring - or with null selects none.</summary>
	public void SelectUnit(CUnit? unit)
	{
		if (_counters is null || ReferenceEquals(_counters.Selected, unit))
		{
			return;
		}
		_counters.Selected = unit;
		EmitSignal(SignalName.UnitSelected, unit?.Id ?? 0);
	}

	// --- input ---

	private int ProvinceAtScreen(Vector2 screen)
	{
		Vector2 point = ScreenToMap(screen);
		int row = Mathf.FloorToInt(point.Y);
		if (row < 0 || row >= _map.Height)
		{
			return 0;
		}
		return _map.ProvinceAt(Mathf.FloorToInt(point.X), _map.DisplayRow(row));
	}

	private void UpdateHover(Vector2 screen)
	{
		int province = ProvinceAtScreen(screen);
		if (province == _hovered)
		{
			return;
		}
		_hovered = province;
		_material.SetShaderParameter("hovered_province", province);
		EmitSignal(SignalName.ProvinceHovered, province);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel:
				ZoomAt(wheel.Position, ZoomStep);
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel:
				ZoomAt(wheel.Position, 1f / ZoomStep);
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Middle } button:
				_dragging = button.Pressed;
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Right } button:
				// The right button drags the map, and orders the selected unit when it is
				// let go where it went down.
				_dragging = button.Pressed;
				if (button.Pressed)
				{
					_dragged = 0f;
				}
				else if (_dragged <= ClickSlack && _counters?.Selected is { } ordered && _places is null
					&& ProvinceAtScreen(button.Position) is int target && target > 0)
				{
					EmitSignal(SignalName.UnitOrdered, ordered.Id, target, button.ShiftPressed);
				}
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
				// A counter is over the province it stands in, so it is asked first - but
				// not while a unit is being placed, when the click is for the province.
				CUnit? unit = _places is null ? _counters?.Click(ScreenToMap(click.Position)) : null;
				SelectUnit(unit);
				if (unit is null)
				{
					Select(ProvinceAtScreen(click.Position));
				}
				break;
			case InputEventMouseMotion motion when _dragging:
				_dragged += motion.Relative.Length();
				_camera.Position -= motion.Relative / _camera.Zoom;
				ClampCamera();
				break;
			case InputEventMouseMotion motion:
				UpdateHover(motion.Position);
				break;
			case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab }:
				Mode = _mode == MapMode.Political ? MapMode.Terrain : MapMode.Political;
				break;
		}
	}

	public override void _Process(double delta)
	{
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		if (direction != Vector2.Zero)
		{
			_camera.Position += direction * (KeyScrollSpeed * (float)delta / _camera.Zoom.X);
			ClampCamera();
			UpdateHover(GetViewport().GetMousePosition());
		}
	}
}
