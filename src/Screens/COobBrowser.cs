using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The order of battle browser, <c>oobbrowser</c>, opened from the single unit panel and up for as long as one unit is
/// selected: the player's whole tree in one or two lists, with what is dragged onto a row reorganising it. See
/// <c>docs/mechanics/oobbrowser.md</c>.
/// </summary>
public partial class COobBrowser : Control
{
	/// <summary>One list and what it shows.</summary>
	private sealed class CView
	{
		public required CGuiWindow Window { get; init; }
		public required CGuiListBox List { get; init; }
		public COobTree Tree { get; } = new();
		public List<COobEntry> Entries { get; } = new();
		public int ShownVersion { get; set; } = -1;
		public CGuiButton? Collapse { get; init; }
		public CGuiButton? Uncollapse { get; init; }
	}

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;

	private CGuiWindow? _window;
	private CGuiWindow? _dragUnit;
	private CGuiWindow? _dragLeader;
	private CGuiButton? _splitButton;
	private CView? _main;
	private CView? _split;
	private bool _open;
	private bool _follow = true;
	private CUnit? _selected;
	private readonly Dictionary<CUnit, CUnit?> _parents = new();

	private CUnit? _draggedUnit;
	private CSubUnit? _draggedSubUnit;
	private CLeader? _draggedLeader;
	private int _dragState;
	private Vector2 _dragFrom;
	private COobEntry? _hovered;
	private double _hoverTime;
	private double _scrollCounter;
	private Vector2 _mouse;

	/// <summary>The units selected on the map.</summary>
	public Func<IReadOnlyList<CUnit>> Selection { get; set; } = Array.Empty<CUnit>;

	/// <summary>A row asked for its unit to be the one selected.</summary>
	public event Action<CUnit>? UnitChosen;

	/// <summary>A row was clicked, and any country screen goes.</summary>
	public event Action? CountryViewsClosing;

	/// <summary>The browser's width has changed; the unit panel stands beside it.</summary>
	public event Action<float>? WidthChanged;

	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public bool IsOpen => _open;

	private bool SplitShown => _split is { Window.Visible: true };

	/// <summary>How much of the screen's left the browser takes.</summary>
	public float TotalWidth => !_open ? 0f : COobBrowserText.WindowWidth * (SplitShown ? 2 : 1);

	public override void _Ready()
	{
		Name = COobBrowserText.WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, COobBrowserText.WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_window.Visible = false;
		CGuiWindow? split = _window.FindWindow(COobBrowserText.SplitName);
		_main = MakeView(_window);
		_split = split is null ? null : MakeView(split);
		if (split is not null)
		{
			split.Visible = false;
		}
		Bind();
		_dragUnit = MakeFloating(COobBrowserText.UnitRowName);
		_dragLeader = MakeFloating(COobBrowserText.LeaderRowName);
	}

	/// <summary>A window's own list and buttons, not those of the window inside it.</summary>
	private static CView? MakeView(CGuiWindow window)
	{
		if (Direct<CGuiListBox>(window, "list") is not { } list)
		{
			return null;
		}
		return new CView
		{
			Window = window,
			List = list,
			Collapse = Direct<CGuiButton>(window, "collapse_button"),
			Uncollapse = Direct<CGuiButton>(window, "uncollapse_button"),
		};
	}

	private static T? Direct<T>(Node window, string name) where T : Node
	{
		return window.GetChildren().OfType<T>().FirstOrDefault(child => child.Name == name);
	}

