using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Map;

/// <summary>
/// The units on the map, as counters: one stack to a province, the higher rank on
/// top, the top one with its owner's flag and its two figures. A child of the map view,
/// so its coordinates are map pixels with north at the top. See
/// <c>docs/mechanics/mapunits.md</c>.
/// </summary>
public partial class CUnitCounters : Node2D
{
	/// <summary>Under this many pixels of the screen a counter is not drawn, nor a text written.</summary>
	private const float LeastCounterOnScreen = 6f;
	private const float LeastTextOnScreen = 5f;

	/// <summary>The frame of the pointer's picture for a unit that is marching: the green one.</summary>
	private const int MarchingFrame = 0;

	private static readonly Color[] QuadColours = { Colors.White, Colors.White, Colors.White, Colors.White };

	private CMap _map = null!;
	private CGameState _state = null!;
	private CGuiContext _gui = null!;
	private CCounterKind? _far;
	private CCounterKind? _near;
	private CBillboardType? _pointer;
	private Texture2D? _pointerTexture;
	private readonly Dictionary<string, Texture2D?> _arrowTextures = new(StringComparer.OrdinalIgnoreCase);
	private CHierarchyLayer _hierarchy = null!;
	private CBuildingIcons _buildings = null!;
	private CAirMapLayer _airMap = null!;
	private CBattleMarkers _markers = null!;
	private readonly Vector2[] _quad = new Vector2[4];
	private readonly Vector2[] _quadPicture = new Vector2[4];

	private List<CMapStack> _stacks = new();
	private readonly Dictionary<string, Texture2D?> _flags = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<CMapCounter> _drawn = new();
	private float _zoom = 1f;
	private Rect2 _view;
	private CSelection _selection = new();
	private readonly HashSet<CUnit> _selectedSet = new();

	/// <summary>
	/// Where the counters are drawn: two canvas items to a counter, in the order drawn - its plate through
	/// <c>counter.fx</c>, which is a material of its own, and then its pointer, flag and figures - kept from frame to frame
	/// and refilled.
	/// </summary>
	private Node2D _plates = null!;
	private readonly List<Rid> _items = new();
	private int _itemsUsed;

