using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The menu a finished unit is put on the map from: the original's
/// <c>sidebar_deploy</c>. Its list is the units waiting, until one is chosen; then it is
/// the units in the field that one may join, and the map takes a click on a province
/// it may be placed in. See <c>docs/mechanics/mapunits.md</c>, section 6. The buildings waiting are listed after the
/// units; one chosen colours the map, and a click where it may go places it (<c>buildinglines.md</c>, section 6).
/// </summary>
public partial class CDeployMenu : Control
{
	private const string WindowName = "sidebar_deploy";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;

	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private CGuiTextBox? _title;
	private readonly List<CUnit> _listed = new();
	private readonly List<CBuildingDeployment> _listedBuildings = new();

	private CUnit? _placing;
	private CBuildingDeployment? _placingBuilding;
	private bool _choosingTarget;
	private bool _targetsStale;
	private bool _listStale;

	/// <summary>
	/// Raised when the unit being placed changes: with the unit when a line is chosen,
	/// and with null when it has been placed or the menu closes.
	/// </summary>
	public event Action<CUnit?>? PlacingChanged;

	/// <summary>Raised when the menu goes up or comes down.</summary>
	public event Action? Toggled;

	/// <summary>
	/// Raised when the building being placed changes: with it when its line is chosen, with null when it has been placed
	/// or the menu closes (<c>docs/mechanics/buildinglines.md</c>, section 6).
	/// </summary>
	public event Action<CBuildingDeployment?>? BuildingPlacingChanged;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	/// <summary>The unit the map is waiting for a place for, or null.</summary>
	public CUnit? Placing => _placing;

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
		_window.MouseFilter = MouseFilterEnum.Ignore;
		_list = _window.FindListBox("deployment_queue");
		_title = _window.FindTextBox("title_view_deploy");
		if (_window.FindButton("close") is { } close)
		{
			close.Pressed += Close;
		}
	}

	public void Open()
	{
		if (_window is null || Visible)
		{
			return;
		}
		Visible = true;
		_choosingTarget = false;
		_targetsStale = false;
		_listStale = true;
		Refresh();
		Toggled?.Invoke();
	}

	public void Close()
	{
		if (!Visible)
		{
			return;
		}
		Visible = false;
		_targetsStale = false;
		SetPlacing(null);
		SetPlacingBuilding(null);
		Toggled?.Invoke();
	}

	/// <summary>
	/// The map's question while a building is being placed: where it may go, the click places it and answers yes;
	/// elsewhere it answers no and the click goes on as an ordinary one, the line staying chosen.
	/// </summary>
	public bool PlaceBuilding(int province)
	{
		if (_placingBuilding is not { } deployment || province <= 0
			|| !CBuildingLines.CanDeployIn(_simulation.State, _player, deployment.Building, _simulation.State.Province(province)))
		{
			return false;
		}
		_simulation.Post(new CDeployBuildingCommand(_player.Tag, deployment.Id, province));
		SetPlacingBuilding(null);
		_listStale = true;
		return true;
	}

	/// <summary>
	/// The map's answer: the unit being placed goes in that province, if it may. The
	/// order is the command's to refuse; the menu only stops waiting.
	/// </summary>
	public void PlaceIn(int province)
	{
		if (_placing is not { } unit)
		{
			return;
		}
		var command = new CDeployUnitCommand(_player.Tag, unit.Id, province);
		if (!command.MayExecute(_simulation.State))
		{
			return;
		}
		_simulation.Post(command);
		Placed();
	}

	public override void _Process(double delta)
	{
		if (Visible && _window is not null)
		{
			Refresh();
		}
	}

	/// <summary>Every frame while it is up, as the original's does.</summary>
	private void Refresh()
	{
		if (_list is null)
		{
			return;
		}
		if (_placing is not null && !_player.Deployments.Contains(_placing))
		{
			Placed();                                        // it went somewhere, or is no longer waiting
		}
		if (_placingBuilding is not null && !_player.BuildingDeployments.Contains(_placingBuilding))
		{
			SetPlacingBuilding(null);
		}

		if (!_choosingTarget)
		{
			if (_listStale || !_listed.SequenceEqual(_player.Deployments) || !_listedBuildings.SequenceEqual(_player.BuildingDeployments))
			{
				BuildList();
			}
			if (_player.Deployments.Count == 0 && _player.BuildingDeployments.Count == 0)
			{
				Close();                                     // an empty list closes itself
			}
			return;
		}

		if (_targetsStale && _placing is { } unit)
		{
			_targetsStale = false;
			if (!BuildTargets(unit))
			{
				// Nothing it may join: the plain list again, and the map still waits.
				_choosingTarget = false;
				_listStale = true;
			}
		}
	}

	private void BuildList()
	{
		_listStale = false;
		Set(_title, CDeployMenuText.Title(_context.Localisation.Localise, choosingTarget: false));
		_list!.Clear();
		_listed.Clear();
		foreach (CUnit unit in _player.Deployments)
		{
			_listed.Add(unit);
			if (CDeploymentEntry.Create(_context, unit) is { } line)
			{
				line.Selected += () => Choose(unit);
				_list.Add(line.Window);
			}
		}
		// The buildings after the units, each in the order it was finished (ours: the original keeps one list of both).
		_listedBuildings.Clear();
		foreach (CBuildingDeployment deployment in _player.BuildingDeployments)
		{
			_listedBuildings.Add(deployment);
			if (CDeploymentEntry.Create(_context, deployment) is { } line)
			{
				line.Selected += () => ChooseBuilding(deployment);
				_list.Add(line.Window);
			}
		}
	}

	/// <summary>A building's line chosen: the map shows where it may go, and the menu stays on its list.</summary>
	private void ChooseBuilding(CBuildingDeployment deployment)
	{
		SetPlacing(null);
		_choosingTarget = false;
		SetPlacingBuilding(deployment);
	}

	private void SetPlacingBuilding(CBuildingDeployment? deployment)
	{
		if (ReferenceEquals(deployment, _placingBuilding))
		{
			return;
		}
		_placingBuilding = deployment;
		BuildingPlacingChanged?.Invoke(deployment);
	}

	/// <summary>Lists the units the one being placed may join, and answers whether there is any.</summary>
	private bool BuildTargets(CUnit unit)
	{
		List<CUnit> targets = CDeployment.Targets(_simulation.State, _player, unit);
		if (targets.Count == 0)
		{
			return false;
		}
		Set(_title, CDeployMenuText.Title(_context.Localisation.Localise, choosingTarget: true));
		_list!.Clear();
		_listed.Clear();
		foreach (CUnit target in targets)
		{
			if (CDeploymentTargetEntry.Create(_context, _simulation.State, target) is { } line)
			{
				line.Selected += () => Join(unit, target);
				_list.Add(line.Window);
			}
		}
		return true;
	}

	private void Choose(CUnit unit)
	{
		SetPlacingBuilding(null);
		_choosingTarget = true;
		_targetsStale = true;
		SetPlacing(unit);
	}

	private void Join(CUnit unit, CUnit target)
	{
		var command = new CDeployUnitCommand(_player.Tag, unit.Id, 0, target.Id);
		if (command.MayExecute(_simulation.State))
		{
			_simulation.Post(command);
		}
		Placed();
	}

	/// <summary>The unit being placed has its place, or lost it: back to the list of those waiting.</summary>
	private void Placed()
	{
		_choosingTarget = false;
		_targetsStale = false;
		_listStale = true;
		SetPlacing(null);
	}

	private void SetPlacing(CUnit? unit)
	{
		if (ReferenceEquals(unit, _placing))
		{
			return;
		}
		_placing = unit;
		PlacingChanged?.Invoke(unit);
	}

	private static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null)
		{
			box.Text = text;
		}
	}
}
