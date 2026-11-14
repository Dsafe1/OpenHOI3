using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
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
	private bool _isLand;
	private CDateWidget? _landDate;
	private CGuiButton? _sync;
	private CGuiTextBox? _syncTitle;
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
	/// <see cref="CAirAreaColours.Nothing"/> for a province the unit cannot reach - and with
	/// null when it is to show its own mode again.
	/// </summary>
	public event Action<Func<CProvince, CColor>?>? AreaShown;

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

	/// <summary>The units selected on the map, whose Accept gives the order to every one of the window's kind.</summary>
	public Func<IReadOnlyList<CUnit>>? Selection { get; set; }

	/// <summary>
	/// The units Accept gives the order to: every selected unit of the window's unit's kind - for the land window every
	/// selected land unit (landorders.md, section 3) - the window's unit first, or that unit alone where it is not among
	/// them (selection.md, section 6).
	/// </summary>
	private List<CUnit> Units()
	{
		var units = new List<CUnit> { _unit! };
		IReadOnlyList<CUnit> selected = Selection?.Invoke() ?? Array.Empty<CUnit>();
		if (!selected.Contains(_unit!))
		{
			return units;
		}
		foreach (CUnit unit in selected)
		{
			bool sameKind = _isLand ? CMovementRules.IsLandUnit(unit) : unit.Kind == _unit!.Kind;
			if (unit != _unit && sameKind && unit.Owner == _player.Tag)
			{
				units.Add(unit);
			}
		}
		return units;
	}

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
		_isLand = CMovementRules.IsLandUnit(unit);
		// The land window has no stance of its own: its orders go out defensive (landorders.md, section 3).
		_stance = isAir ? AirMemory.Stance : _isLand ? CMission.Defensive : COrdersText.Aggressive;
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
		// The land window has neither a stance nor a priority to set (landorders.md, section 2).
		if (!_isLand)
		{
			_stanceValue = window.FindTextBox("stance_value");
			_priorityValue = window.FindTextBox("priority_value");
			Wire(window, "decrease_stance_button", () => _stance = COrdersText.StepStance(_stance, -1));
			Wire(window, "increase_stance_button", () => _stance = COrdersText.StepStance(_stance, 1));
			Wire(window, "decrease_priority_button", () => _priority = COrdersText.StepPriority(_priority, -1));
			Wire(window, "increase_priority_button", () => _priority = COrdersText.StepPriority(_priority, 1));
		}

		if (isAir)
		{
			_air = new CAirOrdersControls(_context, state, _player, unit, window, province, AirMemory.Shape);
			_air.Changed += AirChanged;
			// The order kept is the last window's, or with none the unit's own.
			BuildRows(AirMemory.Order != EUnitOrder.None ? AirMemory.Order : unit.Order);
		}
		else if (_isLand)
		{
			// A date widget starting now, and the box of a move's synchronised arrival (landorders.md, section 2).
			_landDate = CDateWidget.Create(_context, state, window, COrdersText.LandDatePlace, state.TotalHours);
			_sync = window.FindButton(COrdersText.SyncBox);
			_syncTitle = window.FindTextBox(COrdersText.SyncTitle);
			BuildRows(EUnitOrder.None);
			ShowLand();
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
		_isLand = false;
		_landDate = null;
		_sync = null;
		_syncTitle = null;
		Toggled?.Invoke();
	}

	public override void _Process(double delta)
	{
		if (_window is not null && _isLand)
		{
			ShowLand();
		}
		// The air window's every frame: a paradrop's start held at now and PARADROP_MIN_TIME hours (transport.md, section 13.4).
		_air?.HoldStart(TickedOrder);
	}

	/// <summary>
	/// The land window's frame (landorders.md, section 2): the box of a synchronised arrival shown only under Move
	/// Order, the date raised to the estimate while it is ticked, and the day or night at the date's hour.
	/// </summary>
	private void ShowLand()
	{
		if (_unit is null)
		{
			return;
		}
		bool move = TickedOrder == EUnitOrder.Move;
		if (_sync is not null)
		{
			_sync.Visible = move;
		}
		if (_syncTitle is not null)
		{
			_syncTitle.Visible = move;
			_syncTitle.Text = _context.Localisation.Localise("ORDER_SYNC_ARRIVAL");
		}
		if (_landDate is not null && Synchronised)
		{
			_landDate.HoldAtLeast(Estimate());
		}
		long hour = _landDate?.Hour ?? _simulation.State.TotalHours;
		CProvince province = _simulation.State.Province(_province);
		bool night = CAirOrdersText.IsNightAtStart(_simulation.State, province, hour);
		Func<string, string> localise = _context.Localisation.Localise;
		if (_window!.FindTextBox(CAirOrdersText.DayTimeTitle) is { } title)
		{
			title.Text = CAirOrdersText.DayTimeText(localise, province.Name);
		}
		_window.FindIcon(CAirOrdersText.DayTimeIcon)?.SetFrame(CAirOrdersText.DayTimeFrame(night));
		if (_window.FindTextBox(CAirOrdersText.DayTimeDescription) is { } description)
		{
			description.Text = localise(CAirOrdersText.DayTimeKey(night));
		}
	}

	/// <summary>Whether the land window gives a move's synchronised arrival: Move Order ticked and its box with it.</summary>
	private bool Synchronised => _isLand && TickedOrder == EUnitOrder.Move && _sync is { Checked: true, Visible: true };

	/// <summary>The latest estimated arrival of the units the order is for (landorders.md, section 4).</summary>
	private long Estimate()
	{
		return CArrivalEstimate.Latest(_simulation.State, _player, LandUnits(), _simulation.State.Province(_province));
	}

	/// <summary>The land window's: the selected land units that are not falling back (landorders.md, section 3).</summary>
	private List<CUnit> LandUnits()
	{
		return Units().Where(unit => CMovementRules.IsLandUnit(unit) && !unit.IsRetreating).ToList();
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
		if (_isLand)
		{
			return COrdersTooltips.Land(_context.Localisation.Localise, state, province,
				_landDate?.Hour ?? state.TotalHours, element);
		}
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
		_rows = COrdersText.Rows(state, _player, Units(), target);
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
			// The land window's rows say nothing (landorders.md, section 9).
			if (!_isLand)
			{
				entry.SetTooltips(_ => COrdersTooltips.Row(_context.Localisation.Localise, state, unit, target, order), "name");
			}
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
			_air.MakeArea(TickedOrder);         // which redoes accept and the map; the next frame holds a paradrop's start
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
		// The order goes to every selected unit of the window's kind, each its own command (selection.md, section 6).
		if (_isLand)
		{
			AcceptLand();
			return;
		}
		if (_air is { Area: { } area })
		{
			_air.HoldStart(_rows[_ticked].Order);
		}
		foreach (CUnit given in Units())
		{
			CCommand command;
			if (_air is { Area: { } provinces })
			{
				// A change of base under way is left to finish. The priority is not sent: the
				// original's air window shows it and never gives it.
				if (!CDropOrderCommand.MayDrop(given.Order))
				{
					continue;
				}
				command = new CSetUnitOrderCommand(_player.Tag, given.Id, _rows[_ticked].Order, _province, _stance,
					0, 0, provinces.Provinces, _air.Time, _air.EndHour, _air.StartHour);
			}
			else
			{
				command = new CSetUnitOrderCommand(_player.Tag, given.Id, _rows[_ticked].Order, _province, _stance,
					_priority, _rows[_ticked].Convoy);
			}
			if (command.MayExecute(_simulation.State))
			{
				_simulation.Post(command);
				Ordered?.Invoke();
			}
		}
		Close();
	}

	/// <summary>
	/// The land window's accept (landorders.md, section 3): the ticked order for every selected land unit not falling
	/// back, defensive, not to start before the date's hour - or, a move synchronised, not to arrive before the latest
	/// estimate, worked out afresh.
	/// </summary>
	private void AcceptLand()
	{
		EUnitOrder order = _rows[_ticked].Order;
		bool synchronised = Synchronised;
		long? start = synchronised ? null : _landDate?.Hour ?? _simulation.State.TotalHours;
		long? arrive = synchronised ? Estimate() : null;
		foreach (CUnit given in LandUnits())
		{
			var command = new CSetUnitOrderCommand(_player.Tag, given.Id, order, _province, CMission.Defensive,
				startHour: start, arriveHour: arrive);
			if (command.MayExecute(_simulation.State))
			{
				_simulation.Post(command);
				Ordered?.Invoke();
			}
		}
		Close();
	}
}
