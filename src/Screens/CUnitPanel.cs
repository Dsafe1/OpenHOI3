using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Agents;
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
	private bool _underAi;
	private readonly List<CUnit> _parents = new();
	private CUnit? _unit;
	private bool _hasChildren;
	private readonly List<CCombat> _combats = new();

	/// <summary>A unit the player put aboard a fleet, whose selection goes to the fleet once it is aboard.</summary>
	private (CUnit Unit, CUnit Fleet)? _boarding;

	/// <summary>An empty unit asked for beside a unit, and the last unit number taken before it.</summary>
	private (CUnit Unit, int After)? _creating;

	/// <summary>A unit being split, and the last unit number taken before it.</summary>
	private (CUnit Unit, int After)? _splitting;

	private CLeaderSelectionPanel? _leaders;
	private CMultiUnitPanel? _multi;
	private CConfirmDialog? _confirm;
	private CAttachmentPanel? _attachment;
	private (CConfirmDialog? Dialog, CUnit Unit)? _disband;

	/// <summary>Units given a new headquarters by the multi-unit panel, one of them and the last number taken before.</summary>
	private (CUnit Unit, int After)? _headquarters;

	/// <summary>
	/// The leader window's filter of traits, kept from one window to the next for each kind
	/// of leader for as long as the game is up, as the original keeps it in its game state.
	/// </summary>
	private readonly Dictionary<ELeaderType, List<CTrait?>> _keptTraitFilters = new();

	/// <summary>Raised when a row of the panel asks for another unit to be the one selected, or for none.</summary>
	public event Action<CUnit?>? UnitChosen;

	/// <summary>Raised when the row of a battle the unit is in is clicked.</summary>
	public event Action<CCombat>? CombatChosen;

	/// <summary>The order of battle button was pressed.</summary>
	public event Action? BrowserWanted;

	/// <summary>A brigade's row was clicked.</summary>
	public event Action<CSubUnit>? BrigadeChosen;

	/// <summary>The left button went down on a brigade's row.</summary>
	public event Action<CSubUnit>? BrigadeDragStarted;

	private const string UpgradeWindowName = "select_upgrade_window";
	private const string UpgradeRowName = "select_upgrade_entry";
	private CUnitBrigadeRow? _upgradeRow;

	/// <summary>
	/// A brigade's list of what it may become, one row a type and Cancel, beside its button. See
	/// <c>docs/mechanics/upgrades.md</c>, section 5.
	/// </summary>
	private void ShowUpgradeList(CUnitBrigadeRow row)
	{
		CloseUpgradeList();
		if (_player is null || row.SubUnit is not { } brigade || _window?.FindWindow(UpgradeWindowName) is not { } window
			|| window.FindListBox("list") is not { } list || row.UpgradeButton is not { } button)
		{
			return;
		}
		_upgradeRow = row;
		button.SetFrame(2);
		list.Clear();
		foreach (CSubUnitType target in CBrigadeUpgrades.Targets(_simulation.State, _player, brigade))
		{
			AddUpgradeRow(list, _context.Localisation.Localise(target.Key), target, brigade);
		}
		AddUpgradeRow(list, _context.Localisation.Localise("UPGRADE_CANCEL"), null, brigade);
		list.Visible = true;
		window.Visible = true;
		// The figure is a place on the screen and not inside the window; the last pixel is measured (upgrades.md, section 5).
		Vector2 offset = row.Window.GlobalPosition - _window.GlobalPosition;
		list.GlobalPosition = button.Position + offset + Vector2.Down;
	}

	private void AddUpgradeRow(CGuiListBox list, string text, CSubUnitType? target, CSubUnit brigade)
	{
		if (CGuiWindow.CreateEntry(_context, UpgradeRowName) is not { } entry || entry.TryFindButton("target") is not { } button)
		{
			return;
		}
		button.SetText(text);
		button.Pressed += () =>
		{
			if (target is not null && brigade.Unit is { } unit)
			{
				Post(new CUpgradeRegimentCommand(unit.Owner, brigade.Id, target.Key), unit);
			}
			CloseUpgradeList();
		};
		list.Add(entry);
	}

	private void CloseUpgradeList()
	{
		_upgradeRow?.UpgradeButton?.SetFrame(1);
		_upgradeRow = null;
		if (_window?.FindWindow(UpgradeWindowName) is { } window)
		{
			window.Visible = false;
		}
	}

	/// <summary>The confirmation of a disband, which goes by itself when the unit does (attaching.md, section 6).</summary>
	private void AskDisband(CUnit unit)
	{
		Func<string, string> localise = _context.Localisation.Localise;
		_disband = (CConfirmDialog.Open(this, _context, "DefaultConfirm", localise("CONFIRM_DISBAND_TITLE"),
			Interface.Text.CTextTemplate.Fill(localise("CONFIRM_DISBAND_DESC"), ("NAME", unit.Name))), unit);
		if (_disband?.Dialog is { } dialog)
		{
			dialog.Agreed += () => Post(new CDisbandCommand(unit.Id), unit);
		}
	}

	/// <summary>Each press makes the window anew, so a second press rebuilds it (attaching.md, section 2).</summary>
	private void OpenAttachment(CCountry owner, CUnit unit)
	{
		CloseAttachment();
		_attachment = CAttachmentPanel.Open(this, _context, _simulation, owner, unit);
	}

	private void CloseAttachment()
	{
		_attachment?.Close();
		_attachment = null;
	}

	/// <summary>A new headquarters asked for from several units is selected once it exists (attaching.md, section 4).</summary>
	private void FollowHeadquarters()
	{
		if (_headquarters is not ({ } unit, int after))
		{
			return;
		}
		if (unit.IsRemoved)
		{
			_headquarters = null;
		}
		else if (unit.Parent is { } made && made.Id > after)
		{
			_headquarters = null;
			SelectionChanging?.Invoke(selection => selection.Add(made, made.Owner));
		}
	}

	/// <summary>The panel stands this far from the screen's left, beside the order of battle browser.</summary>
	public void SetLeft(float x)
	{
		if (_window is not null)
		{
			_window.Position = new Vector2(x, _window.Position.Y);
		}
	}

	/// <summary>Raised when the panel asks for the map to show a province.</summary>
	public event Action<int>? ProvinceWanted;

	/// <summary>Raised when the panel has posted an order that changes where units stand on the map.</summary>
	public event Action? Ordered;

	/// <summary>The multi-unit panel's reorganise button: the two units, the first selected first.</summary>
	public event Action<CUnit, CUnit>? ReorganiseWanted;

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
		_multi.ReorganiseWanted += (left, right) => ReorganiseWanted?.Invoke(left, right);
		_multi.HeadquartersAsked += (unit, after) => _headquarters = (unit, after);
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
		// to, and the stances an AI may be given.
		foreach (string name in new[] { UpgradeWindowName, "sup_stance_change_window" })
		{
			if (_window.FindWindow(name) is { } child)
			{
				child.Visible = false;
			}
		}
		if (_window.FindButton("oob_button") is { } browser)
		{
			browser.Pressed += () => BrowserWanted?.Invoke();
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
		if (unit is null)
		{
			CloseAttachment();
		}
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
		FollowNewUnits();
		FollowHeadquarters();
		if (_disband is ({ IsOpen: true } dialog, { IsRemoved: true }))
		{
			dialog.Close();
		}
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
			|| !Battles(unit).SequenceEqual(_combats) || CAgents.HighestOver(unit) is not null != _underAi)
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
		// The window a leader is chosen in belongs to the header, which goes now, and the attachment window to the buttons.
		CloseLeaders();
		CloseAttachment();
		if (_list is null)
		{
			return;
		}
		CloseUpgradeList();
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
			&& CUnitObjectivesEntry.Create(_context, state, unit, command => Post(command, unit)) is { } objectives)
		{
			Add(objectives);
		}
		AddAiRows(state, unit);
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
			buttons.NewUnitPressed += split => NewUnit(unit, split);
			buttons.AttachPressed += () => OpenAttachment(owner, unit);
			buttons.DisbandPressed += () => AskDisband(unit);
			buttons.DetachPressed += () => Post(new CDetachUnitCommand(owner.Tag, unit.Id), unit);
			buttons.CreateHigherPressed += () => Post(new CCreateHigherCommand(owner.Tag, new[] { unit.Id }), unit);
			Add(buttons);
		}
		if (CUnitBrigadesEntry.Create(_context, state, owner, _player, unit) is { } brigades)
		{
			brigades.UpgradeWanted += ShowUpgradeList;
			brigades.BrigadeChosen += subUnit => BrigadeChosen?.Invoke(subUnit);
			brigades.BrigadeDragStarted += subUnit => BrigadeDragStarted?.Invoke(subUnit);
			Add(brigades);
		}
		_hasChildren = unit.Children.Count > 0;
		_underAi = CAgents.HighestOver(unit) is not null;
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

	/// <summary>The five rows of a headquarters under an AI, its own or one above it (unitpanel.md, section 9).</summary>
	private void AddAiRows(CGameState state, CUnit unit)
	{
		if (_window?.FindWindow("sup_stance_change_window") is not { } change)
		{
			return;
		}
		change.Visible = false;
		if (unit.Kind is not (EUnitKind.Theatre or EUnitKind.ArmyGroup or EUnitKind.Army or EUnitKind.Corps) || CAgents.HighestOver(unit) is null)
		{
			return;
		}
		foreach (EPlanStanceKind kind in new[] { EPlanStanceKind.Ground, EPlanStanceKind.Air, EPlanStanceKind.Naval })
		{
			if (CUnitStanceEntry.Create(_context, change, unit, kind, command => Post(command, unit)) is { } stance)
			{
				Add(stance);
			}
		}
		if (CPowerRatioEntry.Create(_context, unit) is { } power)
		{
			Add(power);
		}
		if (CDesiredAssetsEntry.Create(_context, state, unit) is { } desired)
		{
			Add(desired);
		}
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

	/// <summary>
	/// The new-unit button (unitpanel.md, section 7.1): with Ctrl, the unit split where it may be; without, an empty unit of
	/// its kind beside it - one at a time, a second press doing nothing until the first has come. What follows each is
	/// done once its units exist, in <see cref="FollowNewUnits"/>.
	/// </summary>
	private void NewUnit(CUnit unit, bool split)
	{
		int after = _simulation.State.LastUnitId;
		if (split)
		{
			if (CUnitSplit.MaySplit(unit) && Post(new CUnitSplitCommand(unit.Owner, unit.Id), unit))
			{
				_splitting = (unit, after);
			}
			return;
		}
		if (_creating is not null)
		{
			return;
		}
		EUnitKind kind = CMovementRules.IsLandUnit(unit) ? EUnitKind.Division : unit.Kind;
		if (Post(new CCreateUnitCommand(unit.Owner, 0, kind, unit.Location, unit.Base, 0, isAllowedEmpty: true), unit))
		{
			_creating = (unit, after);
		}
	}

	/// <summary>
	/// Once the units a press asked for exist: an empty unit is added to the selection, the reorganise window opened with
	/// the old unit on the left and the new on the right, and the new one put under the old one's commander where he may
	/// take the old one; a split's units are the selection, the old first.
	/// </summary>
	private void FollowNewUnits()
	{
		if (_player is null)
		{
			return;
		}
		if (_creating is ({ } old, int after))
		{
			CUnit? created = _player.AllUnits().FirstOrDefault(unit => unit.Id > after && !unit.IsRemoved
				&& unit.SubUnits.Count == 0 && unit.Location == old.Location && unit.Kind is EUnitKind.Division or EUnitKind.Navy
					or EUnitKind.Air);
			if (old.IsRemoved)
			{
				_creating = null;
			}
			else if (created is not null)
			{
				_creating = null;
				SelectionChanging?.Invoke(selection => selection.Add(created, created.Owner));
				ReorganiseWanted?.Invoke(old, created);
				if (old.Parent is { } parent && Sim.Construction.CDeployment.MayTakeSubordinate(parent, old))
				{
					Post(new CAttachUnitCommand(old.Owner, created.Id, parent.Id), old);
				}
			}
		}
		if (_splitting is ({ } unit, int since))
		{
			List<CUnit> made = _player.AllUnits().Where(other => other.Id > since && !other.IsRemoved && other.SubUnits.Count > 0
				&& other.Kind == unit.Kind).OrderBy(other => other.Id).ToList();
			if (unit.IsRemoved)
			{
				_splitting = null;
			}
			else if (made.Count > 0)
			{
				_splitting = null;
				SelectionChanging?.Invoke(selection =>
				{
					selection.Clear();
					selection.Add(unit, unit.Owner);
					foreach (CUnit other in made)
					{
						selection.Add(other, other.Owner);
					}
				});
			}
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
