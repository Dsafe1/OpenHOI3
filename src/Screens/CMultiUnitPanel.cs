using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The panel of several selected units: the original's <c>multi_unitpanel</c> at its file's place, and under it a row
/// for each unit in the order selected - the single panel's header, <c>unitpanel</c> - from <c>unitlist_start</c>,
/// <c>unitlist_offset</c> apart, as many as fit, with a slider beside them where there are more. Laid out and written
/// again every frame. See <c>docs/mechanics/selection.md</c>, section 5.
/// </summary>
public partial class CMultiUnitPanel : Control
{
	private const string WindowName = "multi_unitpanel";
	private const string SliderName = "multiunit_slider";

	/// <summary>The buttons whose systems are not built for one unit either: shown, never to be pressed.</summary>
	private static readonly string[] Unbuilt =
	{
		"upgrade_all", "detach_multiunit", "highercomm_multiunit", "disband_multiunit",
	};

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry? _player;

	private CGuiWindow? _window;
	private CGuiTextBox? _desc;
	private CGuiButton? _priority;
	private CGuiButton? _reinforce;
	private CGuiButton? _upgrade;
	private CGuiButton? _load;
	private CGuiButton? _loadPlanes;
	private CGuiButton? _unmount;
	private CGuiButton? _reorganise;
	private CGuiButton? _merge;
	private CGuiScrollbarType? _sliderType;
	private CGuiScrollbar? _slider;
	private int _sliderFit;

	private Vector2 _start = new(0f, 144f);
	private Vector2 _offset = new(16f, 40f);

	private readonly List<CUnit> _units = new();
	private readonly List<CUnitHeaderEntry> _rows = new();
	private readonly List<CUnit> _rowUnits = new();

	/// <summary>The rows marked with Shift, whose units alone are selected when Shift is let go.</summary>
	private readonly List<CUnit> _kept = new();

	/// <summary>What the rows' buttons asked of the selection this frame, done at the end of it.</summary>
	private readonly List<Action<CSelection>> _pending = new();

	/// <summary>Raised to change the units selected; the map view does it and tells everyone.</summary>
	public event Action<Action<CSelection>>? SelectionChanging;

	/// <summary>A row's leader button or portrait: the leader window for its unit.</summary>
	public event Action<CUnit>? LeaderWanted;

	/// <summary>The reorganise button: the two units, the first selected first.</summary>
	public event Action<CUnit, CUnit>? ReorganiseWanted;

