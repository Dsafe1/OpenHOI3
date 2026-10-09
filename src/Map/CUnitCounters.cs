using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
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

	/// <summary>How far east and north of the standing stack the one for aircraft is put, in counter widths. Ours.</summary>
	private const float AirStackShift = 1.1f;

	/// <summary>How fast a selected counter's ring pulses.</summary>
	private const float PulseSpeed = 3f;

	private CMap _map = null!;
	private CGameState _state = null!;
	private CGuiContext _gui = null!;
	private CCounterKind? _far;
	private CCounterKind? _near;

	private List<CMapStack> _stacks = new();
	private readonly Dictionary<string, Texture2D?> _flags = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<CMapCounter> _drawn = new();
	private float _zoom = 1f;
	private Rect2 _view;
	private CUnit? _selected;
	private double _time;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CMap map, CGameState state, CGuiContext gui, CCounterTypes types)
	{
		_map = map;
		_state = state;
		_gui = gui;
		_far = CCounterKind.Create(gui, types.Find(CCounterTypes.Far), CCounterText.FarSprite, carriesName: false);
		_near = CCounterKind.Create(gui, types.Find(CCounterTypes.Near), CCounterText.NearSprite, carriesName: true);
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
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
			QueueRedraw();
		}
	}

	/// <summary>Works out again which units are where. Call it when one has been placed, made or moved.</summary>
	public void Refresh()
	{
		_stacks = CMapCounters.Build(_state);
		if (_selected is not null && !CMapCounters.HasCounter(_state, _selected))
		{
			_selected = null;
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
		if (stack.Kind == EMapStack.Air)
		{
			float width = 2f * kind.Type.Scale * kind.Aspect * AirStackShift;
			place += new Vector2(width, -width);
		}
		return place;
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
			if (ReferenceEquals(counter.Unit, _selected) && kind.Ring is { } ring)
			{
				float strength = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin((float)_time * PulseSpeed));
				DrawTextureRect(ring, rect, false, new Color(strength, strength, strength));
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
