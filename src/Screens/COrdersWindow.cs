using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
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
/// <c>accept</c>. See <c>docs/mechanics/fleetsandair.md</c>, section 4.
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
	private readonly List<CGuiButton> _boxes = new();
	private List<COrderRow> _rows = new();
	private CUnit? _unit;
	private int _province;
	private int _ticked = -1;
	private int _stance;
	private int _priority;

	/// <summary>Raised when an order has been given.</summary>
	public event Action? Ordered;

	/// <summary>Raised when the window comes up or goes away.</summary>
	public event Action? Toggled;

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
		_stance = COrdersText.Aggressive;
		_priority = 0;
		AddChild(window);

		CProvince target = state.Province(province);
		if (window.FindTextBox("title") is { } title)
		{
			title.Text = target.Name;
		}

		_rows = COrdersText.Rows(state, _player, unit, target);
		_boxes.Clear();
		CGuiListBox? list = window.FindListBox("orders_list");
		for (int index = 0; index < _rows.Count; index++)
		{
			if (CGuiWindow.CreateEntry(_context, COrdersText.EntryWindow) is not { } entry)
			{
				break;
			}
			if (entry.FindTextBox("name") is { } text)
			{
				text.Text = _context.Localisation.Localise(_rows[index].NameKey);
			}
			if (entry.FindButton("checkbox") is { } box)
			{
				int row = index;
				box.Disabled = !_rows[index].MayGive;
				box.Pressed += () => Tick(row);
				_boxes.Add(box);
			}
			list?.Add(entry);
		}

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

		Tick(COrdersText.Suggested(_rows, state, _player, unit, target));
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
		_window.QueueFree();
		_window = null;
		_accept = null;
		_stanceValue = null;
		_priorityValue = null;
		_boxes.Clear();
		_unit = null;
		_ticked = -1;
		Toggled?.Invoke();
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
		if (_accept is not null)
		{
			_accept.Disabled = _ticked < 0;
		}
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
		var command = new CSetUnitOrderCommand(_player.Tag, unit.Id, _rows[_ticked].Order, _province);
		if (command.MayExecute(_simulation.State))
		{
			_simulation.Post(command);
			Ordered?.Invoke();
		}
		Close();
	}
}
