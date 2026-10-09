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
	/// <summary>
	/// The zoom from which the near counter is drawn and under which the far one is.
	/// Ours: the original goes by the camera's distance, and what it compares was not read.
	/// </summary>
	private const float NearZoom = 8f;

	/// <summary>Under this many pixels of the screen a counter is not drawn, nor a text written.</summary>
	private const float LeastCounterOnScreen = 6f;
	private const float LeastTextOnScreen = 5f;

	/// <summary>
	/// How far east and north of the standing stack the one for aircraft is put, in
	/// counter widths. Ours: the original keeps a point for each stack of a province, and
	/// what sets them was not found. The stack of units on the move is placed by
	/// <see cref="CCounterLayout.MovingStack"/>, which is measured.
	/// </summary>
	private const float AirStackShift = 1.1f;

	/// <summary>The frame of the pointer's picture for a unit that is marching: the green one.</summary>
	private const int MarchingFrame = 0;

	private static readonly Color[] QuadColours = { Colors.White, Colors.White, Colors.White, Colors.White };

	/// <summary>How fast a selected counter's ring pulses.</summary>
	private const float PulseSpeed = 3f;

	private CMap _map = null!;
	private CGameState _state = null!;
	private CGuiContext _gui = null!;
	private CCounterKind? _far;
	private CCounterKind? _near;
	private CBillboardType? _pointer;
	private Texture2D? _pointerTexture;
	private Texture2D? _arrowTexture;
	private CHierarchyLayer _hierarchy = null!;
	private readonly Vector2[] _quad = new Vector2[4];
	private readonly Vector2[] _quadPicture = new Vector2[4];

	private List<CMapStack> _stacks = new();
	private readonly Dictionary<string, Texture2D?> _flags = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<CMapCounter> _drawn = new();
	private float _zoom = 1f;
	private Rect2 _view;
	private CUnit? _selected;
	private double _time;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CMap map, CGameState state, CGuiContext gui, CCounterTypes types, CBillboardTypes billboards,
		CArrowTypes arrows)
	{
		if (arrows.Find(CArrowTypes.Move) is { TextureFile.Length: > 0 } arrow && gui.Textures.Exists(arrow.TextureFile))
		{
			_arrowTexture = gui.Textures.LoadFromGamePath(arrow.TextureFile);
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

		_hierarchy = new CHierarchyLayer { Name = "Hierarchy" };
		_hierarchy.Setup(map, gui);
		AddChild(_hierarchy);
	}

	public override void _Ready()
	{
		Refresh();
	}

	/// <summary>The unit whose counter wears the ring, or null.</summary>
	public CUnit? Selected
	{
		get => _selected;
		set
		{
			_selected = value;
			_hierarchy.Unit = value;
			QueueRedraw();
		}
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
		if (_selected is not null && !CMapCounters.HasCounter(_state, _selected))
		{
			_selected = null;
			_hierarchy.Unit = null;
		}
		QueueRedraw();
	}

	/// <summary>Told by the map view whenever what is on screen changes.</summary>
	public void ViewChanged(float zoom, Rect2 view)
	{
		_zoom = zoom;
		_view = view;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_selected is not null)
		{
			_time += delta;
			QueueRedraw();                                   // its ring pulses
		}
	}

	/// <summary>The counter type for the present zoom, or null when counters are too small to draw.</summary>
	private CCounterKind? Kind()
	{
		CCounterKind? kind = _zoom >= NearZoom ? _near ?? _far : _far ?? _near;
		return kind is null || 2f * kind.Type.Scale * _zoom < LeastCounterOnScreen ? null : kind;
	}

	/// <summary>Where a stack stands, in this node's coordinates, on the middle copy of the map.</summary>
	private Vector2 PlaceOf(CMapStack stack, CCounterKind kind)
	{
		CMapPosition position = _map.Positions[stack.Province];
		var place = new Vector2(position.X + 0.5f, _map.DisplayRow(position.Y) + 0.5f);
		float width = 2f * kind.Type.Scale * kind.Aspect;
		if (stack.Kind == EMapStack.Air)
		{
			place += new Vector2(width, -width) * AirStackShift;
		}
		else if (stack.Kind == EMapStack.Moving)
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
	private void DrawPointer(CCounterKind kind, CMapCounter counter, Rect2 rect)
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
		DrawSetTransform(middle, -heading, Vector2.One);
		DrawTextureRectRegion(_pointerTexture, new Rect2(-half, -half, 2f * half, 2f * half),
			new Rect2(MarchingFrame * frame, 0f, frame, _pointerTexture.GetHeight()));
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	/// <summary>
	/// The selected unit's way, drawn under the counters as the original draws it: the
	/// arrow's own picture laid along a curve from the unit's counter through the point
	/// of each province ahead of it. The part already walked is taken from the filled
	/// half of the picture and the rest from the outlined half. Only a selected unit has
	/// one. See <c>docs/mechanics/movement.md</c>, section 7.
	/// </summary>
	private void DrawPath(CCounterKind kind)
	{
		if (_arrowTexture is null || _selected is not { IsMoving: true } unit || unit.Location <= 0)
		{
			return;
		}
		// From where its counter is, which for a unit on the move is beside the province's point.
		Vector2 from = PlaceOf(new CMapStack(unit.Location, CMapCounters.StackOf(unit)), kind);
		var points = new List<System.Numerics.Vector2> { new(from.X, from.Y) };
		foreach (int province in unit.Path)
		{
			Vector2 next = PointOf(province);
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
				DrawPrimitive(_quad, QuadColours, _quadPicture, _arrowTexture);
			}
		}
	}

	private Vector2 PointOf(int province)
	{
		CMapPosition position = _map.Positions[province];
		return new Vector2(position.X + 0.5f, _map.DisplayRow(position.Y) + 0.5f);
	}

	/// <summary>
	/// The counters of a stack that are drawn, the bottom one first: its top six, with
	/// the selected unit brought to the top if it is in the stack at all.
	/// </summary>
	private List<CMapCounter> Drawn(CMapStack stack)
	{
		_drawn.Clear();
		CMapCounter? chosen = null;
		if (_selected is not null)
		{
			foreach (CMapCounter counter in stack.Counters)
			{
				if (ReferenceEquals(counter.Unit, _selected))
				{
					chosen = counter;
				}
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
			if (kind.Texture(_gui, counter) is { } texture)
			{
				DrawTextureRect(texture, rect, false);
			}
			// The ring: white on the selected unit's counter, and the counter type's second
			// colour on that of every unit under it, each pulsing.
			ECounterRing worn = CMapCounters.RingOf(counter.Unit, _selected);
			if (worn != ECounterRing.None && kind.Ring is { } ring)
			{
				float strength = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin((float)_time * PulseSpeed));
				Color colour = worn == ECounterRing.Under ? kind.Type.Color2.ToGodot() : Colors.White;
				DrawTextureRect(ring, rect, false, new Color(colour.R * strength, colour.G * strength, colour.B * strength));
			}
			if (stack.Kind == EMapStack.Moving)
			{
				DrawPointer(kind, counter, rect);
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
				DrawRect(where.Grow(CCounterLayout.FlagFrame(kind.Type)), Colors.Black);
				DrawTextureRect(flag, where, false);
			}
			float figures = CCounterLayout.FiguresHeight(kind.Type, counter.Size);
			System.Numerics.Vector2 start = CCounterLayout.FiguresPlace(kind.Type, counter.Size);
			DrawText(kind, counter.Figures, new Vector2(middle.X + start.X, middle.Y - start.Y), figures, 0f);
			if (kind.CarriesName)
			{
				// Turned a quarter: it runs down the counter's right edge.
				float name = CCounterLayout.NameHeight(kind.Type, counter.Size);
				start = CCounterLayout.NamePlace(kind.Type, counter.Size);
				DrawText(kind, counter.Name, new Vector2(middle.X + start.X, middle.Y - start.Y), name, Mathf.Pi / 2f);
			}
		}
	}

	/// <summary>
	/// Writes a text in the counter's font with the start of the line its letters stand
	/// on at a point, a line of it so high, turned by an angle. That is where the
	/// original's figures and name are on a screenshot of one: the figures' left end and
	/// the foot of their digits on the type's place for them.
	/// </summary>
	private void DrawText(CCounterKind kind, string text, Vector2 start, float lineHeight, float turned)
	{
		if (text.Length == 0 || kind.Font?.Font is not { } typeface || kind.Font.LineHeight <= 0f
			|| lineHeight * _zoom < LeastTextOnScreen)
		{
			return;
		}
		float scale = lineHeight / kind.Font.LineHeight;
		DrawSetTransform(start, turned, new Vector2(scale * kind.Font.Narrowing, scale));
		DrawString(typeface, Vector2.Zero, text, HorizontalAlignment.Left, -1f, kind.Font.Size, kind.Font.Color);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
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
		return StackAt(point)?.Next(_selected);
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
