using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the order of battle browser: an <c>oob_unit_entry</c> window, its <c>oob_actual_entry</c> indented by the
/// row's level. See <c>docs/mechanics/oobbrowser.md</c>, section 5.
/// </summary>
public sealed class COobEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _info;
	private readonly CGuiIcon? _photo;
	private readonly CGuiIcon? _counter;
	private readonly CGuiIcon? _size;
	private readonly CGuiButton? _drag;
	private readonly CGuiButton? _expand;
	private CLeaderShown _leaderShown;
	private bool _pressedPhoto;

	/// <summary>Raised when the row is clicked.</summary>
	public event Action? Chosen;

	/// <summary>Raised when the expand button is pressed.</summary>
	public event Action? ExpandPressed;

	/// <summary>Raised when the left button goes down on the row.</summary>
	public event Action? DragStarted;

	/// <summary>Raised when the left button goes down on the leader's picture.</summary>
	public event Action? LeaderDragStarted;

	private readonly record struct CLeaderShown(object? Leader);

	private COobEntry(CGuiContext context, CGameState state, CGuiWindow window, CUnit unit, int level)
	{
		_context = context;
		_state = state;
		Window = window;
		Unit = unit;
		CGuiWindow actual = window.FindWindow("oob_actual_entry") ?? window;
		actual.Position = new Vector2(level * COobBrowserText.Indent, actual.Position.Y);
		_name = actual.FindTextBox("unit_name");
		_info = actual.FindTextBox("unit_info");
		_photo = actual.FindIcon("leader_photo");
		_counter = actual.FindIcon("counter_type");
		_size = actual.FindIcon("counter_size");
		_drag = actual.FindButton("drag_button");
		_expand = actual.FindButton("expand_button");
		if (_drag is not null)
		{
			_drag.Pressed += () => Chosen?.Invoke();
			_drag.HeldDown += ButtonHeld;
		}
		if (_expand is not null)
		{
			_expand.Pressed += () => ExpandPressed?.Invoke();
		}
		if (_photo is not null)
		{
			_photo.MouseFilter = Control.MouseFilterEnum.Stop;
			_photo.GuiInput += PhotoInput;
		}
		actual.SetTooltips(name => COobBrowserText.Tooltip(context.Localisation.Localise, state, unit, name), "unit_name", "unit_info", "counter_type", "counter_size");
		Fill();
	}

	public CGuiWindow Window { get; }

	public CUnit Unit { get; }

	public static COobEntry? Create(CGuiContext context, CGameState state, CUnit unit, int level)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, COobBrowserText.UnitRowName);
		return window is null ? null : new COobEntry(context, state, window, unit, level);
	}

	/// <summary>
	/// A press on the picture of a leader. The icon then hands the press to the row's button under it, as any icon
	/// over a button does, and that is what starts the drag: the leader's and not the unit's.
	/// </summary>
	private void PhotoInput(InputEvent input)
	{
		if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			_pressedPhoto = Unit.Leader is not null;
		}
	}

	private void ButtonHeld()
	{
		if (_pressedPhoto)
		{
			_pressedPhoto = false;
			LeaderDragStarted?.Invoke();
		}
		else
		{
			DragStarted?.Invoke();
		}
	}

	/// <summary>Whether the point is inside the row's window.</summary>
	public bool Holds(Vector2 point)
	{
		return Window.IsVisibleInTree() && Window.GetGlobalRect().HasPoint(point);
	}

	private void Fill()
	{
		if (_name is not null)
		{
			_name.Text = Unit.Name;
		}
		if (_info is not null)
		{
			_info.Text = COobBrowserText.UnitInfo(Unit);
		}
		_size?.SetFrame(COobBrowserText.SizeFrame(Unit));
		if (Unit.SubUnits.Count > 0)
		{
			_counter?.SetSprite(CUnitPanelText.BrigadeSprite(Unit.SubUnits[0]));
		}
	}

	/// <summary>Written again while the row is in view: what a command may have changed, and how the row stands to a drag.</summary>
	public void Update(bool selected, bool open, bool dragging, bool allowed, bool over)
	{
		Fill();
		if (!ReferenceEquals(_leaderShown.Leader, Unit.Leader))
		{
			_leaderShown = new CLeaderShown(Unit.Leader);
			if (Unit.Leader is { } leader)
			{
				CLeaderEntry.ShowPortrait(_context, _photo, leader);
			}
			else
			{
				// A unit whose leader went elsewhere - dragged to another row - shows the file's own picture again, where
				// it kept the portrait of the leader it no longer has.
				_photo?.ShowSprite();
			}
		}
		if (_drag is not null)
		{
			_drag.SetFrame(selected || (dragging && allowed && over) ? 2 : 1);
			_drag.Disabled = dragging && !allowed;
		}
		if (_expand is not null)
		{
			_expand.Visible = Unit.Children.Count > 0;
			_expand.SetFrame(open ? 2 : 1);
		}
	}
}
