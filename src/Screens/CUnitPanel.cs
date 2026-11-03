using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
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

	/// <summary>A unit the player put aboard a fleet, whose selection goes to the fleet once it is aboard.</summary>
	private (CUnit Unit, CUnit Fleet)? _boarding;

	private CLeaderSelectionPanel? _leaders;
	private CMultiUnitPanel? _multi;
	private CConfirmDialog? _confirm;

	/// <summary>
	/// The leader window's filter of traits, kept from one window to the next for each kind
	/// of leader for as long as the game is up, as the original keeps it in its game state.
	/// </summary>
	private readonly Dictionary<ELeaderType, List<CTrait?>> _keptTraitFilters = new();

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

	/// <summary>Raised by the multi-unit panel to change the units selected (selection.md, section 5).</summary>
	public event Action<Action<CSelection>>? SelectionChanging;

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		// The panel of several selected units stands in this one's place; the single panel is never up beside it.
		_multi = new CMultiUnitPanel();
		_multi.Setup(_context, _simulation, _player);
		_multi.SelectionChanging += change => SelectionChanging?.Invoke(change);
		_multi.LeaderWanted += OpenLeaders;
		_multi.Ordered += () => Ordered?.Invoke();
		AddChild(_multi);

		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_window.Visible = _unit is not null;                 // up only for one unit; the multi-unit panel may be up without it
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

	/// <summary>
	/// Shows the panels for the units selected: for one the single panel, for two or more the multi-unit panel and its
	/// rows, for none neither (selection.md, section 5).
	/// </summary>
	public void ShowSelection(IReadOnlyList<CUnit> units)
	{
		ShowUnit(units.Count == 1 ? units[0] : null);
		_multi?.Show(units);
		ShowPanels();
	}

	/// <summary>Shows the single panel for a unit, or takes it down for none.</summary>
	public void ShowUnit(CUnit? unit)
	{
		if (ReferenceEquals(unit, _unit))
		{
			return;
		}
		_unit = unit;
		if (_window is not null)
		{
			_window.Visible = unit is not null;
		}
		ShowPanels();
		Rebuild();
	}

	private void ShowPanels()
	{
		Visible = (_unit is not null && _list is not null) || _multi is { Visible: true };
	}

	public override void _Process(double delta)
	{
		// The leader window a row of several opened is written as the header's is.
		if (_unit is null && _leaders is not null)
		{
			if (_leaders.IsShown)
			{
				_leaders.Update();
			}
			else
			{
				CloseLeaders();
			}
		}
		if (!Visible || _unit is not { } unit || _list is null)
		{
			return;
		}
		// The selection goes to the fleet or the air unit once the command has run, so that its list is made with the
		// unit in it.
		if (_boarding is ({ } boarded, { } carrier) && boarded == unit)
		{
			if ((boarded.Transport ?? boarded.Plane) == carrier)
			{
				_boarding = null;
				UnitChosen?.Invoke(carrier);
				return;
			}
		}
		else
		{
			_boarding = null;
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
		// The original's header writes the leader window each frame, and ends it once hidden.
		if (_leaders is not null)
		{
			if (_leaders.IsShown)
			{
				_leaders.Update();
			}
			else
			{
				CloseLeaders();
			}
		}
	}

	/// <summary>
	/// The header's leader button: whatever window was up for it goes, and a new one is
	/// made - so a second click opens it afresh, it does not close it.
	/// </summary>
	private void OpenLeaders(CUnit unit)
	{
		CloseLeaders();
		_leaders = CLeaderSelectionPanel.Open(this, _context, _simulation, _player, unit, _keptTraitFilters);
		if (_leaders is not null)
		{
			_leaders.RemoveAllPressed += ConfirmRemoveAll;
		}
	}

	private void CloseLeaders()
	{
		_leaders?.Destroy();
		_leaders = null;
	}

	/// <summary>
	/// "Remove all" asks first, with the original's dialog. Agreed, every unit of the
	/// player's country is left with no leader.
	/// </summary>
	private void ConfirmRemoveAll()
	{
		_confirm?.Close();
		// It stands over the interface, not in the unit's panel: it outlives the leader window.
		if (GetParent() is not { } layer)
		{
			return;
		}
		Func<string, string> localise = _context.Localisation.Localise;
		_confirm = CConfirmDialog.Open(layer, _context, CLeaderWindowText.ConfirmWindowName,
			localise(CLeaderWindowText.ConfirmTitleKey), localise(CLeaderWindowText.ConfirmTextKey));
		if (_confirm is not null)
		{
			_confirm.Agreed += () =>
			{
				if (_player is not null)
				{
					var command = new CRemoveAllLeadersCommand(_player.Tag);
					if (command.MayExecute(_simulation.State))
					{
						_simulation.Post(command);
					}
				}
			};
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
	/// these for a headquarters an AI commands, which is not built.
	/// </summary>
	private void Rebuild()
	{
		// The window a leader is chosen in belongs to the header, which goes now.
		CloseLeaders();
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
				row.Selected += () => ChooseRow(parent);
				Add(row);
			}
		}

		if (CUnitHeaderEntry.Create(_context, state, owner, unit, _player?.Tag) is { } header)
		{
			header.Deselected += () => UnitChosen?.Invoke(null);
			header.LeaderPressed += () => OpenLeaders(unit);
			Add(header);
		}
		if (CUnitStatusEntry.Create(_context, state, owner, unit) is { } status)
		{
			status.LocationPressed += () => ProvinceWanted?.Invoke(unit.Location);
			Add(status);
		}
		if (CUnitOrdersEntry.Create(_context, state, unit, mayCancel: _player is not null) is { } orders)
		{
			orders.CancelPressed += () => CancelOrder(unit);
			Add(orders);
		}
		// A headquarters has a row for the AI that may command it; a division has none.
		if (unit.Kind is EUnitKind.Theatre or EUnitKind.ArmyGroup or EUnitKind.Army or EUnitKind.Corps
			&& CUnitObjectivesEntry.Create(_context, unit) is { } objectives)
		{
			Add(objectives);
		}
		if (CUnitButtonsEntry.Create(_context, state, owner, unit, mayOrder) is { } buttons)
		{
			buttons.SupportChanged += (what, on) => SetSupport(unit, what, on);
			buttons.LoadPressed += fleet =>
			{
				// A selected unit put aboard leaves the selection to its fleet (transport.md, section 3).
				if (Post(new CLoadUnitCommand(owner.Tag, unit.Id, fleet), unit)
					&& owner.AllUnits().FirstOrDefault(other => other.Id == fleet) is { } carrier)
				{
					_boarding = (unit, carrier);
				}
			};
			buttons.UnloadPressed += () => Post(new CUnloadUnitCommand(owner.Tag, unit.Id), unit);
			buttons.PlaneLoadPressed += plane =>
			{
				// And one put aboard an air unit leaves it to the air unit (transport.md, section 13.3).
				if (Post(new CLoadOnPlaneCommand(owner.Tag, unit.Id, plane), unit)
					&& owner.AllUnits().FirstOrDefault(other => other.Id == plane) is { } carrier)
				{
					_boarding = (unit, carrier);
				}
			};
			buttons.PlaneUnloadPressed += () => Post(new CUnloadFromPlaneCommand(owner.Tag, unit.Id), unit);
			Add(buttons);
		}
		if (CUnitBrigadesEntry.Create(_context, state, owner, unit) is { } brigades)
		{
			Add(brigades);
		}
		_hasChildren = unit.Children.Count > 0;
		if (_hasChildren && CUnitChildrenEntry.Create(_context, state, owner, unit) is { } children)
		{
			children.Selected += ChooseRow;
			// The ribbon adds every land unit under this one to the selection, which keeps this one (selection.md, 5.3).
			children.SelectAllPressed += () => SelectionChanging?.Invoke(selection => selection.AddCommand(unit));
			Add(children);
		}
		// What a fleet or an air unit carries, on every one's panel (transport.md, section 12).
		if (unit.Kind is EUnitKind.Navy or EUnitKind.Air && CLoadedUnitsEntry.Create(_context, state, owner, unit) is { } loaded)
		{
			loaded.Selected += aboard =>
			{
				ChooseRow(aboard);
				ProvinceWanted?.Invoke(aboard.Location);
			};
			Add(loaded);
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

	/// <summary>
	/// A click on a parent's, a subordinate's or a passenger's row: that unit alone, and with Ctrl a land unit brings its
	/// whole command (selection.md, section 5.3). The map goes to it either way.
	/// </summary>
	private void ChooseRow(CUnit unit)
	{
		if (Input.IsKeyPressed(Key.Ctrl) && CMovementRules.IsLandUnit(unit))
		{
			SelectionChanging?.Invoke(selection =>
			{
				selection.Clear();
				selection.Force(unit);
				selection.AddCommand(unit);
			});
			ProvinceWanted?.Invoke(unit.Location);
			return;
		}
		UnitChosen?.Invoke(unit);
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

	/// <summary>A command of the player's own about one of its units, posted where it may run. Answers whether it was.</summary>
	private bool Post(CCommand command, CUnit unit)
	{
		if (_player is null || unit.Owner != _player.Tag || !command.MayExecute(_simulation.State))
		{
			return false;
		}
		_simulation.Post(command);
		Ordered?.Invoke();
		return true;
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