	private CGuiWindow? MakeFloating(string name)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(_context, name);
		if (window is null)
		{
			return null;
		}
		AddChild(window);
		window.Visible = false;
		PassThrough(window);
		Inner(window).TryFindButton("expand_button")?.Hide();
		return window;
	}

	/// <summary>The window a row's elements are in.</summary>
	private static CGuiWindow Inner(CGuiWindow row) => Direct<CGuiWindow>(row, "oob_actual_entry") ?? row;

	private static void PassThrough(Control control)
	{
		control.MouseFilter = MouseFilterEnum.Ignore;
		foreach (Control child in control.GetChildren().OfType<Control>())
		{
			PassThrough(child);
		}
	}

	private void Bind()
	{
		if (_main is not { } main)
		{
			return;
		}
		BindView(main);
		if (_split is { } split)
		{
			BindView(split);
			Direct<CGuiButton>(split.Window, "match_views_button")!.Pressed += MatchViews;
		}
		if (Direct<CGuiButton>(main.Window, "theatre_forces_button") is { } forces)
		{
			forces.Pressed += () => _simulation.Post(new CTheatreForcesCommand(_player.Tag));
			forces.Tooltip = () => new Interface.Tooltips.CTooltip { Text = _context.Localisation.Localise("THEATRE_ASSIGN_TROOPS_DESC") };
		}
		_splitButton = Direct<CGuiButton>(main.Window, "split_button");
		if (_splitButton is not null)
		{
			_splitButton.Pressed += ToggleSplit;
		}
	}

	private void BindView(CView view)
	{
		if (view.Collapse is { } collapse)
		{
			collapse.Pressed += () => view.Tree.FillRoots(_player);
		}
		if (view.Uncollapse is { } uncollapse)
		{
			uncollapse.Pressed += () => view.Tree.ExpandAll(_player);
		}
		if (Direct<CGuiButton>(view.Window, "focus_button") is { } focus)
		{
			focus.Pressed += () => ScrollToSelected(view);
		}
	}

	/// <summary>The unit panel's button: it opens, or if up shuts, for the one unit selected.</summary>
	public void Toggle()
	{
		if (_window is null || _main is null)
		{
			return;
		}
		if (_open)
		{
			Close();
			return;
		}
		_open = true;
		_window.Visible = true;
		_window.MoveToFront();
		_split?.Window.Hide();
		if (_window.FindTextBox("oob_label") is { } label)
		{
			label.Text = COobBrowserText.Title(_context.Localisation.Localise, _player);
		}
		Resize();
		if (_main.Tree.Rows.Count == 0)
		{
			_main.Tree.FillRoots(_player);
		}
		// Only the first time: what changed while it was shut is taken up by the refresh, as the original's queues are.
		if (_parents.Count == 0)
		{
			RememberParents();
		}
		_selected = Selection().FirstOrDefault();
		Refresh();
		ScrollToSelected(_main);
		ShowSplitFrame();
		WidthChanged?.Invoke(TotalWidth);
	}

	/// <summary>The window as high as the screen allows, and each list keeping its difference from it.</summary>
	private void Resize()
	{
		float height = GetViewportRect().Size.Y - COobBrowserText.ScreenMargin;
		foreach (CView? view in new[] { _main, _split })
		{
			if (view is null)
			{
				continue;
			}
			view.Window.Size = new Vector2(view.Window.Size.X, height);
			view.List.SetHeight(height - COobBrowserText.ListMargin);
			if (Direct<CGuiButton>(view.Window, "Background") is { } background)
			{
				background.Size = new Vector2(background.Size.X, height);
			}
		}
	}

	public void Close()
	{
		if (!_open)
		{
			return;
		}
		_open = false;
		if (_window is not null)
		{
			_window.Visible = false;
		}
		ClearDrag();
		WidthChanged?.Invoke(0f);
	}

	private void ToggleSplit()
	{
		if (_split is not { } split || _main is not { } main)
		{
			return;
		}
		split.Window.Visible = !split.Window.Visible;
		if (split.Window.Visible)
		{
			MatchViews();
			split.List.ScrollTo(main.List.First);
		}
		ShowSplitFrame();
		WidthChanged?.Invoke(TotalWidth);
	}

	private void ShowSplitFrame()
	{
		_splitButton?.SetFrame(SplitShown ? 2 : 1);
	}

	private void MatchViews()
	{
		if (_split is { } split && _main is { } main)
		{
			split.Tree.CopyFrom(main.Tree);
			Rebuild(split);
			split.List.ScrollTo(main.List.First);
		}
	}

	private void ScrollToSelected(CView view)
	{
		if (!_follow || Selection().FirstOrDefault() is not { } unit)
		{
			return;
		}
		int index = view.Tree.EnsureRow(unit);
		if (index < 0)
		{
			return;
		}
		Rebuild(view);
		view.List.ScrollTo(view.Tree.ScrollFor(index, view.List.Rows));
	}

	private void RememberParents()
	{
		_parents.Clear();
		foreach (CUnit unit in _player.AllUnits())
		{
			_parents[unit] = unit.Parent;
		}
	}

	/// <summary>
	/// Any row whose unit is gone empties that list back to its roots, which loses what was open; a unit that has a new
	/// place in the tree, or is new, takes it.
	/// </summary>
	private void Refresh()
	{
		var moved = new List<CUnit>();
		foreach (CUnit unit in _player.AllUnits())
		{
			if (!_parents.TryGetValue(unit, out CUnit? parent) || !ReferenceEquals(parent, unit.Parent))
			{
				moved.Add(unit);
				_parents[unit] = unit.Parent;
			}
		}
		foreach (CView? view in new[] { _main, _split })
		{
			if (view is null)
			{
				continue;
			}
			if (view.Tree.Rows.Any(row => row.Unit.IsRemoved))
			{
				view.Tree.FillRoots(_player);
			}
			foreach (CUnit unit in moved)
			{
				view.Tree.Reposition(unit);
			}
			if (view.ShownVersion != view.Tree.Version && (view.Window.Visible || view == _main))
			{
				Rebuild(view);
			}
		}
	}

	private void Rebuild(CView view)
	{
		view.ShownVersion = view.Tree.Version;
		int first = view.List.First;
		view.List.Clear();
		view.Entries.Clear();
		for (int index = 0; index < view.Tree.Rows.Count; index++)
		{
			COobRow row = view.Tree.Rows[index];
			if (COobEntry.Create(_context, _simulation.State, row.Unit, row.Level) is not { } entry)
			{
				continue;
			}
			entry.Chosen += () => RowChosen(row.Unit);
			entry.ExpandPressed += () => view.Tree.Toggle(view.Tree.IndexOf(row.Unit));
			entry.DragStarted += () => BeginDrag(row.Unit, null, null);
			entry.LeaderDragStarted += () =>
			{
				if (row.Unit.Leader is { } leader)
				{
					BeginDrag(null, null, leader);
				}
			};
			view.Entries.Add(entry);
			view.List.Add(entry.Window);
		}
		view.List.ScrollTo(first);
	}

	private void RowChosen(CUnit unit)
	{
		_follow = false;
		CountryViewsClosing?.Invoke();
		UnitChosen?.Invoke(unit);
		_selected = unit;
		_follow = true;
	}

	/// <summary>The unit panel's row of a brigade, ship or wing being pressed, while the browser is up.</summary>
	public void BeginDragSubUnit(CSubUnit subUnit)
	{
		if (_open)
		{
			BeginDrag(null, subUnit, null);
		}
	}

	private void BeginDrag(CUnit? unit, CSubUnit? subUnit, CLeader? leader)
	{
		_draggedUnit = unit;
		_draggedSubUnit = subUnit;
		_draggedLeader = leader;
		_dragState = 1;
	}

	private void ClearDrag()
	{
		_draggedUnit = null;
		_draggedSubUnit = null;
		_draggedLeader = null;
		_dragState = 0;
		_hovered = null;
		_hoverTime = 0;
		_scrollCounter = 0;
		if (_dragUnit is not null)
		{
			_dragUnit.Visible = false;
		}
		if (_dragLeader is not null)
		{
			_dragLeader.Visible = false;
		}
	}

	private bool Dragging => _dragState == 3;

	private bool Allows(CUnit row)
	{
		if (_draggedUnit is { } unit)
		{
			return COobBrowserText.MayTakeUnit(row, unit);
		}
		if (_draggedSubUnit is { } subUnit)
		{
			return COobBrowserText.MayTakeSubUnit(_simulation.State, _player, row, subUnit);
		}
		return _draggedLeader is { } leader && COobBrowserText.MayTakeLeader(row, leader);
	}

	public override void _Input(InputEvent input)
	{
		// The event's own point and not the system pointer's, so that the developer switches drag too.
		if (input is InputEventMouse moved)
		{
			_mouse = moved.Position;
		}
		if (_dragState == 0 || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } button)
		{
			return;
		}
		if (Dragging)
		{
			Drop(button.Position);
		}
		ClearDrag();
	}

	/// <summary>The drop: the first list that has a row under the point, the main one before the split one.</summary>
	private void Drop(Vector2 point)
	{
		foreach (CView? view in new[] { _main, _split })
		{
			if (view is null || !view.Window.IsVisibleInTree() || view.Entries.FirstOrDefault(entry => entry.Holds(point)) is not { } entry)
			{
				continue;
			}
			if (Allows(entry.Unit))
			{
				Post(entry.Unit);
			}
			return;
		}
	}

	private void Post(CUnit row)
	{
		if (_draggedUnit is { } unit)
		{
			_simulation.Post(new CAttachUnitCommand(_player.Tag, unit.Id, row.Id));
		}
		else if (_draggedSubUnit is { } subUnit)
		{
			_simulation.Post(new CTransferSubUnitCommand(_player.Tag, subUnit.Id, row.Id));
		}
		else if (_draggedLeader is { } leader)
		{
			foreach (CCommand command in COobBrowserText.LeaderDrop(_player.Tag, row, leader))
			{
				_simulation.Post(command);
			}
		}
	}

	public override void _Process(double delta)
	{
		if (!_open || _main is null)
		{
			return;
		}
		IReadOnlyList<CUnit> selection = Selection();
		if (selection.Count != 1)
		{
			Close();
			return;
		}
		if (!ReferenceEquals(selection[0], _selected))
		{
			_selected = selection[0];
			ScrollToSelected(_main);
		}
		Refresh();
		Vector2 mouse = _mouse;
		UpdateDrag(delta, mouse);
		foreach (CView? view in new[] { _main, _split })
		{
			if (view is not null && view.Window.IsVisibleInTree())
			{
				UpdateView(view, mouse);
			}
		}
	}

	private void UpdateView(CView view, Vector2 mouse)
	{
		bool indented = view.Tree.AnyIndented;
		if (view.Collapse is not null)
		{
			view.Collapse.Visible = indented;
		}
		if (view.Uncollapse is not null)
		{
			view.Uncollapse.Visible = !indented;
		}
		int first = view.List.First;
		int last = Math.Min(first + view.List.Rows, view.Entries.Count);
		for (int index = first; index < last; index++)
		{
			COobEntry entry = view.Entries[index];
			entry.Update(ReferenceEquals(entry.Unit, _selected), view.Tree.IsOpen(index), Dragging, !Dragging || Allows(entry.Unit), entry.Holds(mouse));
		}
	}

	private void UpdateDrag(double delta, Vector2 mouse)
	{
		switch (_dragState)
		{
			case 1:
				_dragFrom = mouse;
				_dragState = 2;
				return;
			case 2:
				if (Mathf.Abs(mouse.X - _dragFrom.X) > COobBrowserText.DragThreshold || Mathf.Abs(mouse.Y - _dragFrom.Y) > COobBrowserText.DragThreshold)
				{
					_dragState = 3;
					ShowFloating();
				}
				return;
			case 3:
				break;
			default:
				return;
		}
		MoveFloating(mouse);
		OpenHovered(delta, mouse);
		AutoScroll(delta, mouse);
	}

	private void ShowFloating()
	{
		if (_draggedLeader is { } leader && _dragLeader is { } floatingLeader)
		{
			floatingLeader.Visible = true;
			floatingLeader.MoveToFront();
			if (floatingLeader.FindTextBox("leader_name") is { } name)
			{
				name.Text = Interface.Tooltips.CLeaderTooltip.RankName(_context.Localisation.Localise, leader) + " " + leader.Name;
			}
			floatingLeader.FindIcon("leader_rank")?.SetFrame(leader.Rank);
			if (floatingLeader.FindTextBox("leader_info") is { } info)
			{
				info.Text = COobBrowserText.LeaderInfo(_context.Localisation.Localise, leader);
			}
			CLeaderEntry.ShowPortrait(_context, floatingLeader.FindIcon("leader_photo"), leader);
			return;
		}
		if (_dragUnit is not { } floating)
		{
			return;
		}
		floating.Visible = true;
		floating.MoveToFront();
		CGuiWindow inner = Inner(floating);
		string name2 = _draggedUnit?.Name ?? _draggedSubUnit?.Name ?? "";
		if (inner.FindTextBox("unit_name") is { } unitName)
		{
			unitName.Text = name2;
		}
		if (inner.FindTextBox("unit_info") is { } unitInfo)
		{
			unitInfo.Text = _draggedUnit is { } unit ? COobBrowserText.UnitInfo(unit) : "";
		}
		if (_draggedUnit is { SubUnits.Count: > 0 } counter)
		{
			inner.FindIcon("counter_type")?.SetSprite(CUnitPanelText.BrigadeSprite(counter.SubUnits[0]));
			inner.FindIcon("counter_size")?.SetFrame(COobBrowserText.SizeFrame(counter));
		}
		else if (_draggedSubUnit is { } subUnit)
		{
			inner.FindIcon("counter_type")?.SetSprite(CUnitPanelText.BrigadeSprite(subUnit));
			inner.FindIcon("counter_size")?.SetFrame(1);
		}
	}

	private void MoveFloating(Vector2 mouse)
	{
		if (_dragLeader is { Visible: true } leader)
		{
			leader.GlobalPosition = mouse + new Vector2(10f, 0f);
		}
		if (_dragUnit is { Visible: true } unit)
		{
			unit.GlobalPosition = mouse;
		}
	}

	/// <summary>A closed row the mouse stays on for a second opens.</summary>
	private void OpenHovered(double delta, Vector2 mouse)
	{
		COobEntry? over = null;
		CView? holder = null;
		foreach (CView? view in new[] { _main, _split })
		{
			if (view is null || !view.Window.IsVisibleInTree())
			{
				continue;
			}
			over = view.Entries.FirstOrDefault(entry => entry.Holds(mouse));
			if (over is not null)
			{
				holder = view;
				break;
			}
		}
		if (!ReferenceEquals(over, _hovered))
		{
			_hovered = over;
			_hoverTime = 0;
			return;
		}
		if (over is null || holder is null)
		{
			return;
		}
		_hoverTime += delta;
		int index = holder.Tree.IndexOf(over.Unit);
		if (_hoverTime >= COobBrowserText.HoverOpens && index >= 0 && !holder.Tree.IsOpen(index) && over.Unit.Children.Count > 0)
		{
			holder.Tree.Open(index);
			_hovered = null;
			_hoverTime = 0;
		}
	}

	/// <summary>Beyond a list's top or bottom edge, inside its column, the list creeps by the root of the distance.</summary>
	private void AutoScroll(double delta, Vector2 mouse)
	{
		foreach (CView? view in new[] { _main, _split })
		{
			if (view is null || !view.Window.IsVisibleInTree())
			{
				continue;
			}
			Rect2 rect = view.List.GetGlobalRect();
			if (mouse.X < rect.Position.X || mouse.X > rect.End.X)
			{
				continue;
			}
			float distance = mouse.Y < rect.Position.Y ? mouse.Y - rect.Position.Y : mouse.Y > rect.End.Y ? mouse.Y - rect.End.Y : 0f;
			if (distance == 0f)
			{
				_scrollCounter = 0;
				return;
			}
			_scrollCounter += Mathf.Sign(distance) * Mathf.Sqrt(Mathf.Abs(distance)) * delta;
			if (Math.Abs(_scrollCounter) > 1.0)
			{
				view.List.ScrollTo(view.List.First + Math.Sign(_scrollCounter));
				_scrollCounter = 0;
			}
			return;
		}
		_scrollCounter = 0;
	}
}
