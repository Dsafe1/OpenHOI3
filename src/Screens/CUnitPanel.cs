using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The panel of a selected unit: the original's <c>single_unitpanel</c>, which is one
/// list. It is filled with a fixed run of entries - the units above, the header, the
/// status, the order, the buttons, the brigades, the units under it - each a window of
/// its own, and every one is written again each frame the panel is up, as the
/// original writes them. See <c>docs/mechanics/unitpanel.md</c>.
/// </summary>
public partial class CUnitPanel : Control
{
	private const string WindowName = "single_unitpanel";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry? _player;

	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private readonly List<CUnitPanelEntry> _entries = new();
	private readonly List<CUnit> _parents = new();
	private CUnit? _unit;
	private bool _hasChildren;
	private readonly List<CCombat> _combats = new();

	/// <summary>Raised when a row of the panel asks for another unit to be the one selected, or for none.</summary>
	public event Action<CUnit?>? UnitChosen;

	/// <summary>Raised when the row of a battle the unit is in is clicked.</summary>
	public event Action<CCombat>? CombatChosen;

	/// <summary>Raised when the panel asks for the map to show a province.</summary>
	public event Action<int>? ProvinceWanted;

	/// <summary>Raised when the panel has posted an order that changes where units stand on the map.</summary>
	public event Action? Ordered;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry? player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public bool IsOpen => Visible;

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_list = _window.FindListBox("list");
		_list?.UseEntryHeights();
		// Two windows of its own that come up over it: what a brigade may be upgraded
		// to, and the stances an AI may be given. Neither is built.
		foreach (string name in new[] { "select_upgrade_window", "sup_stance_change_window" })
		{
			if (_window.FindWindow(name) is { } child)
			{
				child.Visible = false;
			}
		}
		// The order of battle browser is not built.
		if (_window.FindButton("oob_button") is { } browser)
		{
			browser.Disabled = true;
		}
	}

	/// <summary>Shows the panel for a unit, or takes it down for none.</summary>
	public void ShowUnit(CUnit? unit)
	{
		if (ReferenceEquals(unit, _unit))
		{
			return;
		}
		_unit = unit;
		Visible = unit is not null && _list is not null;
		Rebuild();
	}

	public override void _Process(double delta)
	{
		if (!Visible || _unit is not { } unit || _list is null)
		{
			return;
		}
		// The run of entries depends on what is above the unit, on whether anything is
		// under it and on the battles it is in; when any of them changes the list is made
		// again.
		if (!Above(unit).SequenceEqual(_parents) || unit.Children.Count > 0 != _hasChildren
			|| !Battles(unit).SequenceEqual(_combats))
		{
			Rebuild();
			return;
		}
		bool resized = false;
		foreach (CUnitPanelEntry entry in _entries)
		{
			entry.Update();
			resized |= entry.Resized;
		}
		if (resized)
		{
			_list.Relayout();
		}
	}

	/// <summary>What a unit has a row for at the end of its panel: its battles, and then the bombing it is in.</summary>
	private static List<CCombat> Battles(CUnit unit)
	{
		var battles = new List<CCombat>(unit.Combats);
		if (unit.Bombing is { } bombing)
		{
			battles.Add(bombing);
		}
		return battles;
	}

	/// <summary>The units above one, the highest first.</summary>
	private static List<CUnit> Above(CUnit unit)
	{
		var above = new List<CUnit>();
		for (CUnit? parent = unit.Parent; parent is not null; parent = parent.Parent)
		{
			above.Insert(0, parent);
		}
		return above;
	}

	/// <summary>
	/// Empties the list and fills it in the original's order. It has more entries than
	/// these for a headquarters an AI commands and for what a fleet or an air unit
	/// carries; neither is built.
	/// </summary>
	private void Rebuild()
	{
		if (_list is null)
		{
			return;
		}
		_list.Clear();
		_entries.Clear();
		_parents.Clear();
		_combats.Clear();
		if (_unit is not { } unit || _simulation.State.Country(unit.Owner) is not { } owner)
		{
			return;
		}
		CGameState state = _simulation.State;
		bool mayOrder = _player is not null && unit.Owner == _player.Tag;

		_parents.AddRange(Above(unit));
		foreach (CUnit parent in _parents)
		{
			if (CUnitParentEntry.Create(_context, parent) is { } row)
			{
				row.Selected += () => UnitChosen?.Invoke(parent);
				Add(row);
			}
		}

		if (CUnitHeaderEntry.Create(_context, state, owner, unit) is { } header)
		{
			header.Deselected += () => UnitChosen?.Invoke(null);
			Add(header);
		}
		if (CUnitStatusEntry.Create(_context, state, owner, unit) is { } status)
		{
			status.LocationPressed += () => ProvinceWanted?.Invoke(unit.Location);
			Add(status);
		}
		if (CUnitOrdersEntry.Create(_context, state, unit) is { } orders)
		{
			orders.CancelPressed += () => CancelOrder(unit);
			Add(orders);
		}
		// A headquarters has a row for the AI that may command it; a division has none.
		if (unit.Kind is EUnitKind.Theatre or EUnitKind.ArmyGroup or EUnitKind.Army or EUnitKind.Corps
			&& CUnitObjectivesEntry.Create(_context) is { } objectives)
		{
			Add(objectives);
		}
		if (CUnitButtonsEntry.Create(_context, unit, mayOrder) is { } buttons)
		{
			buttons.SupportChanged += (what, on) => SetSupport(unit, what, on);
			Add(buttons);
		}
		if (CUnitBrigadesEntry.Create(_context, state, owner, unit) is { } brigades)
		{
			Add(brigades);
		}
		_hasChildren = unit.Children.Count > 0;
		if (_hasChildren && CUnitChildrenEntry.Create(_context, state, owner, unit) is { } children)
		{
			children.Selected += child => UnitChosen?.Invoke(child);
			Add(children);
		}
		// Last, a row for each battle the unit is in, and after them one for its bombing.
		_combats.AddRange(Battles(unit));
		foreach (CCombat combat in _combats)
		{
			if (CUnitCombatEntry.Create(_context, state, unit, combat) is { } battle)
			{
				battle.Selected += () => CombatChosen?.Invoke(combat);
				Add(battle);
			}
		}
		_list.Relayout();
	}

	private void Add(CUnitPanelEntry entry)
	{
		_entries.Add(entry);
		_list!.Add(entry.Window);
	}

	/// <summary>
	/// The button beside the order: a land unit on the march is halted, and any other has
	/// its order taken back. Only the player's own.
	/// </summary>
	private void CancelOrder(CUnit unit)
	{
		if (_player is null || unit.Owner != _player.Tag)
		{
			return;
		}
		// A land unit on the march is halted; any other has its order taken back, and
		// keeps the way it was on.
		CCommand command = CMovementRules.IsLandUnit(unit) && unit.IsMoving && !unit.IsRetreating
			? new CCancelMovementCommand(_player.Tag, unit.Id)
			: new CDropOrderCommand(_player.Tag, unit.Id);
		if (command.MayExecute(_simulation.State))
		{
			_simulation.Post(command);
			Ordered?.Invoke();
		}
	}

	private void SetSupport(CUnit unit, EUnitSupport what, bool on)
	{
		if (_player is null || unit.Owner != _player.Tag)
		{
			return;
		}
		var command = new CSetUnitSupportCommand(_player.Tag, unit.Id, what, on);
		if (command.MayExecute(_simulation.State))
		{
			_simulation.Post(command);
		}
	}
}