	/// <summary>Raised when the panel has posted an order that changes where units stand on the map.</summary>
	public event Action? Ordered;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry? player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		if (_context.Types.FindPosition("unitlist_start") is { } start)
		{
			_start = new Vector2(start.Position.X, start.Position.Y);
		}
		if (_context.Types.FindPosition("unitlist_offset") is { } offset)
		{
			_offset = new Vector2(offset.Position.X, offset.Position.Y);
		}

		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_desc = _window.FindTextBox("desc");
		_priority = Checkbox("prio_checkbox", EUnitSupport.Priority);
		_reinforce = Checkbox("reinf_checkbox", EUnitSupport.Reinforcement);
		_upgrade = Checkbox("upgrade_checkbox", EUnitSupport.Upgrade);
		_load = _window.FindButton("load_multiunit");
		_loadPlanes = _window.FindButton("load_on_air_multiunit");
		_unmount = _window.FindButton("unmount_multiunit");
		if (_load is not null)
		{
			_load.Pressed += Load;
		}
		if (_loadPlanes is not null)
		{
			_loadPlanes.Pressed += LoadPlanes;
		}
		if (_unmount is not null)
		{
			_unmount.Pressed += Unmount;
		}
		// Two units reorganised: the first selected on the left, the second on the right (reorganise.md, section 1).
		_reorganise = _window.FindButton("reorganizebutton");
		if (_reorganise is not null)
		{
			_reorganise.Pressed += () =>
			{
				if (CSubUnitTransfer.MayReorganise(_units))
				{
					ReorganiseWanted?.Invoke(_units[0], _units[1]);
				}
			};
		}
		// The selected units made one, unless an AI agent runs any of them or any unit above one (selection.md, 5.4).
		_merge = _window.FindButton("mergebutton");
		if (_merge is not null)
		{
			_merge.Pressed += Merge;
		}
		if (_window.FindButton("close_multiunit") is { } close)
		{
			close.Pressed += () => _pending.Add(selection => selection.Clear());
		}
		foreach (string name in Unbuilt)
		{
			if (_window.FindButton(name) is { } button)
			{
				button.Disabled = true;
			}
		}
		// The file's slider is the size its declaration gives; the one shown is made as long as the rows that fit.
		if (_window.FindScrollbar(SliderName) is { } fileSlider)
		{
			_sliderType = fileSlider.GuiType as CGuiScrollbarType;
			fileSlider.Visible = false;
		}
		_window.SetTooltips(element => CMultiUnitTooltips.For(_context.Localisation.Localise, _simulation.State, _units, element));
	}

	/// <summary>Shows the panel for the units selected, two or more; for fewer it goes.</summary>
	public void Show(IReadOnlyList<CUnit> units)
	{
		_units.Clear();
		if (units.Count >= 2)
		{
			_units.AddRange(units);
		}
		Visible = _units.Count >= 2 && _window is not null;
		_kept.RemoveAll(unit => !_units.Contains(unit));
		if (!_rowUnits.SequenceEqual(_units))
		{
			RebuildRows();
		}
		if (Visible)
		{
			Update();
		}
	}

	public override void _Process(double delta)
	{
		if (!Visible || _window is null)
		{
			return;
		}
		// The rows marked with Shift are the selection once Shift is let go, of the first one's kind.
		if (_kept.Count > 0 && !Input.IsKeyPressed(Key.Shift))
		{
			var kept = new List<CUnit>(_kept);
			_kept.Clear();
			_pending.Add(selection =>
			{
				selection.Clear();
				foreach (CUnit unit in kept)
				{
					selection.Add(unit, unit.Owner);
				}
			});
		}
		Update();
		// What the rows' buttons asked, at the end of the frame's pass over the panel.
		if (_pending.Count > 0)
		{
			var pending = new List<Action<CSelection>>(_pending);
			_pending.Clear();
			SelectionChanging?.Invoke(selection =>
			{
				foreach (Action<CSelection> change in pending)
				{
					change(selection);
				}
			});
		}
	}

	/// <summary>The mouse wheel over the rows, while the slider is up: one notch, one row.</summary>
	public override void _Input(InputEvent @event)
	{
		if (!Visible || _slider is not { Visible: true } slider || _window is null
			|| @event is not InputEventMouseButton { Pressed: true } wheel
			|| wheel.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown))
		{
			return;
		}
		Rect2 panel = _window.GetGlobalRect();
		int fit = RowsThatFit();
		var area = new Rect2(panel.Position, new Vector2(panel.Size.X, panel.Size.Y + _offset.Y * fit));
		if (!area.HasPoint(wheel.Position))
		{
			return;
		}
		int notches = wheel.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
		float value = CMultiUnitText.Wheel(slider.Value.Raw / (float)CFixedPoint.Scale, notches, _rowUnits.Count, fit);
		slider.Value = new CFixedPoint((long)MathF.Round(value * CFixedPoint.Scale));
		GetViewport().SetInputAsHandled();
	}

	private CGuiButton? Checkbox(string name, EUnitSupport what)
	{
		CGuiButton? box = _window!.FindButton(name);
		if (box is not null)
		{
			box.Pressed += () => SetSupport(what, box.Checked);
		}
		return box;
	}

	/// <summary>Writes the panel and lays the rows out.</summary>
	private void Update()
	{
		Func<string, string> localise = _context.Localisation.Localise;
		CGameState state = _simulation.State;
		if (_desc is not null)
		{
			string text = CMultiUnitText.Desc(localise, _units);
			if (_desc.Text != text)
			{
				_desc.Text = text;
			}
		}
		Tick(_priority, EUnitSupport.Priority);
		Tick(_reinforce, EUnitSupport.Reinforcement);
		Tick(_upgrade, EUnitSupport.Upgrade);
		bool carry = CMultiUnitText.CarryShown(_units);
		Carry(_load, carry, CMultiUnitText.FleetFor(state, _units) is not null);
		Carry(_loadPlanes, carry, CMultiUnitText.PlaneFor(state, _units) is not null);
		Carry(_unmount, carry, CMultiUnitText.Aboard(_units) > 0);
		if (_reorganise is not null)
		{
			_reorganise.Disabled = !CSubUnitTransfer.MayReorganise(_units);
		}
		// Greyed where the rule says no; where it says yes, left as it was if an AI agent runs one of them.
		if (_merge is not null && _player is not null)
		{
			if (!CUnitMerge.MayMerge(state, _player, _units))
			{
				_merge.Disabled = true;
			}
			else if (!CUnitMerge.HasAgentAbove(_units))
			{
				_merge.Disabled = false;
			}
		}
		LayoutRows();
	}

	/// <summary>Ticked where any selected unit has the flag: there is no mixed state.</summary>
	private void Tick(CGuiButton? box, EUnitSupport what)
	{
		bool ticked = CMultiUnitText.FlagCount(_units, what) > 0;
		if (box is not null && box.Checked != ticked)
		{
			box.Checked = ticked;
		}
	}

	private static void Carry(CGuiButton? button, bool shown, bool enabled)
	{
		if (button is null)
		{
			return;
		}
		button.Visible = shown;
		button.Disabled = !enabled;
	}

	private int RowsThatFit()
	{
		return CMultiUnitText.RowsThatFit((int)GetViewportRect().Size.Y, (int)_start.Y, (int)_offset.Y);
	}

	/// <summary>
	/// The rows from <c>unitlist_start</c>, as many as fit; with more, moved right for the slider at their left, and
	/// the first shown by the slider's value.
	/// </summary>
	private void LayoutRows()
	{
		int fit = RowsThatFit();
		int rows = _rows.Count;
		float x = 0f;
		int first = 0;
		if (rows > fit && fit > 0)
		{
			x += _offset.X;
			CGuiScrollbar slider = Slider(fit);
			slider.Visible = true;
			first = CMultiUnitText.FirstRow(rows, fit, slider.Value.Raw / (float)CFixedPoint.Scale);
		}
		else if (_slider is not null)
		{
			_slider.Visible = false;
		}
		int shown = 0;
		for (int index = 0; index < rows; index++)
		{
			CUnitHeaderEntry row = _rows[index];
			bool up = index >= first && index < first + fit;
			row.Window.Visible = up;
			if (!up)
			{
				continue;
			}
			row.Window.MoveTo(new Vector2(_start.X + x, _start.Y + shown * _offset.Y));
			row.SetKept(_kept.Contains(_rowUnits[index]));
			row.Update();
			shown++;
		}
	}

	/// <summary>The slider beside the rows, made again when the number of rows that fit changes.</summary>
	private CGuiScrollbar Slider(int fit)
	{
		if (_slider is not null && _sliderFit == fit)
		{
			return _slider;
		}
		CFixedPoint value = _slider?.Value ?? CFixedPoint.Zero;
		_slider?.QueueFree();
		_slider = new CGuiScrollbar { Name = SliderName };
		if (_sliderType is not null)
		{
			_slider.Initialise(_sliderType, _context, _offset.Y * fit);
		}
		_window!.AddChild(_slider);
		_slider.Value = value;
		_sliderFit = fit;
		return _slider;
	}

	private void RebuildRows()
	{
		foreach (CUnitHeaderEntry row in _rows)
		{
			row.Window.QueueFree();
		}
		_rows.Clear();
		_rowUnits.Clear();
		CGameState state = _simulation.State;
		foreach (CUnit unit in _units)
		{
			if (state.Country(unit.Owner) is not { } owner
				|| CUnitHeaderEntry.Create(_context, state, owner, unit, _player?.Tag, row: true) is not { } row)
			{
				continue;
			}
			row.Deselected += () => _pending.Add(selection => selection.Remove(unit));
			row.OnlyPressed += () => OnlyPressed(unit, row);
			row.LeaderPressed += () => LeaderWanted?.Invoke(unit);
			row.Window.Visible = false;
			AddChild(row.Window);
			_rows.Add(row);
			_rowUnits.Add(unit);
		}
	}

	/// <summary>
	/// A row's "select this": the unit alone; with Ctrl, it and every unit under it; with Shift, the row marked, or
	/// unmarked, for when Shift is let go (selection.md, section 5.1).
	/// </summary>
	private void OnlyPressed(CUnit unit, CUnitHeaderEntry row)
	{
		if (Input.IsKeyPressed(Key.Shift))
		{
			if (!_kept.Remove(unit))
			{
				_kept.Add(unit);
			}
			row.SetKept(_kept.Contains(unit));
			return;
		}
		if (Input.IsKeyPressed(Key.Ctrl))
		{
			// The unit and every unit under it at any depth; those not selected yet are selected whatever their kind.
			var command = new List<CUnit>(unit.WithAllBelow());
			var keep = new HashSet<CUnit>(command);
			_pending.Add(selection =>
			{
				selection.RemoveWhere(selected => !keep.Contains(selected));
				foreach (CUnit below in command)
				{
					selection.Force(below);
				}
			});
			return;
		}
		_pending.Add(selection =>
		{
			selection.Clear();
			selection.Force(unit);
		});
	}

	/// <summary>A checkbox clicked: every selected unit whose flag differs is set to the box's new state.</summary>
	private void SetSupport(EUnitSupport what, bool on)
	{
		foreach (CUnit unit in _units)
		{
			if (CMultiUnitText.HasFlag(unit, what) != on)
			{
				Post(new CSetUnitSupportCommand(unit.Owner, unit.Id, what, on), unit);
			}
		}
	}

	/// <summary><c>load_multiunit</c>: a load for each selected unit, all naming the fleet found.</summary>
	private void Load()
	{
		if (CMultiUnitText.FleetFor(_simulation.State, _units) is not { } fleet)
		{
			return;
		}
		foreach (CUnit unit in _units.ToList())
		{
			Post(new CLoadUnitCommand(unit.Owner, unit.Id, fleet.Id), unit);
		}
	}

	/// <summary><c>load_on_air_multiunit</c>: the same with the air unit found.</summary>
	private void LoadPlanes()
	{
		if (CMultiUnitText.PlaneFor(_simulation.State, _units) is not { } plane)
		{
			return;
		}
		foreach (CUnit unit in _units.ToList())
		{
			Post(new CLoadOnPlaneCommand(unit.Owner, unit.Id, plane.Id), unit);
		}
	}

	/// <summary><c>unmount_multiunit</c>: first each unit aboard a plane, then each aboard a fleet, with no test of where.</summary>
	private void Unmount()
	{
		foreach (CUnit unit in _units.ToList())
		{
			if (unit.Plane is not null)
			{
				Post(new CUnloadFromPlaneCommand(unit.Owner, unit.Id), unit);
			}
		}
		foreach (CUnit unit in _units.ToList())
		{
			if (unit.Transport is not null)
			{
				Post(new CUnloadUnitCommand(unit.Owner, unit.Id), unit);
			}
		}
	}

	/// <summary><c>mergebutton</c>: one merge of every selected unit, in selection order; nothing where an AI agent runs one.</summary>
	private void Merge()
	{
		if (_player is null || _units.Count == 0 || CUnitMerge.HasAgentAbove(_units))
		{
			return;
		}
		var ids = new List<int>();
		foreach (CUnit unit in _units)
		{
			ids.Add(unit.Id);
		}
		Post(new CMergeCommand(_units[0].Owner, ids), _units[0]);
	}

	/// <summary>A command of the player's own about one of its units, posted where it may run.</summary>
	private void Post(CCommand command, CUnit unit)
	{
		if (_player is null || unit.Owner != _player.Tag || !command.MayExecute(_simulation.State))
		{
			return;
		}
		_simulation.Post(command);
		Ordered?.Invoke();
	}
}