	/// <summary>The first selected unit, or null: whose hierarchy is drawn and whose reach the air map dims by.</summary>
	private CUnit? _selected => _selection.First;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CMap map, CGameState state, CGuiContext gui, CCounterTypes types, CBillboardTypes billboards,
		CArrowTypes arrows)
	{
		foreach (string name in new[] { CArrowTypes.Move, CArrowTypes.Attack, CArrowTypes.Retreat, CArrowTypes.Invasion, CArrowTypes.Strategic })
		{
			_arrowTextures[name] = arrows.Find(name) is { TextureFile.Length: > 0 } arrow && gui.Textures.Exists(arrow.TextureFile)
				? gui.Textures.LoadFromGamePath(arrow.TextureFile)
				: null;
		}
		_pointer = billboards.Find(CBillboardTypes.CounterArrow);
		if (_pointer is not null && _pointer.TextureFile.Length != 0 && gui.Textures.Exists(_pointer.TextureFile))
		{
			_pointerTexture = gui.Textures.LoadFromGamePath(_pointer.TextureFile);
		}
		_map = map;
		_state = state;
		_gui = gui;
		_far = CCounterKind.Create(gui, types.Find(CCounterTypes.Far), CCounterText.FarSprite, carriesName: false);
		_near = CCounterKind.Create(gui, types.Find(CCounterTypes.Near), CCounterText.NearSprite, carriesName: true);
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;

		// Under the counters: first the buildings, then the lines of a selected unit's hierarchy.
		_buildings = new CBuildingIcons { Name = "Buildings" };
		_buildings.Setup(map, state, gui, billboards);
		AddChild(_buildings);

		// Over the buildings, in the air map mode: its rings, large pictures and icons.
		_airMap = new CAirMapLayer { Name = "AirMap", ShowBehindParent = true };
		_airMap.Setup(map, state, gui, billboards);
		AddChild(_airMap);

		_hierarchy = new CHierarchyLayer { Name = "Hierarchy" };
		_hierarchy.Setup(map, state, gui);
		AddChild(_hierarchy);

		// The counters themselves, over the lines.
		_plates = new Node2D { Name = "Plates" };
		AddChild(_plates);

		// Over the counters: the markers of the battles the player has a part in.
		_markers = new CBattleMarkers { Name = "Battles" };
		_markers.Setup(map, state, gui, billboards, PlaceOfUnit);
		AddChild(_markers);

		// Over all of it, in the weather map mode: the clouds, the rain and the snow (weather.md, section 6).
		_weather = new CWeatherLayer { Name = "Weather" };
		_weather.Setup(map, state, gui, billboards);
		AddChild(_weather);
	}

	private CWeatherLayer _weather = null!;

	/// <summary>Whether the weather map mode is on, which shows its pictures.</summary>
	public bool WeatherMode
	{
		get => _weather.Visible;
		set
		{
			_weather.Visible = value;
			_weather.Refresh();
		}
	}

	/// <summary>Takes again the pictures of the weather, as an hour has left it.</summary>
	public void RefreshWeather()
	{
		if (_weather.Visible)
		{
			_weather.Refresh();
		}
	}

	/// <summary>The buildings on the map, the ports and the airfields among them.</summary>
	public CBuildingIcons Buildings => _buildings;

	/// <summary>The markers of the battles being fought.</summary>
	public CBattleMarkers Markers => _markers;

	/// <summary>What the air map mode draws over the map.</summary>
	public CAirMapLayer AirMap => _airMap;

	/// <summary>The map's surface, which draws the hierarchy's lines and the air bases' rings where there is one.</summary>
	public CMapSurface? Surface
	{
		set
		{
			_hierarchy.Surface = value;
			_airMap.Surface = value;
		}
	}

	/// <summary>Whether the air map mode is on, which shows its layer and works it out.</summary>
	public bool AirMode
	{
		get => _airMap.Visible;
		set
		{
			_airMap.Visible = value;
			if (value)
			{
				_airMap.Refresh(_selected);
			}
		}
	}

	/// <summary>
	/// Where a unit's counter stands on the middle copy of the map: its stack's place,
	/// or its province's own point for a unit that has no counter.
	/// </summary>
	private Vector2 PlaceOfUnit(CUnit unit)
	{
		if (unit.Location <= 0 || unit.Location >= _map.ProvinceCount)
		{
			return Vector2.Zero;
		}
		CCounterKind? kind = _near ?? _far;
		return kind is not null && CMapCounters.HasCounter(_state, unit)
			? PlaceOf(new CMapStack(unit.Location, CMapCounters.StackOf(unit)), kind)
			: PointOf(unit.Location);
	}

	public override void _Ready()
	{
		Refresh();
	}

	/// <summary>The first unit selected, or null.</summary>
	public CUnit? Selected => _selected;

	/// <summary>The country being played, whose counters alone show a mark in their corner.</summary>
	public CCountryTag? Player
	{
		get => _player;
		set
		{
			_player = value;
			QueueRedraw();
		}
	}

	private CCountryTag? _player;

	/// <summary>Raised when a refresh took out of the selection units that are no longer anywhere.</summary>
	public event Action? SelectionLost;

	/// <summary>
	/// The units whose counters wear the white ring, in the order selected: the map view's own selection, which the
	/// counters are told of with <see cref="SelectionChanged"/>.
	/// </summary>
	public CSelection Selection
	{
		get => _selection;
		set
		{
			_selection = value;
			SelectionChanged();
		}
	}

	/// <summary>Takes up what the selection now holds: the rings, the lines of the hierarchy and the air map's rings.</summary>
	public void SelectionChanged()
	{
		_selectedSet.Clear();
		_selectedSet.UnionWith(_selection.Units);
		// The lines of the hierarchy are the first selected land unit's alone (selection.md, section 4).
		_hierarchy.Unit = _selection.Units.FirstOrDefault(unit => CSelection.KindOf(unit) == ESelectionKind.Land);
		// The air map mode's rings are dimmed by what is selected.
		if (_airMap.Visible)
		{
			_airMap.RefreshCircles(_selected);
		}
		QueueRedraw();
	}

	/// <summary>
	/// Whether the selected unit's hierarchy is drawn on the map: the original's
	/// <c>render_hierarchy</c> setting. Nothing keeps it between games yet.
	/// </summary>
	public bool HierarchyShown
	{
		get => _hierarchy.Shown;
		set => _hierarchy.Shown = value;
	}

	/// <summary>Works out again which units are where. Call it when one has been placed, made or moved.</summary>
	public void Refresh()
	{
		// Standing stacks first, so that one on the move, which overlaps what stands in
		// its province, is drawn over it and is the one a click there finds.
		_stacks = CMapCounters.Build(_state).OrderBy(stack => stack.Kind).ToList();
		// A fleet in port and an air unit at its base have no counter and stay selected
		// all the same, and so does a land unit gone aboard; a unit that is no longer anywhere does not.
		if (_selection.Count > 0 && _selection.RemoveWhere(unit => unit.IsRemoved || !CMapCounters.IsSomewhere(_state, unit)))
		{
			SelectionChanged();
			SelectionLost?.Invoke();
		}
		_buildings.Refresh();
		_markers.Refresh();
		QueueRedraw();
	}

	/// <summary>Told by the map view whenever what is on screen changes.</summary>
	public void ViewChanged(float zoom, Rect2 view)
	{
		_zoom = zoom;
		_view = view;
		// In the far view the map is bare: no counter, no building, no line of a hierarchy.
		bool close = !CMapCamera.IsFarView(CameraHeight());
		_buildings.Visible = close;
		_hierarchy.Visible = close;
		_markers.Visible = close;
		_buildings.ViewChanged(view);
		_airMap.ViewChanged(view);
		_markers.ViewChanged(zoom, view);
		_weather.ViewChanged(view);
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_selected is not null)
		{
			// A selected counter's ring pulses by the clock's seconds, one figure for every counter.
			CCounterKind.SetPulse(Time.GetTicksMsec() / 1000.0);
		}
	}

	public override void _ExitTree()
	{
		foreach (Rid item in _items)
		{
			RenderingServer.FreeRid(item);
		}
		_items.Clear();
		_itemsUsed = 0;
	}

	/// <summary>The next canvas item of the counters, emptied, with no material, at its place in the order drawn.</summary>
	private Rid NextItem()
	{
		if (_itemsUsed == _items.Count)
		{
			Rid made = RenderingServer.CanvasItemCreate();
			RenderingServer.CanvasItemSetParent(made, _plates.GetCanvasItem());
			RenderingServer.CanvasItemSetDrawIndex(made, _items.Count);
			RenderingServer.CanvasItemSetDefaultTextureFilter(made, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
			_items.Add(made);
		}
		Rid item = _items[_itemsUsed++];
		RenderingServer.CanvasItemClear(item);
		RenderingServer.CanvasItemSetMaterial(item, new Rid());
		return item;
	}

	/// <summary>Empties every canvas item of the counters, ready to be filled again.</summary>
	private void ClearItems()
	{
		for (int index = 0; index < _itemsUsed; index++)
		{
			RenderingServer.CanvasItemClear(_items[index]);
			RenderingServer.CanvasItemSetMaterial(_items[index], new Rid());
		}
		_itemsUsed = 0;
	}

	/// <summary>How high the original's camera would be to show the map as it is shown now.</summary>
	private float CameraHeight()
	{
		return CMapCamera.HeightAt(_zoom, GetViewportRect().Size.Y);
	}

	/// <summary>
	/// The counter type for the present view: the near one from close by, the far one
	/// from further off, and none at all in the far view, where the map is bare.
	/// </summary>
	private CCounterKind? Kind()
	{
		float height = CameraHeight();
		if (CMapCamera.IsFarView(height))
		{
			return null;
		}
		CCounterKind? kind = CMapCamera.IsNear(height) ? _near ?? _far : _far ?? _near;
		return kind is null || 2f * kind.Type.Scale * _zoom < LeastCounterOnScreen ? null : kind;
	}

	/// <summary>Where a stack stands, in this node's coordinates, on the middle copy of the map.</summary>
	private Vector2 PlaceOf(CMapStack stack, CCounterKind kind)
	{
		CMapPosition position = _map.Positions[stack.Province];
		var place = new Vector2(position.X + 0.5f, _map.DisplayRow(position.Y) + 0.5f);
		// The stack of aircraft stands on the province's own point, as the standing stack
		// does, and is drawn after it - so over it. Only the stack on the move has a point
		// of its own on land.
		if (stack.Kind == EMapStack.Moving)
		{
			place += new Vector2(CCounterLayout.MovingStack.X, -CCounterLayout.MovingStack.Y);
		}
		return place;
	}

	/// <summary>
	/// The pointer beside the counter of a unit on the move: the original's own picture,
	/// its <c>offset</c> away from the counter's middle in the direction the unit is
	/// going, and turned to point that way. Every moving unit has one, selected or not.
	/// </summary>
	private void DrawPointer(Rid item, CCounterKind kind, CMapCounter counter, Rect2 rect)
	{
		CUnit unit = counter.Unit;
		if (_pointer is null || _pointerTexture is null || !unit.IsMoving || unit.Location <= 0)
		{
			return;
		}
		CMapPosition here = _map.Positions[unit.Location];
		CMapPosition next = _map.Positions[unit.Path[0]];
		float heading = CCounterLayout.Heading(here.X, here.Y, next.X, next.Y, _map.Width);
		System.Numerics.Vector2 at = CCounterLayout.PointerPlace(kind.Type, _pointer, counter.Size, heading);
		float half = CCounterLayout.PointerHalfSize(kind.Type, _pointer, counter.Size);

		// North is up on the map and down on the screen, so the angle turns the other way.
		Vector2 middle = rect.GetCenter() + new Vector2(at.X, -at.Y);
		float frame = _pointerTexture.GetWidth() / (float)_pointer.Frames;
		RenderingServer.CanvasItemAddSetTransform(item, new Transform2D(-heading, middle));
		_pointerTexture.DrawRectRegion(item, new Rect2(-half, -half, 2f * half, 2f * half),
			new Rect2(MarchingFrame * frame, 0f, frame, _pointerTexture.GetHeight()));
		RenderingServer.CanvasItemAddSetTransform(item, Transform2D.Identity);
	}

	/// <summary>
	/// The selected unit's way, drawn under the counters as the original draws it: the
	/// arrow's own picture laid along a curve from the unit's counter through the point
	/// of each province ahead of it. The part already walked is taken from the filled
	/// half of the picture and the rest from the outlined half. Only a selected unit has
	/// one. An attack and a retreat have pictures of their own. See
	/// <c>docs/mechanics/movement.md</c>, section 7.
	/// </summary>
	private void DrawPath(CCounterKind kind)
	{
		// Every selected unit on the move shows its way (selection.md, section 4).
		foreach (CUnit unit in _selection.Units)
		{
			if (unit.IsMoving && unit.Location > 0)
			{
				DrawPath(kind, unit);
			}
		}
	}

	private void DrawPath(CCounterKind kind, CUnit unit)
	{
		Texture2D? arrowTexture = _arrowTextures.GetValueOrDefault(CMapArrows.TypeOf(_state, unit))
			?? _arrowTextures.GetValueOrDefault(CArrowTypes.Move);
		if (arrowTexture is null)
		{
			return;
		}
		// From where its counter is, which for a unit on the move is beside the province's
		// point - or, for a fleet still in port, from the port.
		Vector2 from = CMapCounters.HasCounter(_state, unit)
			? PlaceOf(new CMapStack(unit.Location, CMapCounters.StackOf(unit)), kind)
			: WayPointOf(unit, unit.Location);
		var points = new List<System.Numerics.Vector2> { new(from.X, from.Y) };
		foreach (int province in unit.Path)
		{
			Vector2 next = WayPointOf(unit, province);
			// The short way round: the east edge of the map is beside the west.
			next.X += Mathf.Round((points[^1].X - next.X) / _map.Width) * _map.Width;
			points.Add(new System.Numerics.Vector2(next.X, next.Y));
		}

		List<CArrowSample> band = CArrowBand.Build(points);
		float walked = CMovement.WalkedShare(_state, unit) / (float)CFixedPoint.Scale;
		for (int copy = -1; copy <= 1; copy++)
		{
			var shift = new Vector2(copy * _map.Width, 0f);
			for (int index = 1; index < band.Count; index++)
			{
				CArrowSample a = band[index - 1];
				CArrowSample b = band[index];
				// The picture is the outline over the filled arrow: half its height each.
				float top = a.Progress < walked ? 0.5f : 0f;
				_quad[0] = new Vector2(a.Left.X, a.Left.Y) + shift;
				_quad[1] = new Vector2(b.Left.X, b.Left.Y) + shift;
				_quad[2] = new Vector2(b.Right.X, b.Right.Y) + shift;
				_quad[3] = new Vector2(a.Right.X, a.Right.Y) + shift;
				_quadPicture[0] = new Vector2(a.Along, top);
				_quadPicture[1] = new Vector2(b.Along, top);
				_quadPicture[2] = new Vector2(b.Along, top + 0.5f);
				_quadPicture[3] = new Vector2(a.Along, top + 0.5f);
				DrawPrimitive(_quad, QuadColours, _quadPicture, arrowTexture);
			}
		}
	}

	private Vector2 PointOf(int province)
	{
		CMapPosition position = _map.Positions[province];
		return new Vector2(position.X + 0.5f, _map.DisplayRow(position.Y) + 0.5f);
	}

	/// <summary>
	/// The point of a province a unit's way is drawn through: the province's own, but
	/// for a fleet in a land province the port.
	/// </summary>
	private Vector2 WayPointOf(CUnit unit, int province)
	{
		if (unit.Kind != EUnitKind.Navy || _map.IsSea(province))
		{
			return PointOf(province);
		}
		CMapPosition port = _map.NavalBasePositions[province];
		return new Vector2(port.X + 0.5f, _map.DisplayRow(port.Y) + 0.5f);
	}

	/// <summary>
	/// The counters of a stack that are drawn, the bottom one first: its top six, with
	/// the selected unit brought to the top if it is in the stack at all.
	/// </summary>
	private List<CMapCounter> Drawn(CMapStack stack)
	{
		_drawn.Clear();
		// Ours with several selected: the first of them in the stack, in the order they were selected.
		CMapCounter? chosen = null;
		foreach (CUnit selected in _selection.Units)
		{
			foreach (CMapCounter counter in stack.Counters)
			{
				if (ReferenceEquals(counter.Unit, selected))
				{
					chosen = counter;
				}
			}
			if (chosen is not null)
			{
				break;
			}
		}
		int room = CCounterLayout.Drawn(stack.Counters.Count) - (chosen is null ? 0 : 1);
		for (int index = stack.Counters.Count - 1; index >= 0 && _drawn.Count < room; index--)
		{
			if (!ReferenceEquals(stack.Counters[index], chosen))
			{
				_drawn.Insert(0, stack.Counters[index]);
			}
		}
		if (chosen is not null)
		{
			_drawn.Add(chosen);
		}
		return _drawn;
	}

	private static Rect2 RectOf(CCounterKind kind, CMapCounter counter, Vector2 place, int drawn)
	{
		System.Numerics.Vector2 fan = CCounterLayout.FanOffset(drawn);
		float half = CCounterLayout.HalfHeight(kind.Type, counter.Size);
		var middle = new Vector2(place.X + fan.X, place.Y - fan.Y);
		return new Rect2(middle.X - half * kind.Aspect, middle.Y - half, 2f * half * kind.Aspect, 2f * half);
	}

	public override void _Draw()
	{
		ClearItems();
		if (Kind() is not { } kind)
		{
			return;
		}
		float reach = kind.Type.Scale * 4f;                     // further than any counter of a stack is from its place
		Rect2 view = _view.Grow(reach);
		DrawPath(kind);
		foreach (CMapStack stack in _stacks)
		{
			Vector2 place = PlaceOf(stack, kind);
			for (int copy = -1; copy <= 1; copy++)
			{
				var here = new Vector2(place.X + copy * _map.Width, place.Y);
				if (view.HasPoint(here))
				{
					DrawStack(kind, stack, here);
				}
			}
		}
	}

	private void DrawStack(CCounterKind kind, CMapStack stack, Vector2 place)
	{
		List<CMapCounter> drawn = Drawn(stack);
		for (int index = 0; index < drawn.Count; index++)
		{
			CMapCounter counter = drawn[index];
			Rect2 rect = RectOf(kind, counter, place, index);
			// The counter through counter.fx, its corner's mark and its ring with it: white on a selected unit's counter
			// and the counter type's second colour on that of every unit under one, pulsing.
			ECounterRing worn = CMapCounters.RingOf(counter.Unit, _selectedSet);
			ECounterMark mark = CCounterShading.MarkOf(counter.Unit, Player);
			Rid plate = NextItem();
			RenderingServer.CanvasItemSetMaterial(plate, kind.Material(_gui, counter, mark, worn).GetRid());
			kind.Plate.DrawRect(plate, rect, false);

			// What goes over it, under the next counter.
			Rid item = NextItem();
			if (stack.Kind == EMapStack.Moving)
			{
				DrawPointer(item, kind, counter, rect);
			}
			if (index != drawn.Count - 1)
			{
				continue;
			}

			// Only the one on top carries a flag and figures.
			Vector2 middle = rect.GetCenter();
			if (kind.Type.HasFlag && Flag(counter.Unit.Owner) is { } flag)
			{
				System.Numerics.Vector2 at = CCounterLayout.FlagPlace(kind.Type, counter.Size);
				System.Numerics.Vector2 size = CCounterLayout.FlagSize(kind.Type);
				var where = new Rect2(middle.X + at.X - size.X / 2f, middle.Y - at.Y - size.Y / 2f, size.X, size.Y);
				RenderingServer.CanvasItemAddRect(item, where.Grow(CCounterLayout.FlagFrame(kind.Type)), Colors.Black);
				flag.DrawRect(item, where, false);
			}
			float figures = CCounterLayout.FiguresHeight(kind.Type, counter.Size);
			System.Numerics.Vector2 start = CCounterLayout.FiguresPlace(kind.Type, counter.Size);
			DrawText(item, kind, counter.Figures, new Vector2(middle.X + start.X, middle.Y - start.Y), figures, 0f);
			if (kind.CarriesName)
			{
				// Turned a quarter: it runs down the counter's right edge.
				float name = CCounterLayout.NameHeight(kind.Type, counter.Size);
				start = CCounterLayout.NamePlace(kind.Type, counter.Size);
				DrawText(item, kind, counter.Name, new Vector2(middle.X + start.X, middle.Y - start.Y), name, Mathf.Pi / 2f);
			}
		}
	}

	/// <summary>
	/// Writes a text in the counter's font with the start of the line its letters stand
	/// on at a point, a line of it so high, turned by an angle. That is where the
	/// original's figures and name are on a screenshot of one: the figures' left end and
	/// the foot of their digits on the type's place for them.
	/// </summary>
	private void DrawText(Rid item, CCounterKind kind, string text, Vector2 start, float lineHeight, float turned)
	{
		if (text.Length == 0 || kind.Font?.Font is not { } typeface || kind.Font.LineHeight <= 0f
			|| lineHeight * _zoom < LeastTextOnScreen)
		{
			return;
		}
		float scale = lineHeight / kind.Font.LineHeight;
		RenderingServer.CanvasItemAddSetTransform(item, new Transform2D(turned, new Vector2(scale * kind.Font.Narrowing, scale), 0f, start));
		typeface.DrawString(item, Vector2.Zero, text, HorizontalAlignment.Left, -1f, kind.Font.Size, kind.Font.Color);
		RenderingServer.CanvasItemAddSetTransform(item, Transform2D.Identity);
	}

	private Texture2D? Flag(CCountryTag tag)
	{
		string key = tag.ToString();
		if (!_flags.TryGetValue(key, out Texture2D? flag))
		{
			string file = _state.Country(tag)?.FlagFile ?? "";
			flag = file.Length != 0 && _gui.Textures.Exists(file) ? _gui.Textures.LoadFromGamePath(file) : null;
			_flags[key] = flag;
		}
		return flag;
	}

	/// <summary>
	/// The unit a click at a point of the map selects, or null. Only the counter on top
	/// of a stack takes a click - on the edge of one under it the click is the
	/// province's - and it selects the unit on top, then with each further click the
	/// next unit of the stack.
	/// </summary>
	public CUnit? Click(Vector2 point)
	{
		// The stack is stepped through only from the one selected unit's own counter (selection.md, section 2.1).
		return StackAt(point)?.Next(_selection.Single);
	}

	/// <summary>The stack whose top counter is at a point of the map, or null.</summary>
	private CMapStack? StackAt(Vector2 point)
	{
		if (Kind() is not { } kind)
		{
			return null;
		}
		float reach = kind.Type.Scale * 4f;
		float x = Mathf.PosMod(point.X, _map.Width);
		CMapStack? found = null;
		foreach (CMapStack stack in _stacks)
		{
			Vector2 place = PlaceOf(stack, kind);
			float across = Mathf.Abs(place.X - x);
			if (Mathf.Min(across, _map.Width - across) > reach || Mathf.Abs(place.Y - point.Y) > reach)
			{
				continue;
			}
			// The point, moved onto the copy of the map this stack's place is given on.
			var local = new Vector2(x + Mathf.Round((place.X - x) / _map.Width) * _map.Width, point.Y);
			List<CMapCounter> drawn = Drawn(stack);
			int top = drawn.Count - 1;
			if (top >= 0 && RectOf(kind, drawn[top], place, top).HasPoint(local))
			{
				found = stack;                               // of two that overlap, the one drawn later
			}
		}
		return found;
	}
}
