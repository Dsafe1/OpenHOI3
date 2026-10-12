using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The window a right click with a fleet or an air unit brings up: the original's
/// <c>naval_unit_order</c> and <c>air_unit_order</c>. It is headed with the province's
/// name and lists the orders for it, one of which is ticked and given with
/// <c>accept</c>; the air one also makes the mission's area and its hours and dates
/// (<see cref="CAirOrdersControls"/>), colours the map while it is up, and remembers its
/// priority, stance, shape and order for the next. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 4.
/// </summary>
public partial class COrdersWindow : Control
{
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;

	private CGuiWindow? _window;
	private CGuiButton? _accept;
	private CGuiTextBox? _stanceValue;
	private CGuiTextBox? _priorityValue;
	private CGuiListBox? _list;
	private readonly List<CGuiButton> _boxes = new();
	private List<COrderRow> _rows = new();
	private CAirOrdersControls? _air;
	private CUnit? _unit;
	private int _province;
	private int _ticked = -1;
	private int _stance;
	private int _priority;

	/// <summary>Raised when an order has been given.</summary>
	public event Action? Ordered;

	/// <summary>Raised when the window comes up or goes away.</summary>
	public event Action? Toggled;

	/// <summary>
	/// Raised when the map is to show an air mission's area, with each province's colour -
	/// null for a province the unit cannot reach - and with null when it is to show its
	/// own mode again.
	/// </summary>
	public event Action<Func<CProvince, CColor?>?>? AreaShown;

	/// <summary>What the last air window kept for the next: the session's.</summary>
	public CAirOrdersMemory AirMemory { get; private set; } = CAirOrdersMemory.First;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public bool IsOpen => _window is not null;

	/// <summary>The unit the window is up for, or null.</summary>
	public CUnit? Unit => _unit;

