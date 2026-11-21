using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Sound;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
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
		/// <summary>How the player's supplies stand in it: <c>docs/mechanics/supplymap.md</c>.</summary>
		Supply,
		/// <summary>Where the player's fighters fly, with its air bases and the week's air battles: <c>docs/mechanics/airmap.md</c>.</summary>
		Air,
		/// <summary>How great the revolt risk is: <c>docs/mechanics/revolt.md</c>, section 7.</summary>
		Revolt,
		/// <summary>The ground greyed where it is frozen, with the weather's pictures over it: <c>docs/mechanics/weather.md</c>, section 6.</summary>
		Weather,
		/// <summary>A ramp by the infrastructure: <c>docs/mechanics/mapmodes.md</c>, section 2.</summary>
		Infrastructure,
		/// <summary>Victory points by who holds them: <c>mapmodes.md</c>, section 3.</summary>
		VictoryPoints,
		/// <summary>Land units' weight by who holds the ground: <c>mapmodes.md</c>, section 4.</summary>
		Strength,
		/// <summary>IC and strategic resources, with the resource icons: <c>mapmodes.md</c>, section 5.</summary>
		Resources,
		/// <summary>Each terrain's own colour: <c>mapmodes.md</c>, section 6.</summary>
		SimplifiedTerrain,
		/// <summary>Each region's colour: <c>mapmodes.md</c>, section 7.</summary>
		Region,
		/// <summary>The owners' standing towards the player: <c>mapmodes.md</c>, section 8.</summary>
		Diplomatic,
		/// <summary>What the player knows of each province: <c>mapmodes.md</c>, section 9.</summary>
		Intel,
		/// <summary>The player's theatres: <c>mapmodes.md</c>, section 10.</summary>
		Theatre,
		/// <summary>The player's fleets' missions, with the week's naval battles: <c>mapmodes.md</c>, section 11.</summary>
		Naval,
	}

	/// <summary>Whether this is the front end's lobby map, whose political colours are the owners' with the controllers' as stripes.</summary>
	public bool IsLobby { get; set; }

	/// <summary>Each region's colour by its number, handed out from <c>interface/colors.txt</c>: what the region mode paints.</summary>
	public System.Collections.Generic.IReadOnlyList<CColor> RegionColours { get; set; } = System.Array.Empty<CColor>();

	/// <summary>Each continent's colour, by its key, which the region mode paints with continent painting on.</summary>
	public IReadOnlyDictionary<string, CColor> ContinentColours { get; set; } = new Dictionary<string, CColor>();

	/// <summary>
	/// The editor's continent painting, the original console's <c>cpaint</c>: the region mode then colours each province by
	/// its continent (mapmodes.md, section 7). Nothing in a game sets it; the debug menu does.
	/// </summary>
	public bool ContinentPainting
	{
		get => _continentPainting;
		set
		{
			_continentPainting = value;
			if (_mode == MapMode.Region)
			{
				RefreshColours();
			}
		}
	}

	private bool _continentPainting;

	[Signal]
	public delegate void ProvinceHoveredEventHandler(int provinceId);

	[Signal]
	public delegate void ProvinceSelectedEventHandler(int provinceId);

	/// <summary>The units selected changed: with the first one's number, or 0 where none is selected now.</summary>
	[Signal]
	public delegate void UnitSelectedEventHandler(int unitId);

	/// <summary>
	/// The right button was clicked on a province with units selected: each is given the click in turn, in the order
	/// selected, and with <c>append</c> - the shift key - after whatever it was already told (selection.md, section 3).
	/// </summary>
	public event Action<IReadOnlyList<CUnit>, int, bool>? UnitsOrdered;

	/// <summary>Ctrl and a digit with units selected: the group is to become them (selection.md, section 7).</summary>
	public event Action<int, IReadOnlyList<CUnit>>? GroupStored;

	/// <summary>A drag box was let go, which shuts the side menus as it lets the selection go.</summary>
	public event Action? BoxReleased;

	/// <summary>
	/// How far the pointer may travel with the right button down and its release still
	/// be a click that orders, in pixels of the screen.
	/// </summary>
	private const float ClickSlack = 6f;

	private const string ShaderPath = "res://src/Map/PoliticalMap.gdshader";
	private const int PaletteWidth = 256;

	/// <summary>The original's picture of the stripes that mark ground held by another than its owner.</summary>
	private const string StripesPath = "map/terrain/stripes.dds";

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
	private Image _stripeImage = null!;
	private ImageTexture _stripeTexture = null!;
	private Image _selectionImage = null!;
	private ImageTexture _selectionTexture = null!;
	private CTextureManager? _textures;
	private Camera2D _camera = null!;

	/// <summary>Whether the middle button is held, which drags the map.</summary>
	private bool _dragging;

	/// <summary>Where on the screen the right button went down, while it is held.</summary>
	private Vector2? _rightPress;
	private int _hovered;
	private int _selected;
	// A game starts in the terrain mode, as the original's does (minimap.md, section 3).
	private MapMode _mode = MapMode.Terrain;
	private CUnitCounters? _counters;
	private HashSet<int>? _places;

	/// <summary>The units selected, in the order selected and of one kind (selection.md, section 1).</summary>
	private readonly CSelection _units = new();

	/// <summary>Where the left button went down on the map, while it is held; the release is what acts.</summary>
	private Vector2? _press;

	private CSelectionBox? _box;

	/// <summary>The selection group last recalled, for a second press of its digit to go to it; -1 for none.</summary>
	private int _lastGroup = -1;

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
		_counters = new CUnitCounters { Name = "Counters", CounterDistance = () => CounterDistance() };
		_counters.Setup(_map, _state, gui, types, billboards, arrows);
		_planMarkers = new CPlanMarkerLayer { Name = "PlanMarkers" };
		_planMarkers.Setup(_map, _state, gui, billboards);
		AddChild(_planMarkers);
		_counters.Markers.Player = _player;
		_counters.Player = _player;
		_counters.AirMap.Player = _player;
		_counters.NavalMap.Player = _player;
		_textures = gui.Textures;
	}

	/// <summary>
	/// The map's cache: the one in the player's Documents folder and no other, built there where it is not this map's
	/// (<c>docs/mechanics/mapcache.md</c>, section 11).
	/// </summary>
	public static CMapCache OpenCache(CDirectorySettings directories, CMap map)
	{
		return CMapCache.OpenOrBuild(directories, directories.UserFolder, map,
			problem => GD.PushWarning($"map: {problem}"));
	}

	/// <summary>
	/// Gives the map its surface drawn as the original draws it, through the install's effect files, from the install's
	/// quad cache (<c>docs/mechanics/mapdraw.md</c>); where the cache cannot be read, or is stale, the map is drawn flat as
	/// before. Before the node enters the tree, and after <see cref="Setup"/>.
	/// </summary>
	public void SetupSurface(CGuiContext gui, CDirectorySettings directories, CMapCache cache, bool simpleWater, bool waves, bool trees,
		CMapSurfaceKeeper keeper, IReadOnlyList<CQuadTile>? read = null)
	{
		IReadOnlyList<CQuadTile> tiles = read ?? CQuadCache.Load(cache, problem => GD.PushWarning($"map: {problem}"));
		// The borders' corners are this view's own: their bytes carry over from one change of hands to the next. A copy of
		// those read from the cache once (CMapSurfaceKeeper.Corners).
		_corners = keeper.Corners(cache, _map);
		if (tiles.Count == 0)
		{
			GD.PushWarning("map: no quad cache read, so the surface is drawn flat");
			return;
		}
		// The surface the last view of this map drew through, where there was one, else a new one (CMapSurfaceKeeper).
		_held = keeper.Take(this, _map, gui, cache, tiles, CQuadCache.Columns(directories), simpleWater, waves, trees, _corners);
		if (_counters is not null)
		{
			_counters.Surface = _held;
		}
		_textures ??= gui.Textures;
		_names = new CCountryMapNames { Name = "CountryNames", Visible = false };
		_names.Setup(_map, _state, gui.Fonts);
		_provinceNames = new CProvinceNames { Name = "ProvinceNames" };
		_provinceNames.Setup(_map, _state, gui.Fonts);
		_mapTexts = new CMapTexts { Name = "MapTexts", GameSpeed = () => MapTextSpeed() };
		_mapTexts.Setup(_map, gui.Fonts, CAnimatedMapTextTypes.Load(directories));
		_projectionTypes = CProjectionTypes.Load(directories);
	}

	private CProvinceNames? _provinceNames;

	/// <summary>The messages written on the map, over the counters (messages.md, section 6).</summary>
	private CMapTexts? _mapTexts;

	/// <summary>The game's speed, nought to four, which the texts on the map age by.</summary>
	public Func<int> MapTextSpeed { get; set; } = () => 0;

	/// <summary>A message's text written over a province's middle, where the map draws such texts at all.</summary>
	public void ShowMessageText(int province, string text)
	{
		if (_mapTexts is null || province <= 0 || province >= _state.Provinces.Count)
		{
			return;
		}
		Sim.Provinces.CProvince at = _state.Provinces[province];
		_mapTexts.Add(new Vector2(at.CentreX + 0.5f, _map.DisplayRow(at.CentreY) + 0.5f), text);
	}

	/// <summary>The interface's projection types, which the click's marks are: read with the surface they are drawn on.</summary>
	private CProjectionTypes? _projectionTypes;

	/// <summary>
	/// The mark a right click leaves on the province a unit was ordered to (<c>docs/mechanics/movement.md</c>, section 7):
	/// <c>legal_selection_projection</c> where it was sent, <c>illegal_selection_projection</c> where it was refused, lying
	/// where that unit would stand in the province. One for each unit the click is given to. Drawn on the map's surface
	/// alone; without one, nothing.
	/// </summary>
	public void ShowOrderMark(CUnit unit, int province, bool legal)
	{
		if (_surface is not { } surface || province <= 0 || province >= _map.ProvinceCount
			|| _projectionTypes?.Find(legal ? CProjectionTypes.Legal : CProjectionTypes.Illegal) is not { } type)
		{
			return;
		}
		bool sea = _map.IsSea(province);
		(float x, float z) = CProjectionMarks.PointOf(_state, _map, unit, province);
		surface.AddMark(new CProjectionMark(type, province, !sea, x, CProjectionMarks.AnchorHeight(sea), z, CMapSurface.MarkClock));
	}

	/// <summary>The surface this view was given, which another view of the map may have taken since.</summary>
	private CMapSurface? _held;

	/// <summary>The surface while this view holds it, else null: a view put away does not draw through the next one's.</summary>
	private CMapSurface? _surface => _held is { } held && ReferenceEquals(held.Holder, this) ? held : null;

	/// <summary>Whether the sea is drawn simple: the player's <c>simpleWater</c>, which Apply changes at once.</summary>
	public bool SimpleWater
	{
		set
		{
			if (_surface is not null)
			{
				_surface.SimpleWater = value;
			}
		}
	}
	private CBorderCache? _corners;
	private ushort[]? _pixels;
	private int[]? _controllers;
	private bool[]? _land;

	/// <summary>Ours: how often at most the borders are made again, in seconds of the clock on the wall.</summary>
	private const double BordersInterval = 0.25;
	private bool _bordersDirty;
	private double _sinceBorders;

	/// <summary>The game's count of changes of hands when the borders were last made, or -1 before the first.</summary>
	private long _bordersAt = -1;

	/// <summary>
	/// The border lookup made again, its corners worked out first where a province has changed hands since - as the
	/// original runs its passes only at a change of controller (<c>FINDINGS-mapdraw.md</c>, B.7), so their bytes carry over.
	/// </summary>
	private void RefreshBorders()
	{
		if (_surface is null)
		{
			return;
		}
		// Every province's controller as a number once, for the passes and the lookup alike.
		int count = _state.Provinces.Count;
		if (_controllers?.Length != count)
		{
			_controllers = new int[count];
			_land = new bool[count];
			for (int id = 1; id < count; id++)
			{
				_land[id] = !_state.Provinces[id].IsSea;
			}
		}
		int[] controllers = _controllers;
		bool[] land = _land!;
		for (int id = 0; id < count; id++)
		{
			controllers[id] = _state.Provinces[id].Controller is { } tag ? (_state.Country(tag)?.Index ?? -1) + 1 : 0;
		}
		if (_corners is not null && _state.ControlChanges != _bordersAt)
		{
			_bordersAt = _state.ControlChanges;
			ushort[] pixels = _pixels ??= _map.Raster.ToArray();
			int width = _map.Width;
			CBorderCorners.Refresh(_corners, _map.Width, _map.Height, (x, y) => pixels[y * width + x],
				id => id > 0 && id < land.Length && land[id], id => id < controllers.Length ? controllers[id] : 0);
		}
		_surface.SetBorders((a, b) => controllers[a] == controllers[b]);
	}

	/// <summary>The far view's country names, which come with the surface (<c>docs/mechanics/mapnames.md</c>).</summary>
	private CCountryMapNames? _names;

	/// <summary>
	/// Whether the names are shown: in the far view - the camera above the first of `terrain_sheet_heights` - and in any map
	/// style but 0, the terrain and weather modes [exe] (<c>FINDINGS-farmap.md</c>, B.4).
	/// </summary>
	private bool NamesShown()
	{
		float height = GetViewportRect().Size.Y / _camera.Zoom.Y / (2f * Mathf.Tan(CMapSurfaceCamera.FieldOfView / 2f));
		return _map.Level(height) > 0 && Style(_mode) != 0;
	}

	/// <summary>Works out again which country names rest on what: once a day, for a faction joined or left.</summary>
	public void RefreshNames()
	{
		_names?.Refresh();
	}

	/// <summary>The units on the map, or null when it was given none to draw.</summary>
	public CUnitCounters? Counters => _counters;

	public int SelectedProvince => _selected;

	/// <summary>
	/// Every province selected, in the order selected - one, or more added with Shift. The province view shows the last
	/// added, or after one is taken out the first left (<c>docs/mechanics/provinceview.md</c>, section 1).
	/// </summary>
	public IReadOnlyList<int> SelectedProvinces => _selection;

	/// <summary>Whether a click on the map is placing a unit or a building, which selects nothing for the province view.</summary>
	public bool IsPlacing => _places is not null || _deployment is not null;

	private readonly List<int> _selection = new();

	/// <summary>The province under the mouse, or 0.</summary>
	public int HoveredProvince => _hovered;

	/// <summary>The country being played, whose units a click on a port or an airfield picks.</summary>
	public CCountryTag? Player
	{
		get => _player;
		set
		{
			_player = value;
			if (_counters is not null)
			{
				_counters.Markers.Player = value;
				_counters.Player = value;
				_counters.AirMap.Player = value;
				_counters.NavalMap.Player = value;
			}
		}
	}

	private CCountryTag? _player;

	/// <summary>
	/// A replay, which looks on: any country's unit is selected by a click, and a right click orders nothing
	/// (docs/replay.md, section 6).
	/// </summary>
	public bool LooksOn { get; set; }

	/// <summary>Whose units a click on a port or an airfield picks: the player's, or anybody's in a replay.</summary>
	private CCountryTag? Picker => LooksOn ? null : Player;

	/// <summary>Who a unit clicked is selected for: the player, or in a replay its own owner, so that any may be.</summary>
	private CCountryTag? Selector(CUnit unit) => LooksOn ? unit.Owner : Player;

	/// <summary>Raised when the marker of a battle is clicked.</summary>
	public event Action<CCombat>? CombatSelected;

	/// <summary>
	/// Marks the provinces a unit being placed may go in, or with null marks none.
	/// How the original shows them was not read; here they are tinted green.
	/// </summary>
	public void ShowPlaces(IReadOnlyCollection<int>? places)
	{
		_places = places is null || places.Count == 0 ? null : new HashSet<int>(places);
		RefreshColours();
	}

	/// <summary>
	/// Colours the map for the air orders window while it is up, whatever the mode, in the original's map style 16 - or
	/// with null gives the mode and its style back (<c>docs/mechanics/fleetsandair.md</c>, section 4). A province the unit
	/// cannot reach is given nothing, every part of its colour nought, and the surface's shaders draw that as the original's
	/// do: the ground turned grey and darkened, the sea near black.
	/// </summary>
	public void ShowAirArea(Func<CProvince, CColor>? colours)
	{
		_airArea = colours;
		if (_surface is not null)
		{
			_surface.Style = SurfaceStyle;
		}
		RefreshColours();
	}

	private Func<CProvince, CColor>? _airArea;

	/// <summary>The original's map style while the air orders window is up, its mode 15 (<c>FINDINGS-minimap.md</c>, section 6).</summary>
	private const int AirOrdersStyle = 16;

	/// <summary>The style the surface draws in: the air orders window's, the theatre editor's or the mode's own.</summary>
	private int SurfaceStyle => _airArea is not null ? AirOrdersStyle : _theatreEditor is not null ? EditorStyle : Style(_mode);

	/// <summary>
	/// Colours every province by the building being placed from the deploy menu - the original's map mode 14 - or with
	/// null gives the mode back. Painted once, as the line is chosen (<c>docs/mechanics/buildinglines.md</c>, section 6).
	/// </summary>
	public void ShowDeployment(Func<CProvince, CColor>? colours)
	{
		_deployment = colours;
		RefreshColours();
	}

	private Func<CProvince, CColor>? _deployment;

	private CTheatreEditor? _theatreEditor;

	/// <summary>The original's map style for the theatre editor's mode 17 (<c>reversing/findings/FINDINGS-farmap.md</c>, A.3).</summary>
	private const int EditorStyle = 2;

	/// <summary>The theatre headquarters the theatre editor is open for, or null.</summary>
	public CUnit? TheatreEditing => _theatreEditor?.Headquarters;

	/// <summary>Raised when the theatre editor gives provinces to a theatre: its number, and the provinces in order.</summary>
	public event Action<int, int[]>? TheatreEdited;

	/// <summary>
	/// Opens the theatre editor for a theatre headquarters - the original's map mode 17, the minimap's buttons left as they
	/// are - or with null closes it, anything left gathered given to the theatre of the headquarters it was opened for
	/// (<c>docs/mechanics/attaching.md</c>, section 7).
	/// </summary>
	public void SetTheatreEditing(CUnit? headquarters)
	{
		if (_theatreEditor is { } open)
		{
			Give(open, open.Headquarters);
		}
		_theatreEditor = headquarters is null ? null : new CTheatreEditor(headquarters);
		// Mode 17 draws in the political mode's style, 2, whatever mode was up; closing gives the mode's own back.
		if (_surface is not null)
		{
			_surface.Style = SurfaceStyle;
		}
		_box?.HideBox();
		RefreshColours();
	}

	/// <summary>What the editor has gathered, given to the theatre heading the unit's chain, where there is one.</summary>
	private void Give(CTheatreEditor editor, CUnit unit)
	{
		int[] provinces = editor.Release();
		if (provinces.Length > 0 && CTheatreEditor.TheatreOf(_state, unit) is { } theatre)
		{
			TheatreEdited?.Invoke(theatre.Id, provinces);
		}
	}

	/// <summary>A point of the map in the positions file's frame, where y counts north: what a box is gathered in.</summary>
	private Vector2 PositionsFrame(Vector2 map) => new(map.X - 0.5f, _map.Height - 0.5f - map.Y);

	/// <summary>
	/// Asked with the province under a left click while a building is being placed: answers whether the click placed it.
	/// </summary>
	public Func<int, bool>? PlaceBuilding { get; set; }

	public MapMode Mode
	{
		get => _mode;
		set
		{
			_mode = value;
			if (_counters is not null)
			{
				_counters.AirMode = value == MapMode.Air;
				_counters.NavalMode = value == MapMode.Naval;
				_counters.ResourceMode = value == MapMode.Resources;
				_counters.WeatherMode = value == MapMode.Weather;
			}
			// Before the view is in the tree the mode is only noted: it enters drawing it, its surface made in that style alone.
			if (!IsInsideTree())
			{
				return;
			}
			if (_surface is not null)
			{
				_surface.Style = SurfaceStyle;
				_surface.TreeSnow = value == MapMode.Weather ? Snowed : null;
			}
			RefreshColours();
		}
	}

	/// <summary>
	/// Whether a province's trees are drawn snowed in the weather mode: its temperature below −5, strictly [exe]
	/// (<c>reversing/findings/FINDINGS-weathersim.md</c>, C.5).
	/// </summary>
	private bool Snowed(int province) =>
		province > 0 && province < _state.Provinces.Count && _state.Provinces[province].Weather.Temperature.Raw < -5000;

	/// <summary>Works out again what the air or the naval map mode draws over the map, when it is on: once a day.</summary>
	public void RefreshModeIcons()
	{
		if (_mode == MapMode.Air && _counters is not null)
		{
			_counters.AirMap.Refresh(_units.First);
		}
		// The naval mode's icons follow the week's battles a day at a time, as the original's do (mapmodes.md, section 11).
		if (_mode == MapMode.Naval && _counters is not null)
		{
			_counters.NavalMap.Refresh();
		}
	}

	/// <summary>
	/// The original's map style for a mode (<c>reversing/findings/FINDINGS-farmap.md</c>, A.3): 0 the terrain and weather
	/// modes, which draw the ground as it is, 18 the air mode, 9 supply and 2 the political - and ours for revolt risk,
	/// which the original's style for it was not read.
	/// </summary>
	private static int Style(MapMode mode) => mode switch
	{
		MapMode.Terrain or MapMode.Weather => 0,
		MapMode.Supply or MapMode.Infrastructure => 9,
		MapMode.Air => 18,
		MapMode.Naval => 19,
		MapMode.Diplomatic => 6,
		MapMode.SimplifiedTerrain or MapMode.Region => 8,
		MapMode.Intel => 12,
		MapMode.VictoryPoints => 13,
		MapMode.Theatre => 14,
		MapMode.Strength => 15,
		MapMode.Resources => 20,
		_ => 2,
	};

	public override void _Ready()
	{
		// The surface is the keeper's child, not the view's, so that it outlives the view.
		if (_surface is not null)
		{
			_surface.Style = SurfaceStyle;
		}
		_material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		_material.SetShaderParameter("province_ids", BuildProvinceTexture());
		_material.SetShaderParameter("map_size", new Vector2I(_map.Width, _map.Height));
		Material = _material;

		int rows = (_map.ProvinceCount + PaletteWidth - 1) / PaletteWidth;
		_paletteImage = Image.CreateEmpty(PaletteWidth, rows, false, Image.Format.Rgba8);
		_paletteTexture = ImageTexture.CreateFromImage(_paletteImage);
		_material.SetShaderParameter("palette", _paletteTexture);
		_stripeImage = Image.CreateEmpty(PaletteWidth, rows, false, Image.Format.Rgba8);
		_stripeTexture = ImageTexture.CreateFromImage(_stripeImage);
		_material.SetShaderParameter("stripe_palette", _stripeTexture);
		// A texel a province, lit where it is selected: every selected province is outlined (provinceview.md, section 1).
		_selectionImage = Image.CreateEmpty(PaletteWidth, rows, false, Image.Format.R8);
		_selectionTexture = ImageTexture.CreateFromImage(_selectionImage);
		_material.SetShaderParameter("selection", _selectionTexture);

		// The stripes are the original's own picture, and without it nothing is striped.
		if (_textures?.LoadImage(StripesPath) is { } stripes)
		{
			_material.SetShaderParameter("stripes", ImageTexture.CreateFromImage(StripeMask(stripes)));
			_material.SetShaderParameter("has_stripes", true);
		}
		RefreshColours();

		_camera = new Camera2D { Position = new Vector2(_map.Width / 2f, _map.Height / 2f) };
		AddChild(_camera);
		_camera.MakeCurrent();
		// The names are drawn before the units' layers, as the original's scene draws them (FINDINGS-mapdraw.md, C.1).
		if (_names is not null)
		{
			AddChild(_names);
		}
		if (_provinceNames is not null)
		{
			AddChild(_provinceNames);
		}
		if (PlanSigns is not null)
		{
			AddChild(PlanSigns);
		}
		if (_counters is not null)
		{
			_counters.Selection = _units;
			_counters.SelectionLost += () => UnitsChanged(keepGroup: true);
			AddChild(_counters);
		}
		// The texts of messages are drawn late, over the counters.
		if (_mapTexts is not null)
		{
			AddChild(_mapTexts);
		}
		// The drag box, over everything else on the map.
		_box = new CSelectionBox { Name = "SelectionBox", Visible = false };
		AddChild(_box);
		SetZoom(MinZoom());
	}

	public override void _Draw()
	{
		// Over the original's surface nothing of the flat map is drawn: it draws the borders, the sea zones' and the selection
		// itself (docs/mechanics/mapdraw.md, section 6). The flat map is drawn only where the install has no quad cache.
		if (_surface is not null)
		{
			return;
		}
		DrawRect(new Rect2(-_map.Width, 0, _map.Width * 3, _map.Height), Colors.White);
	}

	/// <summary>
	/// The stripes as a picture whose alpha is how much of the second colour a point
	/// takes, with smaller copies of itself for the map seen from far off. The original's
	/// file keeps its stripes in a format whose see-through points are black: where a
	/// decoder hands that back with no alpha at all, the brightest channel stands for it.
	/// </summary>
	private static Image StripeMask(Image source)
	{
		var picture = (Image)source.Duplicate();
		if (picture.IsCompressed())
		{
			picture.Decompress();
		}
		picture.Convert(Image.Format.Rgba8);
		bool hasAlpha = picture.DetectAlpha() != Image.AlphaMode.None;
		var mask = Image.CreateEmpty(picture.GetWidth(), picture.GetHeight(), false, Image.Format.Rgba8);
		for (int y = 0; y < picture.GetHeight(); y++)
		{
			for (int x = 0; x < picture.GetWidth(); x++)
			{
				Color pixel = picture.GetPixel(x, y);
				float amount = hasAlpha ? pixel.A : Mathf.Max(pixel.R, Mathf.Max(pixel.G, pixel.B));
				mask.SetPixel(x, y, new Color(1f, 1f, 1f, amount));
			}
		}
		mask.GenerateMipmaps();
		return mask;
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
	/// Recolours every province from the game state, by the current mode: its holder's
	/// colour, striped with its owner's where that is another country, or its terrain's,
	/// on land, and one colour for all the sea. Call it again whenever what the mode
	/// shows changes.
	/// </summary>
	public void RefreshColours()
	{
		Dictionary<int, (CColor Ground, CColor Stripes)>? air = _mode == MapMode.Air && _airArea is null
			? CAirMapColours.Build(_state, Player)
			: null;
		// The naval mode colours the sea as the air mode does (mapmodes.md, section 11), the theatre mode by the player's theatres.
		Dictionary<int, (CColor Ground, CColor Stripes)>? naval = _mode == MapMode.Naval && _airArea is null
			? CNavalMapColours.Build(_state, Player)
			: null;
		Dictionary<int, CColor>? theatres = _mode == MapMode.Theatre ? CTheatreMapColours.Build(_state, Player, RegionColours) : null;
		Dictionary<int, List<CUnit>>? landUnits = null;
		for (int id = 0; id < _map.ProvinceCount; id++)
		{
			CProvince province = _state.Provinces[id];
			Color colour;
			Color? stripe = null;
			if (id == 0)
			{
				colour = NothingColour;
			}
			else if (_deployment is not null)
			{
				colour = _deployment(province).ToGodot();
			}
			else if (_theatreEditor is not null && Player is { } editing && !province.IsSea)
			{
				colour = _theatreEditor.Colour(_state, editing, province, RegionColours).ToGodot();
			}
			else if (_airArea is not null)
			{
				colour = AirAreaOver(province, _airArea(province));
			}
			else if (air is not null)
			{
				// The air map mode colours the sea as well. Ours, after the original's shaders
				// as far as they were looked at: on land each colour is laid at its alpha over
				// the ground's colour turned grey, and on the sea at the water's 0.8.
				(CColor ground, CColor stripes) = air.TryGetValue(id, out var both)
					? both
					: (CAirMapColours.Grey, CAirMapColours.Grey);
				colour = AirOver(province, ground);
				stripe = AirOver(province, stripes);
			}
			else if (naval is not null)
			{
				(CColor ground, CColor stripes) = naval.TryGetValue(id, out var both)
					? both
					: (CNavalMapColours.Grey, CNavalMapColours.Grey);
				colour = AirOver(province, ground);
				stripe = AirOver(province, stripes);
			}
			else if (province.IsSea)
			{
				colour = SeaColour;
			}
			else if (_mode == MapMode.Terrain)
			{
				colour = province.Terrain?.Color.ToGodot() ?? UnownedColour;
			}
			else if (_mode == MapMode.Supply)
			{
				(CColor ground, CColor stripes) = CSupplyMapColours.Of(_state, province, Player);
				colour = ground.ToGodot();
				stripe = stripes.ToGodot();
			}
			else if (_mode == MapMode.Revolt)
			{
				colour = CRevoltMapColours.Of(_state, province, Player).ToGodot();
			}
			else if (_mode == MapMode.Weather)
			{
				colour = CWeatherMapColours.Of(_state, province, Player).ToGodot();
			}
			else if (_mode == MapMode.Infrastructure)
			{
				colour = CInfrastructureMapColours.Of(_state, province, Player).ToGodot();
			}
			else if (_mode == MapMode.VictoryPoints)
			{
				colour = CVictoryPointMapColours.Of(_state, province, Player).ToGodot();
			}
			else if (_mode == MapMode.Strength)
			{
				landUnits ??= CStrengthMapColours.LandUnits(_state);
				colour = CStrengthMapColours.Of(_state, province, Player, landUnits).ToGodot();
			}
			else if (_mode == MapMode.Resources)
			{
				(CColor ground, CColor stripes) = CResourceMapColours.Of(_state, province, Player);
				colour = ground.ToGodot();
				stripe = stripes.ToGodot();
			}
			else if (_mode == MapMode.SimplifiedTerrain)
			{
				colour = CSimplifiedTerrainColours.Of(_state, province, Player).ToGodot();
			}
			else if (_mode == MapMode.Region)
			{
				colour = (_continentPainting
					? CRegionMapColours.OfContinent(_state, province, Player, ContinentColours)
					: CRegionMapColours.Of(_state, province, Player, RegionColours)).ToGodot();
			}
			else if (_mode == MapMode.Diplomatic)
			{
				(CColor ground, CColor stripes) = CDiplomaticMapColours.Of(_state, province, Player);
				colour = ground.ToGodot();
				stripe = stripes.ToGodot();
			}
			else if (_mode == MapMode.Intel)
			{
				colour = CIntelMapColours.Of(_state, province, Player).ToGodot();
			}
			else if (theatres is not null)
			{
				float b = CVictoryPointMapColours.Brightness(_state, province, Player);
				colour = (theatres.TryGetValue(id, out CColor painted)
					? CSimplifiedTerrainColours.Scaled(painted, b)
					: new CColor(0.5f * b, 0.5f * b, 0.5f * b)).ToGodot();
			}
			else
			{
				// The controller's colour with the owner's as the thin stripes - the other way round in the lobby
				// (mapmodes.md, section 1).
				(CColor ground, CColor stripes) = CPoliticalMapColours.Of(_state, province, Player, IsLobby);
				colour = ground.ToGodot();
				stripe = stripes.ToGodot();
			}
			if (_places is not null && _places.Contains(id))
			{
				colour = colour.Lerp(PlaceColour, PlaceStrength);
				stripe = stripe?.Lerp(PlaceColour, PlaceStrength);
			}
			_paletteImage.SetPixel(id % PaletteWidth, id / PaletteWidth, colour);
			_stripeImage.SetPixel(id % PaletteWidth, id / PaletteWidth, stripe ?? colour);
		}
		_paletteTexture.Update(_paletteImage);
		_stripeTexture.Update(_stripeImage);
		_surface?.SetColours(_paletteImage, _stripeImage, KnownWell);
		// The borders follow at most four times a second: at the top speed of a war, hands change most hours.
		_bordersDirty = true;
		_names?.Refresh();
	}

	/// <summary>
	/// Whether the player knows a province at 2 or more, which the surface draws unfogged (<c>mapdraw.md</c>, section 5);
	/// with nobody playing, every province.
	/// </summary>
	private float KnownWell(int id)
	{
		if (Player is not { } player || id <= 0 || id >= _state.Provinces.Count)
		{
			return 1f;
		}
		return CProvinceIntel.Level(_state, _state.Provinces[id], player) >= KnownWellLevel ? 1f : 0f;
	}

	private const int KnownWellLevel = 2;

	/// <summary>How much of a province's colour the original's water shader lays over the sea.</summary>
	private const float WaterTakes = 0.8f;

	/// <summary>
	/// An air or naval map mode colour as it is handed on. The surface's shaders - the install's terrain_2_0.fx and water.fx,
	/// the sea's Color techniques - grey the ground and lay the colour over the land and the water themselves, so the surface
	/// is given the colour as it is; only the flat map, with no surface, is given it laid over the sea or the ground turned
	/// grey here, a stand-in for those shaders.
	/// </summary>
	private Color AirOver(CProvince province, CColor air)
	{
		var colour = new Color(air.R, air.G, air.B);
		if (_surface is not null)
		{
			// Its alpha with it: the lookups and the sea's rasters carry it in these styles (mapdraw.md, section 5.1).
			return new Color(air.R, air.G, air.B, air.A);
		}
		if (province.IsSea)
		{
			return SeaColour.Lerp(colour, WaterTakes);
		}
		Color terrain = province.Terrain?.Color.ToGodot() ?? UnownedColour;
		float grey = terrain.Luminance;
		return new Color(grey, grey, grey).Lerp(colour, air.A);
	}

	/// <summary>
	/// An air orders window's colour as it is handed on: to the surface as it is, alpha and all, its shaders drawing
	/// "nothing" as the original's do; to the flat map, with no surface, a province given nothing as those shaders would
	/// draw it - the ground turned grey and darkened, the sea a fifth of its colour - and any other colour flat.
	/// </summary>
	private Color AirAreaOver(CProvince province, CColor air)
	{
		if (_surface is not null || air != CAirAreaColours.Nothing)
		{
			return new Color(air.R, air.G, air.B, air.A);
		}
		if (province.IsSea)
		{
			Color sea = SeaColour * CAirAreaColours.NothingOverSea;
			return new Color(sea.R, sea.G, sea.B);
		}
		Color terrain = province.Terrain?.Color.ToGodot() ?? UnownedColour;
		float grey = CAirAreaColours.NothingOverLand(terrain.Luminance);
		return new Color(grey, grey, grey);
	}

	// --- the camera ---

	/// <summary>The zoom at which the whole height of the map just fits the screen.</summary>
	private float MinZoom()
	{
		return GetViewportRect().Size.Y / _map.Height;
	}

	/// <summary>
	/// The closest the map is seen from: as close as the original's camera goes. Ours for
	/// the other end: the original's camera stops at twice the height of its far view,
	/// and here the whole map can be seen at once.
	/// </summary>
	private float MaxZoom()
	{
		return CMapCamera.ZoomAt(_leastHeight, GetViewportRect().Size.Y);
	}

	private float _leastHeight = CMapCamera.LowestHeight;

	/// <summary>The console's <c>debug minzoom</c>: the lowest height the camera comes down to.</summary>
	private CPlanMarkerLayer? _planMarkers;

	/// <summary>Each unit's drawn front line, by the area it was built from.</summary>
	private readonly Dictionary<CUnit, List<int>> _fronts = new();

	/// <summary>
	/// A line goes with its unit's selection or its agent, and comes when a single headquarters of the player's is selected under an AI,
	/// or when its area changes; an area gone empty leaves the line as it was (mapunits.md, section 11).
	/// </summary>
	private void UpdateFrontLines()
	{
		if (_surface is null)
		{
			return;
		}
		foreach (CUnit unit in _fronts.Keys.ToArray())
		{
			if (!_units.Contains(unit) || CAgents.HighestAutomated(unit) is null)
			{
				_fronts.Remove(unit);
				_surface.ClearFront(unit.Id);
			}
		}
		if (_units.Single is not { } selected || selected.Owner != _player || CAgents.HighestAutomated(selected) is null || selected.Plan.Area.Count == 0
			|| (_fronts.TryGetValue(selected, out List<int>? built) && built.SequenceEqual(selected.Plan.Area)))
		{
			return;
		}
		_fronts[selected] = selected.Plan.Area.ToList();
		_surface.SetFront(selected.Id, CFrontLines.Build(_state, selected, province =>
		{
			CProvince shown = _state.Province(province);
			return new System.Numerics.Vector2(shown.CentreX, shown.CentreY);
		}));
	}

	/// <summary>The console's <c>debug render</c>: the bits of the map's layers left on.</summary>
	public int RenderLayers
	{
		get => _renderLayers;
		set
		{
			_renderLayers = value;
			_surface?.SetLayers((value & 0x1) != 0, (value & 0x2) != 0, (value & 0x80) != 0, (value & 0x100) != 0, (value & 0x200) != 0);
			if (_provinceNames is not null)
			{
				_provinceNames.Visible = TextShown;
			}
		}
	}

	private int _renderLayers = 0xFFFF;

	private bool TextShown => (_renderLayers & 0x8) != 0;

	/// <summary>The bits of the map's layers the mode shown leaves on [exe].</summary>
	public int ModeLayers => _mode switch
	{
		MapMode.Terrain => 0xFFFF,
		MapMode.Political or MapMode.Revolt => 0xFFBE,
		MapMode.Weather => 0xFFBB,
		MapMode.Resources => 0xFFAA,
		MapMode.Air or MapMode.Naval => 0xFF9A,
		_ => 0xFFBA,
	};

	/// <summary>Where the camera stands and how near, for the session that takes the game over.</summary>
	public (Vector2 Centre, float Zoom) View
	{
		get => (_camera.Position, _camera.Zoom.X);
		set
		{
			StopEase(height: true);
			_camera.Position = value.Centre;
			SetZoom(value.Zoom);                                // the borders' shading with the zoom, as every other zoom sets it
		}
	}

	public void SetLeastHeight(int height)
	{
		_leastHeight = height;
	}

	private void SetZoom(float zoom)
	{
		zoom = Mathf.Clamp(zoom, MinZoom(), MaxZoom());
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
		_surface?.SetView(position, _camera.Zoom.X, GetViewportRect().Size);
		CameraMoved?.Invoke();
	}

	/// <summary>Raised whenever the camera moves or its height changes: what the minimap's frame follows.</summary>
	public event Action? CameraMoved;

	/// <summary>Where the camera looks, in the map's pixels with y counted north, as the original's camera holds it.</summary>
	public System.Numerics.Vector2 CameraOnMap => new(_camera.Position.X, _map.Height - _camera.Position.Y);

	/// <summary>How high the original's camera would be to show the map as it is shown now.</summary>
	public float CameraHeight => CMapCamera.HeightAt(_camera.Zoom.X, GetViewportRect().Size.Y);

	/// <summary>The map's size in its pixels.</summary>
	public System.Numerics.Vector2 MapSize => new(_map.Width, _map.Height);

	/// <summary>
	/// The camera over a point of the map at once, its height left alone: what a press on the minimap does
	/// (<c>docs/mechanics/minimap.md</c>, section 2). Y is counted north.
	/// </summary>
	public void JumpTo(float x, float yNorth)
	{
		StopEase(height: false);
		_camera.Position = new Vector2(x, _map.Height - yNorth);
		ClampCamera();
	}

	/// <summary>Where a point on the screen is on the map, in this node's coordinates.</summary>
	/// <summary>The battle plans' say over a mouse event before the map's; true where the map is to do nothing (battleplans.md, section 9).</summary>
	public Func<InputEvent, bool>? PlanInput { get; set; }

	/// <summary>A point of the screen in the map's own coordinates, y north, as a battle plan holds its points.</summary>
	public (float X, float Y) WorldAt(Vector2 screen)
	{
		Vector2 point = ScreenToMap(screen);
		return (point.X, _map.Height - point.Y);
	}

	/// <summary>The words of battle plans; has to be given before the node enters the tree.</summary>
	public CBattlePlanSigns? PlanSigns { get; set; }

	/// <summary>The surface, where the map is drawn on one.</summary>
	public CMapSurface? Surface3D => _surface;

	private Vector2 ScreenToMap(Vector2 screen)
	{
		return _camera.Position + (screen - GetViewportRect().Size / 2f) / _camera.Zoom;
	}

	/// <summary>The settings the zoom goes by: <c>zoom_speed</c> and <c>use_legacy_camera</c> [mod: settings.txt].</summary>
	public Func<(float ZoomSpeed, bool Legacy)>? CameraSettings { get; set; }

	/// <summary>The height the camera is easing to, or null where it stands at the one it wants.</summary>
	private float? _heightWanted;

	/// <summary>The point the camera is easing to, or null where it stands over it.</summary>
	private Vector2? _aimWanted;

	/// <summary>Whether this frame has had its wheel notch: the original takes one a frame, however far the wheel turned.</summary>
	private bool _notched;

	/// <summary>
	/// A wheel notch, as the original's camera takes it (<c>reversing/findings/FINDINGS-camerazoom.md</c>): the target
	/// height a tenth lower or a fifth higher at the default <c>zoom_speed</c>, sized by the height now; and, unless the
	/// target landed on a limit or the pointer is within six pixels of an edge, the look target pulled about an eighth of
	/// the way towards the point under the pointer, in or out. Both are then eased to, a frame at a time
	/// (<see cref="EaseCamera"/>). With <c>use_legacy_camera</c> the height is taken at once and nothing is pulled.
	/// </summary>
	private void WheelNotch(Vector2 screen, bool inwards)
	{
		if (_notched)
		{
			return;
		}
		_notched = true;
		(float speed, bool legacy) = CameraSettings?.Invoke() ?? (CMapZoom.DefaultSpeed, false);
		Vector2 size = GetViewportRect().Size;
		float height = CameraHeight;
		float lowest = _leastHeight;
		float highest = HighestHeight();
		float wanted = CMapZoom.NotchTarget(_heightWanted ?? height, height, speed, inwards, lowest, highest);
		if (legacy)
		{
			_heightWanted = null;
			SetZoom(CMapCamera.ZoomAt(wanted, size.Y));
			return;
		}
		_heightWanted = wanted;
		bool inside = inwards ? wanted > lowest : highest > wanted;
		if (inside && screen.X > CMapZoom.PullMargin && screen.Y > CMapZoom.PullMargin
			&& screen.X < size.X - CMapZoom.PullMargin && screen.Y < size.Y - CMapZoom.PullMargin)
		{
			Vector2 under = ScreenToMap(screen);
			Vector2 aim = _aimWanted ?? _camera.Position;
			float left = CMapZoom.PullLeft(height, speed);
			_aimWanted = new Vector2(CMapZoom.Pull(aim.X, under.X, left), CMapZoom.Pull(aim.Y, under.Y, left));
		}
	}

	/// <summary>
	/// The highest the camera goes. *Ours*: the original's stops at 1000; here it goes on until the whole map is on
	/// screen, as it did before the zoom eased.
	/// </summary>
	private float HighestHeight()
	{
		return CMapCamera.HeightAt(MinZoom(), GetViewportRect().Size.Y);
	}

	/// <summary>
	/// One frame of the original's ease: the height and the look point each close 0.197 of the gap to the ones wanted -
	/// a frame, not a second, as the original's does (<c>docs/enhanced.md</c>). A few thousandths short is taken as there.
	/// </summary>
	private void EaseCamera()
	{
		if (_heightWanted is null && _aimWanted is null)
		{
			return;
		}
		Vector2 size = GetViewportRect().Size;
		if (_heightWanted is { } wanted)
		{
			float next = CMapZoom.EaseTowards(CameraHeight, wanted);
			if (Math.Abs(wanted - next) <= wanted * 1e-5f)
			{
				next = wanted;
				_heightWanted = null;
			}
			_camera.Zoom = Vector2.One * Mathf.Clamp(CMapCamera.ZoomAt(next, size.Y), MinZoom(), MaxZoom());
			_material.SetShaderParameter("province_border", Mathf.SmoothStep(1.2f, 2.5f, _camera.Zoom.X));
		}
		if (_aimWanted is { } aim)
		{
			Vector2 from = _camera.Position;
			// The short way round the map: the camera folds back onto the middle copy, the aim may not have.
			aim.X = from.X + Mathf.Wrap(aim.X - from.X, -_map.Width / 2f, _map.Width / 2f);
			Vector2 next = new(CMapZoom.EaseTowards(from.X, aim.X), CMapZoom.EaseTowards(from.Y, aim.Y));
			if ((aim - next).Length() <= 0.01f)
			{
				next = aim;
				_aimWanted = null;
			}
			_camera.Position = next;
		}
		ClampCamera();
		if (_aimWanted is { } kept)
		{
			// Held to what the camera may show from north to south at its height now, as the original holds its target the
			// next frame, so that the ease ends.
			float half = size.Y / _camera.Zoom.Y / 2f;
			float y = half * 2f >= _map.Height ? _map.Height / 2f : Mathf.Clamp(kept.Y, half, _map.Height - half);
			Vector2 from = _camera.Position;
			_aimWanted = new Vector2(from.X + Mathf.Wrap(kept.X - from.X, -_map.Width / 2f, _map.Width / 2f), y);
		}
	}

	/// <summary>A drag or the keys move the camera, and the point it is easing to with it, as the original's move its target.</summary>
	private void ShiftAim(Vector2 by)
	{
		if (_aimWanted is { } aim)
		{
			_aimWanted = aim + by;
		}
	}

	/// <summary>Whatever puts the camera somewhere at once ends its ease there; with a height too, the height's.</summary>
	private void StopEase(bool height)
	{
		_aimWanted = null;
		if (height)
		{
			_heightWanted = null;
		}
	}

	/// <summary>
	/// The zoom buttons by the minimap and their keys, Page Down and Page Up: the target height a fifth of itself lower or
	/// higher, held between the lowest and 1000, about the middle of the screen, eased as a notch is - or at once with
	/// <c>use_legacy_camera</c> (<c>minimap.md</c>, section 4; <c>reversing/findings/FINDINGS-camerazoom.md</c>, section 4) [exe].
	/// </summary>
	public void StepZoom(bool inwards)
	{
		Sounds?.PlayUnlessPlaying("click");
		float wanted = CMapZoom.ButtonTarget(_heightWanted ?? CameraHeight, inwards, _leastHeight, CMapCamera.HighestHeight);
		if (CameraSettings?.Invoke().Legacy == true)
		{
			_heightWanted = null;
			SetZoom(CMapCamera.ZoomAt(wanted, GetViewportRect().Size.Y));
			return;
		}
		_heightWanted = wanted;
	}

	/// <summary>Puts a province in the middle of the screen, using its bounding box.</summary>
	public void CentreOn(int provinceId, float? zoom = null)
	{
		if (provinceId <= 0 || provinceId >= _map.ProvinceCount || _map.Shapes[provinceId].IsEmpty)
		{
			return;
		}
		CProvinceShape shape = _map.Shapes[provinceId];
		StopEase(height: zoom is not null);
		_camera.Position = new Vector2(shape.CentreX + 0.5f, _map.DisplayRow(shape.CentreY) + 0.5f);
		SetZoom(zoom ?? _camera.Zoom.X);
	}

	/// <summary>A province in the middle of the screen, seen from a height of the original's camera.</summary>
	public void CentreOnFrom(int provinceId, float height)
	{
		CentreOn(provinceId, CMapCamera.ZoomAt(height, GetViewportRect().Size.Y));
	}

	/// <summary>
	/// The camera over a point of the map, in the map's pixels with its rows counted from the bottom as the original
	/// counts them, at a zoom.
	/// </summary>
	public void LookAt(float x, int rowFromBottom, float zoom)
	{
		StopEase(height: true);
		_camera.Position = new Vector2(x, _map.DisplayRow(rowFromBottom) + 0.5f);
		SetZoom(zoom);
		ClampCamera();
	}

	/// <summary>The province alone selected, or with 0 none.</summary>
	public void Select(int provinceId)
	{
		_selection.Clear();
		if (provinceId > 0)
		{
			_selection.Add(provinceId);
		}
		Show(provinceId);
	}

	/// <summary>
	/// Shift and a click on a province: a selected one is taken out, the view going to the first left; another is added
	/// and shown (<c>docs/mechanics/provinceview.md</c>, section 1).
	/// </summary>
	public void ToggleSelected(int provinceId)
	{
		if (_selection.Remove(provinceId))
		{
			Show(_selection.Count > 0 ? _selection[0] : 0);
			return;
		}
		_selection.Add(provinceId);
		Show(provinceId);
	}

	/// <summary>Lets go of everything selected, units and provinces - Escape, or the province view's close button.</summary>
	public void ClearSelection()
	{
		SelectUnit(null);
		Select(0);
	}

	/// <summary>
	/// The province view goes to the province; every selected province is lit on the map, as the original lights each
	/// of several (provinceview.md, section 1).
	/// </summary>
	private void Show(int provinceId)
	{
		// A province selected clicks (sound.md, section 7).
		if (provinceId > 0)
		{
			Sounds?.PlayUnlessPlaying("click");
		}
		_selected = provinceId;
		LightSelection();
		EmitSignal(SignalName.ProvinceSelected, provinceId);
	}

	/// <summary>Lights these provinces and no others, telling nobody: the lobby's whole chosen country.</summary>
	public void Light(IEnumerable<int> provinces)
	{
		_selection.Clear();
		_selection.AddRange(provinces);
		LightSelection();
	}

	private void LightSelection()
	{
		_selectionImage.Fill(Colors.Black);
		foreach (int province in _selection)
		{
			_selectionImage.SetPixel(province % PaletteWidth, province / PaletteWidth, Colors.White);
		}
		_selectionTexture.Update(_selectionImage);
		// On the surface the selected provinces are lifted in its lookups, and nothing marks the one under the mouse (B.9).
		var lit = new HashSet<int>(_selection);
		_surface?.SetSelection(lit.Contains);
	}

	/// <summary>The units selected, in the order selected.</summary>
	public CSelection Units => _units;

	/// <summary>The units selected when the selection last changed, so that only a unit newly selected sounds.</summary>
	private readonly HashSet<CUnit> _sounded = new(ReferenceEqualityComparer.Instance);

	/// <summary>The sound effects (<c>docs/mechanics/sound.md</c>): none in the front end's map until it is given them.</summary>
	public Audio.CSoundEffects? Sounds { get; set; }

	/// <summary>
	/// Selects one unit alone - its counter wears a ring - or with null lets every unit go. Asked by the panels and the
	/// developer's switches, so the unit need not be the player's.
	/// </summary>
	public void SelectUnit(CUnit? unit)
	{
		if (_units.Count == (unit is null ? 0 : 1) && (unit is null || ReferenceEquals(_units.First, unit)))
		{
			return;
		}
		_units.Clear();
		if (unit is not null)
		{
			_units.Force(unit);
		}
		UnitsChanged();
	}

	/// <summary>Changes the units selected as the panel's rows ask, and tells everyone.</summary>
	public void ChangeUnits(Action<CSelection> change)
	{
		change(_units);
		if (_units.Count > 0)
		{
			Select(0);
		}
		UnitsChanged();
	}

	/// <summary>The counters take up the selection and the session is told.</summary>
	private void UnitsChanged(bool keepGroup = false)
	{
		if (!keepGroup)
		{
			_lastGroup = -1;
		}
		// The theatre editor ends once the selection is no longer exactly one theatre headquarters (attaching.md, section 7).
		if (_theatreEditor is not null && !CTheatreEditor.Keeps(_units.Units))
		{
			SetTheatreEditing(null);
		}
		// Each unit newly selected sounds as its kind - a fleet, an air unit, else an army - and a sound already playing
		// is not started again, so a box of twenty divisions is heard once (sound.md, section 7).
		foreach (CUnit unit in _units.Units)
		{
			if (!_sounded.Contains(unit))
			{
				Sounds?.PlayUnlessPlaying(CSelectionSounds.Selected(unit));
			}
		}
		_sounded.Clear();
		_sounded.UnionWith(_units.Units);
		_counters?.SelectionChanged();
		EmitSignal(SignalName.UnitSelected, _units.First?.Id ?? 0);
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

	/// <summary>The unit whose counter is under the mouse, or null: over a counter the map's tooltip is the unit's (tooltips.md, section 7.1).</summary>
	public CUnit? HoveredUnit => _counters is not null && _mouseOnMap ? _counters.UnitAt(ScreenToMap(_mouse)) : null;

	/// <summary>The port or airfield whose picture is under the mouse, asked as a click asks: the air map's large base, then any counter, then the pictures (tooltips.md, section 7.2).</summary>
	public (int Province, EMapBuilding Kind)? HoveredBase
	{
		get
		{
			if (_counters is null || !_mouseOnMap)
			{
				return null;
			}
			Vector2 point = ScreenToMap(_mouse);
			if (_counters.AirMap.AirBaseAt(point) is int airfield and > 0)
			{
				return (airfield, EMapBuilding.Airfield);
			}
			if (_counters.UnitAt(point) is not null || _counters.Buildings.At(point) is not { Kind: not EMapBuilding.Other } picture)
			{
				return null;
			}
			return (picture.Province, picture.Kind);
		}
	}

	private Vector2 _mouse;
	private bool _mouseOnMap;

	private void UpdateHover(Vector2 screen)
	{
		_mouse = screen;
		_mouseOnMap = true;
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
		if (PlanInput?.Invoke(@event) == true)
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		switch (@event)
		{
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel when !WheelStill:
				WheelNotch(wheel.Position, inwards: true);
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel when !WheelStill:
				WheelNotch(wheel.Position, inwards: false);
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Middle } button:
				_dragging = button.Pressed;
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Right } button:
				// The right button orders the selected unit when it is let go where it went down. It does not move the
				// map - decided on 2026-11-12; the middle button, the keys and the minimap do.
				if (button.Pressed)
				{
					_rightPress = button.Position;
				}
				else if (_rightPress is { } down && (button.Position - down).Length() <= ClickSlack && _units.Count > 0
					&& _places is null && !LooksOn && ProvinceAtScreen(button.Position) is int target && target > 0)
				{
					_rightPress = null;
					UnitsOrdered?.Invoke(new List<CUnit>(_units.Units), target, button.ShiftPressed);
				}
				else
				{
					_rightPress = null;
				}
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press:
				// The press only marks where the gesture began; its release is the click or the end of a drag
				// (selection.md, section 2).
				_press = ScreenToMap(press.Position);
				if (_theatreEditor is { } starting)
				{
					Vector2 point = PositionsFrame(_press.Value);
					starting.Begin(point.X, point.Y);
				}
				break;
			case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release when _press is { } from:
				_press = null;
				_box?.HideBox();
				Vector2 to = ScreenToMap(release.Position);
				if (_theatreEditor is { } painting && CBoxSelection.IsDrag(from.X, from.Y, to.X, to.Y))
				{
					// The editor's drag paints and selects nothing: what is under the box goes to the selected unit's theatre.
					if (_units.Units.FirstOrDefault() is { } selected)
					{
						Give(painting, selected);
					}
					painting.Release();
					RefreshColours();
				}
				else if (_places is null && CBoxSelection.IsDrag(from.X, from.Y, to.X, to.Y))
				{
					BoxSelect(from, to, release.ShiftPressed);
				}
				else
				{
					Click(release);
				}
				break;
			case InputEventMouseMotion motion when _press is { } start && !_dragging:
				UpdateHover(motion.Position);
				Vector2 here = ScreenToMap(motion.Position);
				if (_places is null && CBoxSelection.IsDrag(start.X, start.Y, here.X, here.Y))
				{
					_box?.ShowBox(start, here, _camera.Zoom.X);
					// The editor gathers the box again each time the pointer has moved more than two map units.
					if (!GameBusy && _theatreEditor is { } gathering && Player is { } painter
						&& CTheatreEditor.TheatreOf(_state, gathering.Headquarters) is { } theatre)
					{
						Vector2 a = PositionsFrame(start);
						Vector2 b = PositionsFrame(here);
						if (gathering.ShouldGather(b.X, b.Y))
						{
							gathering.Gather(_state, painter, theatre, a.X, a.Y, b.X, b.Y, _map.Width);
							RefreshColours();
						}
					}
				}
				else
				{
					_box?.HideBox();
				}
				break;
			case InputEventMouseMotion motion when _dragging:
				_camera.Position -= motion.Relative / _camera.Zoom;
				ShiftAim(-motion.Relative / _camera.Zoom);
				ClampCamera();
				break;
			case InputEventMouseMotion motion:
				UpdateHover(motion.Position);
				break;
			case InputEventKey { Pressed: true, Echo: false } key when GroupOf(key) is int group:
				GroupKey(group, key.CtrlPressed);
				GetViewport().SetInputAsHandled();
				break;
			case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
				when _selection.Count > 0 || _units.Count > 0:
				// Escape with anything selected lets it all go (provinceview.md, section 1).
				ClearSelection();
				GetViewport().SetInputAsHandled();
				break;
		}
	}

	/// <summary>
	/// The release of the left button where it was no drag: what is under the pointer is selected, or with Shift added
	/// or taken out, or with Ctrl brings the land units under it (selection.md, section 2.1).
	/// </summary>
	private void Click(InputEventMouseButton click)
	{
		// A building being placed is asked first: where it may go the click places it, and elsewhere the click
		// goes on as an ordinary click (buildinglines.md, section 6).
		if (_deployment is not null && PlaceBuilding is { } place && place(ProvinceAtScreen(click.Position)))
		{
			return;
		}
		bool shift = click.ShiftPressed;
		bool anything = _units.Count > 0 || _selection.Count > 0;
		// A counter is over the province it stands in, so it is asked first - but
		// not while a unit is being placed, when the click is for the province.
		Vector2 clicked = ScreenToMap(click.Position);
		// A battle's marker is drawn over the counters, so it is asked before them. It never joins a selection.
		if (_places is null && _counters?.Markers.At(clicked) is { } battle)
		{
			if (!shift || !anything)
			{
				CombatSelected?.Invoke(battle);
			}
			return;
		}
		// In the air map mode a large air base is asked before any counter, which is
		// how an air unit at its base is picked whatever stands over it.
		CUnit? unit = null;
		bool building = false;
		CUnit? lone = _units.Single;
		bool picks = LooksOn || Player is not null;
		if (_places is null && picks && _counters is not null && _counters.AirMap.AirBaseAt(clicked) is int airfield and > 0)
		{
			unit = CMapBuildings.Next(_state, Picker, airfield, EMapBuilding.Airfield, lone);
			building = true;
		}
		if (unit is null && _places is null && _counters?.Click(clicked) is { } counter)
		{
			unit = counter;
			building = false;
		}
		// Then a port or an airfield, which is how a fleet in port and an air unit
		// at its base are picked: each click the next of the player's units there.
		if (unit is null && _places is null && picks && _counters is not null
			&& _counters.Buildings.At(clicked) is { Kind: not EMapBuilding.Other } picture)
		{
			unit = CMapBuildings.Next(_state, Picker, picture.Province, picture.Kind, lone);
			building = true;
		}
		if (unit is not null)
		{
			if (shift && anything)
			{
				// Shift: a counter's unit taken out or added where it is of the selection's kind; a port's or an
				// airfield's never joins units, and nothing joins provinces.
				if (building || _selection.Count > 0)
				{
					return;
				}
				if (!_units.Remove(unit) && !_units.Add(unit, Selector(unit)))
				{
					return;
				}
				UnitsChanged();
				return;
			}
			// A plain click lets everything go - the provinces and the province view with them - and selects the
			// unit where it is the player's; with Ctrl a land unit brings its whole command.
			_units.Clear();
			Select(0);
			if (_units.Add(unit, Selector(unit)) && click.CtrlPressed && CSelection.KindOf(unit) == ESelectionKind.Land)
			{
				_units.AddCommand(unit);
			}
			UnitsChanged();
			return;
		}
		int province = ProvinceAtScreen(click.Position);
		if (_places is not null)
		{
			// While a unit is being placed each click on a province places the next.
			SelectUnit(null);
			Select(province);
			return;
		}
		bool sea = province > 0 && province < _state.Provinces.Count && _state.Province(province).IsSea;
		if (shift)
		{
			// Only provinces join provinces: with a unit selected, nothing; a sea zone lets everything go
			// (provinceview.md, section 1).
			if (_units.Count > 0)
			{
				return;
			}
			if (sea || province <= 0)
			{
				ClearSelection();
			}
			else
			{
				ToggleSelected(province);
			}
			return;
		}
		SelectUnit(null);
		Select(sea ? 0 : province);
	}

	/// <summary>
	/// The release of a drag box: the selection let go unless Shift is held with units selected, then the player's land
	/// units in the box, else its fleets, else its air units (selection.md, section 2.2).
	/// </summary>
	private void BoxSelect(Vector2 from, Vector2 to, bool shift)
	{
		BoxReleased?.Invoke();
		if (!shift || _units.Count == 0)
		{
			_units.Clear();
			Select(0);
		}
		if (Player is { } tag && _state.Country(tag) is { } player)
		{
			// The corners in the positions file's frame, where a unit's point is: y counts north, and a point stands
			// half a pixel off the corner of its pixel.
			CBoxSelection.Take(_state, player, _map.Positions, _map.BuildingPositions,
				from.X - 0.5f, _map.Height - 0.5f - from.Y, to.X - 0.5f, _map.Height - 0.5f - to.Y, _map.Width, _units,
				_map.LabelPositions);
		}
		UnitsChanged();
	}

	/// <summary>The selection group a key stands for: a digit, without Shift, which makes it another character.</summary>
	private static int? GroupOf(InputEventKey key)
	{
		if (key.ShiftPressed || key.AltPressed)
		{
			return null;
		}
		return key.Keycode switch
		{
			>= Key.Key0 and <= Key.Key9 => (int)(key.Keycode - Key.Key0),
			>= Key.Kp0 and <= Key.Kp9 => (int)(key.Keycode - Key.Kp0),
			_ => null,
		};
	}

	/// <summary>
	/// A digit: with Ctrl the selected units become its group; pressed again, the map goes to the first selected
	/// unit; otherwise its group is selected (selection.md, section 7).
	/// </summary>
	private void GroupKey(int group, bool ctrl)
	{
		if (ctrl)
		{
			if (_units.Count > 0)
			{
				GroupStored?.Invoke(group, new List<CUnit>(_units.Units));
			}
			return;
		}
		if (_lastGroup == group && _units.First is { } first)
		{
			CentreOn(first.Location);
			return;
		}
		if (Player is not { } tag || _state.Country(tag) is not { } player)
		{
			return;
		}
		Select(0);
		CSelectionGroups.Recall(player, group, _units);
		UnitsChanged();
		_lastGroup = group;
	}

	/// <summary>
	/// Which arrow keys the map may not scroll by now, because something before it takes them - across, down: the
	/// in-game menu holds them all, and an edit box with the keyboard takes Left and Right and lets Up and Down go by
	/// (<c>docs/mechanics/interface.md</c>, section 8). The keys are asked each frame rather than heard, so a key
	/// taken elsewhere has to be said here.
	/// </summary>
	public Func<(bool Across, bool Down)>? ScrollKeysHeld { get; set; }

	/// <summary>The player's <c>counter_distance</c>, which the counters and the battle markers go by.</summary>
	public Func<float> CounterDistance { get; set; } = () => CMapCamera.CounterDistance;

	/// <summary>Whether the wheel is to leave the map alone: a main window or the menu is up.</summary>
	public Func<bool>? WheelHeld { get; set; }

	/// <summary>The lobby's map stays at the height it opens at, and nothing else moves it while the wheel is held.</summary>
	private bool WheelStill => IsLobby || WheelHeld?.Invoke() == true;

	/// <summary>
	/// Whether the game's hours run beside the frame (<c>CSimulation.BeginAdvance</c>): the camera still moves and the
	/// surface follows it, but nothing reads the game - the layers that draw from it keep what they last drew, and what
	/// asks the game waits for the hours to be collected.
	/// </summary>
	public bool GameBusy
	{
		get => _gameBusy;
		set
		{
			if (_gameBusy == value)
			{
				return;
			}
			_gameBusy = value;
			if (_counters is not null)
			{
				_counters.Held = value;
			}
			if (_names is not null)
			{
				_names.Redraw.Held = value;
			}
			if (_planMarkers is not null)
			{
				_planMarkers.Redraw.Held = value;
			}
		}
	}

	private bool _gameBusy;

	public override void _Process(double delta)
	{
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		if (ScrollKeysHeld?.Invoke() is var (across, down))
		{
			direction = new Vector2(across ? 0f : direction.X, down ? 0f : direction.Y);
		}
		if (direction != Vector2.Zero)
		{
			Vector2 scrolled = direction * (KeyScrollSpeed * (float)delta / _camera.Zoom.X);
			_camera.Position += scrolled;
			ShiftAim(scrolled);
			ClampCamera();
			UpdateHover(GetViewport().GetMousePosition());
		}
		EaseCamera();
		_notched = false;
		// The surface follows the 2D camera wherever it was moved from; nothing is made again where it has not.
		_surface?.SetView(_camera.Position, _camera.Zoom.X, GetViewportRect().Size);
		_provinceNames?.SetView(_camera.Position, _camera.Zoom.X, GetViewportRect().Size, delta);
		_mapTexts?.SetView(CameraHeight);
		_sinceBorders += delta;
		// While the game's hours run beside the frame the camera moves and nothing of the game is read: what follows
		// reads it, and waits for them.
		if (GameBusy)
		{
			return;
		}
		UpdateFrontLines();
		if (_planMarkers is not null && _counters is not null)
		{
			CPlanMarkers.CMarkers markers = CPlanMarkers.Of(_state, _state.Country(_player), _units.Single);
			_planMarkers.SetObjectives(markers.Objectives);
			_counters.PlanArrows = markers.Arrows;
		}
		if (_names is not null)
		{
			_names.Visible = NamesShown() && TextShown;
		}
		if (_bordersDirty && (_sinceBorders >= BordersInterval || _bordersAt < 0))
		{
			_bordersDirty = false;
			_sinceBorders = 0;
			RefreshBorders();
		}
	}
}