	public override void _Ready()
	{
		Name = "orders";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>
	/// Brings the window up for a unit and a province, in place of any that is up.
	/// Answers false for a unit that has no such window.
	/// </summary>
	public bool Open(CUnit unit, int province)
	{
		Close();
		string name = COrdersText.WindowFor(unit);
		CGameState state = _simulation.State;
		if (name.Length == 0 || province <= 0 || province >= state.Provinces.Count
			|| CGuiWindow.Create(_context, name) is not { } window)
		{
			return false;
		}
		_window = window;
		_unit = unit;
		_province = province;
		bool isAir = unit.Kind == EUnitKind.Air;
		_stance = isAir ? AirMemory.Stance : COrdersText.Aggressive;
		_priority = isAir ? AirMemory.Priority : 0;
		AddChild(window);
		// The window answers for its elements by name; each row of the list for itself.
		window.SetTooltips(Tooltip, "stance_label", "stance_value", "priority_label", "priority_value",
			"daytime_title", "daytime_icondesc");

		// The title is written once: a window moved to another province keeps it.
		if (window.FindTextBox("title") is { } title)
		{
			title.Text = state.Province(province).Name;
		}

		_list = window.FindListBox("orders_list");
		_accept = window.FindButton("accept");
		if (_accept is not null)
		{
			_accept.Pressed += Accept;
		}
		if (window.FindButton("cancel") is { } cancel)
		{
			cancel.Pressed += Close;
		}
		_stanceValue = window.FindTextBox("stance_value");
		_priorityValue = window.FindTextBox("priority_value");
		Wire(window, "decrease_stance_button", () => _stance = COrdersText.StepStance(_stance, -1));
		Wire(window, "increase_stance_button", () => _stance = COrdersText.StepStance(_stance, 1));
		Wire(window, "decrease_priority_button", () => _priority = COrdersText.StepPriority(_priority, -1));
		Wire(window, "increase_priority_button", () => _priority = COrdersText.StepPriority(_priority, 1));

		if (isAir)
		{
			_air = new CAirOrdersControls(_context, state, _player, unit, window, province, AirMemory.Shape);
			_air.Changed += AirChanged;
			// The order kept is the last window's, or with none the unit's own.
			BuildRows(AirMemory.Order != EUnitOrder.None ? AirMemory.Order : unit.Order);
		}
		else
		{
			BuildRows(EUnitOrder.None);
		}
		ShowFigures();
		Toggled?.Invoke();
		return true;
	}

	public void Close()
	{
		if (_window is null)
		{
			return;
		}
		if (_air is not null)
		{
			AirMemory = new CAirOrdersMemory(_priority, _stance, _air.Shape, TickedOrder);
			_air = null;
			AreaShown?.Invoke(null);
		}
		_window.QueueFree();
		_window = null;
		_accept = null;
		_stanceValue = null;
		_priorityValue = null;
		_list = null;
		_boxes.Clear();
		_unit = null;
		_ticked = -1;
		Toggled?.Invoke();
	}

	/// <summary>
	/// A right click on the map while the window is up, with whether shift is held. The
	/// air window takes it for its area, or is moved to the province; the naval one does
	/// nothing with it.
	/// </summary>
	public void MapRightClick(int province, bool shift)
	{
		if (_air is null || _unit is null || province <= 0 || province >= _simulation.State.Provinces.Count)
		{
			return;
		}
		if (_air.RightClick(province, shift))
		{
			_province = province;
			_air.SetProvince(province);
			BuildRows(TickedOrder);
		}
	}

	private CTooltip Tooltip(string element)
	{
		if (_unit is not { } unit)
		{
			return CTooltip.Empty;
		}
		CGameState state = _simulation.State;
		CProvince province = state.Province(_province);
		bool acceptDisabled = _accept?.Disabled ?? true;
		return _air is null
			? COrdersTooltips.Naval(_context.Localisation.Localise, state, unit, province, acceptDisabled, TickedOrder, element)
			: COrdersTooltips.Air(_context.Localisation.Localise, state, province, acceptDisabled, _ticked >= 0,
				_air.HasProvinces, _air.StartHour, element);
	}

	private EUnitOrder TickedOrder => _ticked >= 0 && _ticked < _rows.Count ? _rows[_ticked].Order : EUnitOrder.None;

	/// <summary>Makes the list for the window's province, ticking the row the original would.</summary>
	private void BuildRows(EUnitOrder keep)
	{
		CGameState state = _simulation.State;
		CProvince target = state.Province(_province);
		_list?.Clear();
		_boxes.Clear();
		_rows = COrdersText.Rows(state, _player, _unit!, target);
		for (int index = 0; index < _rows.Count; index++)
		{
			if (CGuiWindow.CreateEntry(_context, COrdersText.EntryWindow) is not { } entry)
			{
				break;
			}
			if (entry.FindTextBox("name") is { } text)
			{
				text.Text = COrdersText.RowText(_context.Localisation.Localise, _rows[index]);
			}
			COrderRow order = _rows[index];
			CUnit unit = _unit!;
			entry.SetTooltips(_ => COrdersTooltips.Row(_context.Localisation.Localise, state, unit, target, order), "name");
			if (entry.FindButton("checkbox") is { } box)
			{
				int row = index;
				box.Disabled = !_rows[index].MayGive;
				box.Pressed += () => Tick(row);
				_boxes.Add(box);
			}
			_list?.Add(entry);
		}
		Tick(_air is null
			? COrdersText.Suggested(_rows, state, _player, _unit!, target)
			: CAirOrdersText.Suggested(_rows, state, _player, _unit!, target, keep));
	}

	private void Wire(CGuiWindow window, string button, Action change)
	{
		if (window.FindButton(button) is { } found)
		{
			found.Pressed += () =>
			{
				change();
				ShowFigures();
			};
		}
	}

	/// <summary>Ticks one row's box and clears the others: only one order is given.</summary>
	private void Tick(int row)
	{
		_ticked = row >= 0 && row < _rows.Count && _rows[row].MayGive ? row : -1;
		for (int index = 0; index < _boxes.Count; index++)
		{
			_boxes[index].Checked = index == _ticked;
		}
		if (_air is not null)
		{
			_air.MakeArea(TickedOrder);         // which redoes accept and the map
			return;
		}
		ShowAccept();
	}

	private void AirChanged()
	{
		ShowAccept();
		if (_air?.Area is { } area && _unit is { } unit)
		{
			var listed = new HashSet<int>(area.Provinces);
			CGameState state = _simulation.State;
			AreaShown?.Invoke(province => CAirAreaColours.Of(state, unit, area.Order, listed, province));
		}
	}

	/// <summary>Accept can be pressed with a row ticked - and for an air mission, with a province on its list.</summary>
	private void ShowAccept()
	{
		bool may = _ticked >= 0 && (_air is null || _air.HasProvinces);
		if (_accept is not null)
		{
			_accept.Disabled = !may;
		}
		_air?.ShowAccept(may);
	}

	private void ShowFigures()
	{
		if (_stanceValue is not null)
		{
			_stanceValue.Text = _context.Localisation.Localise(COrdersText.StanceKey(_stance));
		}
		if (_priorityValue is not null)
		{
			_priorityValue.Text = _priority.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
	}

	private void Accept()
	{
		if (_unit is not { } unit || _ticked < 0)
		{
			return;
		}
		CCommand command;
		if (_air is { Area: { } area })
		{
			// A change of base under way is left to finish. The priority is not sent: the
			// original's air window shows it and never gives it.
			if (!CDropOrderCommand.MayDrop(unit.Order))
			{
				Close();
				return;
			}
			command = new CSetUnitOrderCommand(_player.Tag, unit.Id, _rows[_ticked].Order, _province, _stance,
				0, 0, area.Provinces, _air.Time, _air.EndHour, _air.StartHour);
		}
		else
		{
			command = new CSetUnitOrderCommand(_player.Tag, unit.Id, _rows[_ticked].Order, _province, _stance,
				_priority, _rows[_ticked].Convoy);
		}
		if (command.MayExecute(_simulation.State))
		{
			_simulation.Post(command);
			Ordered?.Invoke();
		}
		Close();
	}
}
