using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Lua;
using OpenHOI3.Map;
using OpenHOI3.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3;

/// <summary>
/// One game on screen: the game state, the map it is played on, and the nodes that
/// show them.
///
/// A game is an object, not the process. Everything a game owns hangs off this node,
/// so freeing it ends the game and adding another starts a new one - which is all that
/// "new game", "load" and "exit to menu" will need when there is a menu to offer them.
/// </summary>
public partial class CSession : Node
{
	private CDirectorySettings _directories = null!;
	private CAiScripts? _aiScripts;
	private CMapView _mapView = null!;
	private CLocalisation _localisation = null!;
	private CSimulation _simulation = null!;
	private CTopBar? _topBar;
	private COutliner? _outliner;
	private CProductionView? _production;
	private CTechnologyView? _technology;
	private CDiplomacyView? _diplomacy;
	private CPoliticsView? _politics;
	private CEspionageView? _espionage;
	private CStrategicEffectsView? _strategicEffects;
	private CProvinceView? _provinceView;
	private CEventWindows? _events;
	private CDeployMenu? _deployMenu;
	private CAlerts? _alerts;
	private CUnitPanel? _unitPanel;
	private COrdersWindow? _orders;
	private CCombatWindow? _combat;
	private Label _provinceLabel = null!;
	private Debug.CDebugMenu? _debugMenu;
	private Debug.CFrameLog? _frameLog;
	private int _shownProvince;
	private bool _unitsMoved;
	private bool _hourPassed;
	private bool _ordered;
	private bool _wasMoving;
	private long _whereabouts;
	private int _waiting;
	private int _controlChanges;
	private long _dayColoured;
	private long _airMissions;

	/// <summary>
	/// The recording made with <c>--record=</c> or played back with <c>--replay=</c>, and whether a playback has run out.
	/// </summary>
	private CReplayFile? _replayFile;
	private bool _replayEnded;

	/// <summary>The country played when nothing says which. There is no screen to choose one on yet.</summary>
	private const string DefaultPlayer = "GER";

	/// <summary>Where the province readout stands: at the screen's edge, and clear of the unit panel when that is up.</summary>
	private const float ReadoutAtEdge = 16f;
	private const float ReadoutBesidePanel = 420f;

	public CGameState State { get; private set; } = null!;

	/// <summary>The country the interface is about, or null when the install has none of that tag.</summary>
	public CCountry? Player { get; private set; }

	public CMap Map { get; private set; } = null!;

	/// <summary>The player's settings, read as the session starts: <c>docs/mechanics/settings.md</c>.</summary>
	public Content.Settings.CUserSettings Settings { get; private set; } = Content.Settings.CUserSettings.Default;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CDirectorySettings directories)
	{
		_directories = directories;
	}

	public override void _Ready()
	{
		// Loading is done here, on the main thread, and holds the first frame for as
		// long as it takes - about a second and a half for the base game. That is
		// acceptable while there is nothing else to show, and not once there is a
		// loading screen to keep moving.
		var clock = Stopwatch.StartNew();
		// The player's settings, from the original's own settings.txt in the Documents folder (settings.md); what of them
		// is not supported is reported as it is read.
		Settings = Content.Settings.CUserSettingsReader.Read(Content.Settings.CUserSettingsReader.DefaultPath());
		CLocalisation localisation = CLocalisation.Load(_directories);
		_localisation = localisation;
		CCountries countries = CCountries.Load(_directories, localisation);
		Map = CMap.Load(_directories);
		// The install's Lua lives as long as the session: a new game asks it for its relations.
		_aiScripts = new CAiScripts(_directories);
		// Who plays is known before the game is made: the AI's first look at the laws leaves that country alone. A game
		// replaying a recording is played by whoever played it when it was recorded, which its header says
		// (docs/replay.md, sections 6 and 8).
		string? replayFrom = Argument("--replay=");
		string? recordTo = Argument("--record=");
		string asked = PlayerArgument() ?? DefaultPlayer;
		IReadOnlyList<CCountryTag> played;
		if (replayFrom is not null)
		{
			played = CReplayFile.ReadHeader(replayFrom).Played;
			if (PlayerArgument() is not null)
			{
				GD.Print($"--replay: the recording says who plays, so --country={asked} is left aside.");
			}
		}
		else
		{
			if (countries.Find(asked) is null)
			{
				GD.Print($"--country: there is no country '{asked}', so {DefaultPlayer} is played.");
				asked = DefaultPlayer;
			}
			played = new[] { new CCountryTag(asked) };
		}
		State = CGameStateLoader.LoadStart(_directories, Map, localisation, countries, aiScripts: _aiScripts, played: played);
		GD.Print($"Loaded {Map.ProvinceCount - 1} provinces and {countries.Count} countries " +
			$"for {State.Date} in {clock.ElapsedMilliseconds} ms.");

		_simulation = new CSimulation(State);
		// The simulation asks who is played by a person in one place, the state's list: an idle fleet of anybody
		// else's starts no battle. A replay of a game nobody played shows the map alone.
		Player = played.Count > 0 ? State.Country(played[0]) : null;
		// The recording takes the AI's place, and the players', from the first hour, entering the game having run as it did
		// when recorded; or the game is recorded from then. A replay takes no order of the player's: its screens look on.
		if (replayFrom is not null)
		{
			_replayFile = CReplayFile.Open(replayFrom, State);
			State.Replay = CReplay.Replaying(_replayFile);
			GD.Print($"Replaying {replayFrom}, " + (Player is null ? "nobody playing." : $"{Player.Tag} looked on and given no orders."));
		}
		else if (recordTo is not null)
		{
			_replayFile = CReplayFile.Create(recordTo, new CReplayHeader(played, State.Seed));
			State.Replay = CReplay.Recording(_replayFile.Write);
			GD.Print($"Recording to {recordTo}, {Player?.Tag} playing.");
		}

		clock.Restart();
		CGuiContext gui = CGuiContext.Load(_directories, localisation);
		GD.Print($"Loaded {gui.Types.Count} interface elements and {gui.Sprites.Count} sprites " +
			$"in {clock.ElapsedMilliseconds} ms.");

		_mapView = new CMapView { Name = "Map" };
		_mapView.Setup(Map, State);
		_mapView.Player = Player?.Tag;
		_mapView.SetupCounters(gui, CCounterTypes.Load(_directories), CBillboardTypes.Load(_directories),
			CArrowTypes.Load(_directories));
		AddChild(_mapView);

		BuildHud(gui);
		// The readout is of the province selected, not of the one under the mouse: it is
		// long, and following the mouse it jumped about.
		_mapView.ProvinceSelected += ShowProvince;

		RunDeveloperArguments();
	}

	/// <summary>
	/// The game's own interface, from the original's interface files: so far the top
	/// bar. Under it a province readout in a plain engine label, which is scaffolding
	/// and goes when the province view comes.
	/// </summary>
	private void BuildHud(CGuiContext gui)
	{
		var layer = new CanvasLayer { Name = "Hud" };
		AddChild(layer);
		bool looksOn = !_simulation.TakesCommands;
		_mapView.LooksOn = looksOn;

		if (Player is not null)
		{
			// The screens go under the top bar, which is drawn over whatever is open.
			_production = new CProductionView();
			_production.Setup(gui, _simulation, Player);
			layer.AddChild(_production);
			_technology = new CTechnologyView();
			_technology.Setup(gui, _simulation, Player);
			layer.AddChild(_technology);
			_diplomacy = new CDiplomacyView();
			_diplomacy.Setup(gui, _simulation, Player);
			CMapView mapView = _mapView;
			_diplomacy.SelectedProvince = () => mapView.SelectedProvince;
			layer.AddChild(_diplomacy);
			_politics = new CPoliticsView();
			_politics.Setup(gui, _simulation, Player);
			layer.AddChild(_politics);
			_espionage = new CEspionageView();
			_espionage.Setup(gui, _simulation, Player);
			_espionage.SelectedProvince = () => mapView.SelectedProvince;
			layer.AddChild(_espionage);

			// The deploy menu and the alert that opens it. A unit chosen there is placed
			// by a click on the map, which is shown where it may go.
			_deployMenu = new CDeployMenu();
			_deployMenu.Setup(gui, _simulation, Player);
			layer.AddChild(_deployMenu);
			_deployMenu.PlacingChanged += unit => _mapView.ShowPlaces(
				unit is null ? null : CDeployment.Places(State, Player, unit));
			_deployMenu.Toggled += () => ShowProvince(_shownProvince);
			_mapView.ProvinceSelected += province => _deployMenu.PlaceIn(province);
			// A building waiting is placed by a click where the map shows green (buildinglines.md, section 6).
			_deployMenu.BuildingPlacingChanged += deployment => _mapView.ShowDeployment(deployment is null ? null
				: province => Interface.Map.CDeploymentColours.Of(State, Player, deployment.Building, province));
			_mapView.PlaceBuilding = province => _deployMenu.PlaceBuilding(province);
		}

		// What follows is built in a replay of a game nobody played too, which looks on through each subject's own
		// country: a province through its controller's eyes, a unit through its owner's, and nothing pressed does anything
		// (docs/replay.md, section 6). A replay of a played game looks on through the player's screens; its unit panel and
		// battle window are given no player, so that they offer nothing to press.
		CCountry? actor = looksOn ? null : Player;

		// The province view, up while provinces are selected and about the last chosen; a click that places
		// something selects nothing for it (provinceview.md, section 1).
		_provinceView = new CProvinceView();
		_provinceView.Setup(gui, _simulation, Player);
		layer.AddChild(_provinceView);
		_mapView.ProvinceSelected += ShowProvinceView;
		_provinceView.CloseRequested += () => _mapView.ClearSelection();
		_provinceView.FlagPressed += FlagPressed;

		if (Player is not null)
		{
			_alerts = new CAlerts();
			_alerts.Setup(gui, Player);
			layer.AddChild(_alerts);
			_alerts.DeployablePressed += ToggleDeployMenu;
		}

		// The panel of the unit selected on the map. Its rows may choose another unit -
		// one above it, one under it - and the map then goes to where that one is, as
		// the original's does.
		_unitPanel = new CUnitPanel();
		_unitPanel.Setup(gui, _simulation, actor);
		layer.AddChild(_unitPanel);
		_unitPanel.UnitChosen += ChooseUnit;
		_unitPanel.ProvinceWanted += province => _mapView.CentreOn(province);
		_unitPanel.Ordered += () => _ordered = true;
		_unitPanel.SelectionChanging += change => _mapView.ChangeUnits(change);
		if (_deployMenu is not null)
		{
			_deployMenu.Toggled += ShowSelectedUnit;
		}

		if (Player is not null)
		{
			// The window of orders a right click with a fleet or an air unit brings up.
			_orders = new COrdersWindow();
			_orders.Setup(gui, _simulation, Player);
			_orders.Selection = () => _mapView.Units.Units;
			layer.AddChild(_orders);
			_orders.Ordered += () => _ordered = true;
			_orders.Toggled += () => ShowProvince(_shownProvince);
			_orders.AreaShown += colours => _mapView.ShowAirArea(colours);
		}

		// The window of a battle, which a click on the battle's marker or on its row
		// in a unit's panel brings up. It stands where the unit panel does, and a
		// battle is selected in a unit's place: so the unit is let go.
		_combat = new CCombatWindow();
		_combat.Setup(gui, _simulation, actor);
		layer.AddChild(_combat);
		_combat.Toggled += () => ShowProvince(_shownProvince);
		_combat.RetreatPressed += Retreat;
		_unitPanel.CombatChosen += ShowCombat;
		_mapView.CombatSelected += ShowCombat;

		// With nobody played the bar has no country, and its buttons no screens to open.
		_topBar = new CTopBar();
		_topBar.Setup(gui, _simulation, Player);
		layer.AddChild(_topBar);
		_topBar.ProductionPressed += () => Toggle(_production);
		_topBar.TechnologyPressed += () => Toggle(_technology);
		_topBar.DiplomacyPressed += () => Toggle(_diplomacy);
		_topBar.PoliticsPressed += () => Toggle(_politics);
		_topBar.IntelligencePressed += () => Toggle(_espionage);

		// The outliner down the right, with the top bar's button for it (docs/mechanics/outliner.md).
		if (Player is not null)
		{
			_outliner = new COutliner();
			_outliner.Setup(gui, _simulation, Player, Settings.Outliner);
			layer.AddChild(_outliner);
			// Under the top bar, whose button for it stands over the outliner's top strip and has to keep its clicks.
			layer.MoveChild(_outliner, _topBar.GetIndex());
			COutliner outliner = _outliner;
			CTopBar topBar = _topBar;
			_topBar.OutlinerPressed += outliner.Toggle;
			outliner.Toggled += () => topBar.ShowOutliner(outliner.IsOpen, !outliner.IsCovered);
			topBar.ShowOutliner(outliner.IsOpen, !outliner.IsCovered);
			outliner.UnitChosen += ChooseUnit;
			outliner.CombatChosen += combat =>
			{
				ShowCombat(combat);
				_mapView.CentreOn(combat.Province);
			};
			outliner.ProvinceChosen += province => _mapView.CentreOn(province);
		}

		if (Player is not null)
		{
			// The offers other countries make the player, as icons hanging from the top bar - over it,
			// since the icons reach up into its window.
			var offers = new CDiploOffers();
			offers.Setup(gui, _simulation, Player.Tag);
			layer.AddChild(offers);

			// An event's windows and the message popups come up over the map and the panels, and under the four
			// screens the top bar opens - the original's, by the maintainer's account on 2026-10-14
			// (docs/mechanics/countryviews.md).
			_events = new CEventWindows();
			_events.Setup(gui, _simulation, Player);
			layer.AddChild(_events);
			layer.MoveChild(_events, _production!.GetIndex());

			// Messages: the log on the bottom bar, and the popups beside the events.
			var messageLog = new CMessageLogView();
			messageLog.Setup(gui);
			layer.AddChild(messageLog);
			layer.MoveChild(messageLog, 0);
			// The Strategic Effects window, which the bar's button by the minimap opens (docs/mechanics/strategiceffects.md).
			_strategicEffects = new CStrategicEffectsView();
			_strategicEffects.Setup(gui, _simulation, Player);
			layer.AddChild(_strategicEffects);
			layer.MoveChild(_strategicEffects, _production.GetIndex());
			messageLog.ModifiersPressed += _strategicEffects.Toggle;
			var popups = new CMessagePopups();
			popups.Setup(gui, _simulation);
			layer.AddChild(popups);
			layer.MoveChild(popups, _production.GetIndex());
			CCountryTag player = Player.Tag;
			void Tell(CMessage message)
			{
				if (message.Type.Log)
				{
					messageLog.Add(message);
				}
				if (message.Type.PopsUp)
				{
					popups.Open(message);
				}
				// The original stops the clock of a game of one player; this is one.
				if (message.Type.PausePopup)
				{
					_simulation.Clock.Paused = true;
				}
			}
			_simulation.State.EventAnswered += (answered, scope, option) => Tell(CMessages.EventAnswered(
				gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, answered, scope, option));
			_simulation.State.DecisionTaken += (taken, scope) => Tell(CMessages.DecisionTaken(
				gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, taken, scope));
			_simulation.State.PactMessaged += pact =>
			{
				if (CMessages.Pact(gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, pact) is { } message)
				{
					Tell(message);
				}
			};
		}
		_simulation.DayPassed += () => _unitsMoved = true;
		_simulation.HourPassed += () => _hourPassed = true;
		_mapView.UnitSelected += unit =>
		{
			_orders?.Close();                                   // it was for the unit selected before
			if (unit != 0)
			{
				_combat?.Close();                               // a unit is selected in the battle's place
			}
			ShowSelectedUnit();
			ShowProvince(_shownProvince);
		};
		_mapView.UnitsOrdered += OrderUnits;
		_mapView.GroupStored += StoreGroup;
		_mapView.BoxReleased += () =>
		{
			if (_deployMenu is { IsOpen: true })
			{
				_deployMenu.Close();                             // a box shuts the side menus and leaves them shut
			}
		};

		_provinceLabel = new Label
		{
			Position = new Vector2(16, 96),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_provinceLabel.AddThemeFontSizeOverride("font_size", 18);
		_provinceLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_provinceLabel.AddThemeConstantOverride("outline_size", 6);
		layer.AddChild(_provinceLabel);

		// The developer switches, on Shift and 4. The readout of the selected province, scaffolding with the supply and
		// the country's figures nothing else shows, is one of them and starts put away: the province view is up.
		_provinceLabel.Visible = false;
		_debugMenu = new Debug.CDebugMenu();
		layer.AddChild(_debugMenu);
		_debugMenu.AddSwitch("Province readout", false, on => _provinceLabel.Visible = on);

		// Every tooltip, over everything else.
		var tooltips = new CTooltips();
		tooltips.Setup(gui);
		tooltips.MapTooltip = _ => ProvinceTooltip(_mapView.HoveredProvince);
		AddChild(tooltips);
	}

	/// <summary>The tooltip of a province under the mouse, in the map mode shown: <c>docs/mechanics/tooltips.md</c>, section 7.</summary>
	private CTooltip? ProvinceTooltip(int provinceId)
	{
		if (provinceId <= 0 || provinceId >= State.Provinces.Count)
		{
			return null;
		}
		EMapTooltipMode mode = _mapView.Mode switch
		{
			CMapView.MapMode.Terrain => EMapTooltipMode.Terrain,
			CMapView.MapMode.Supply => EMapTooltipMode.Supply,
			CMapView.MapMode.Air => EMapTooltipMode.Air,
			CMapView.MapMode.Revolt => EMapTooltipMode.Revolt,
			CMapView.MapMode.Weather => EMapTooltipMode.Weather,
			_ => EMapTooltipMode.Political,
		};
		return CProvinceTooltip.Build(_localisation.Localise, State, State.Province(provinceId), mode, Player?.Tag,
			_mapView.Units.First, _mapView.SelectedProvince, () => CSupplyNetworks.Refresh(State));
	}

	/// <summary>
	/// A top bar button brings its screen up, or takes it down if it is up already. One
	/// screen at a time: the one that was up comes down. That the original does the
	/// same was not read; it is how the game is remembered.
	/// </summary>
	/// <summary>
	/// The province view on the province the map shows, with every province selected; hidden with none, and left as it
	/// was by a click that places something. A province nobody simulates opens nothing (provinceview.md, section 1).
	/// </summary>
	private void ShowProvinceView(int provinceId)
	{
		if (_provinceView is null || _mapView.IsPlacing)
		{
			return;
		}
		if (provinceId <= 0)
		{
			_provinceView.Hide();
			return;
		}
		var selected = new List<CProvince>();
		foreach (int id in _mapView.SelectedProvinces)
		{
			selected.Add(State.Province(id));
		}
		_provinceView.Show(State.Province(provinceId), selected);
	}

	/// <summary>
	/// A flag in the province view: the owner's opens the diplomacy screen; the occupier's first selects its capital,
	/// which moves the view there (provinceview.md, section 2).
	/// </summary>
	private void FlagPressed(CCountryTag country, bool occupier)
	{
		if (occupier && State.Country(country) is { Capital: > 0 } holder)
		{
			_mapView.SelectUnit(null);
			_mapView.Select(holder.Capital);
		}
		if (_diplomacy is { IsOpen: false })
		{
			Toggle(_diplomacy);
		}
	}

	private void Toggle(CCountryView? view)
	{
		if (view is null)
		{
			return;
		}
		if (view.IsOpen)
		{
			view.Close();
			return;
		}
		foreach (CCountryView? other in new CCountryView?[] { _production, _technology, _diplomacy, _politics, _espionage })
		{
			if (!ReferenceEquals(other, view))
			{
				other?.Close();
			}
		}
		view.Open();
	}

	/// <summary>
	/// The unit panel is up for the unit selected on the map, and gives way to the
	/// deploy menu, which stands in the same place.
	/// </summary>
	private void ShowSelectedUnit()
	{
		_unitPanel?.ShowSelection(_deployMenu is { IsOpen: true } ? System.Array.Empty<CUnit>() : _mapView.Units.Units);
	}

	/// <summary>Ctrl and a digit: the selected units become the group, by a command (selection.md, section 7).</summary>
	private void StoreGroup(int group, IReadOnlyList<CUnit> units)
	{
		if (Player is null)
		{
			return;
		}
		var command = new CSetSelectionGroupCommand(Player.Tag, group, units.Select(unit => unit.Id));
		if (command.MayExecute(State))
		{
			_simulation.Post(command);
		}
	}

	/// <summary>
	/// A battle was chosen, by its marker or by its row in a unit's panel: the unit
	/// selected is let go and the battle's window comes up. The map stays where it is -
	/// the maintainer's word, 2026-10-09 - where the original's row in a unit's panel
	/// takes it to the province fought over.
	/// </summary>
	private void ShowCombat(CCombat combat)
	{
		if (_combat is null)
		{
			return;
		}
		_mapView.SelectUnit(null);
		_combat.Open(combat);
	}

	/// <summary>
	/// The battle window's retreat button: every unit of the player's in the battle is
	/// told to stop. One that is attacking breaks its attack off; one that is not on the
	/// march has nothing to stop.
	/// </summary>
	private void Retreat(CCombat combat)
	{
		if (Player is null)
		{
			return;
		}
		var units = new List<CUnit>(combat.Attacker.Units);
		units.AddRange(combat.Defender.Units);
		foreach (CUnit unit in units)
		{
			if (unit.Owner != Player.Tag)
			{
				continue;
			}
			// A fleet is not halted out of a battle: it is set breaking away.
			if (combat.Kind == ECombatKind.Naval)
			{
				var breakAway = new CBreakAwayCommand(Player.Tag, unit.Id);
				if (breakAway.MayExecute(State))
				{
					_simulation.Post(breakAway);
					_ordered = true;
				}
				continue;
			}
			// An air unit is sent where a beaten one goes, out of a battle or a bombing.
			if (unit.Kind == EUnitKind.Air)
			{
				var fallBack = new CFallBackCommand(Player.Tag, unit.Id);
				if (fallBack.MayExecute(State))
				{
					_simulation.Post(fallBack);
					_ordered = true;
				}
				continue;
			}
			if (!unit.IsMoving)
			{
				continue;
			}
			var command = new CCancelMovementCommand(Player.Tag, unit.Id);
			if (command.MayExecute(State))
			{
				_simulation.Post(command);
				_ordered = true;
			}
		}
	}

	/// <summary>A row of the unit panel chose a unit, or none: it is selected, and the map shows where it is.</summary>
	private void ChooseUnit(CUnit? unit)
	{
		_mapView.SelectUnit(unit);
		if (unit is { Location: > 0 })
		{
			_mapView.CentreOn(unit.Location);
		}
	}

	/// <summary>
	/// The alert for units waiting brings the deploy menu up, or takes it down if it is
	/// up. The original closes whichever side menu is open first; this is the only one.
	/// </summary>
	private void ToggleDeployMenu()
	{
		if (_deployMenu is null)
		{
			return;
		}
		if (_deployMenu.IsOpen)
		{
			_deployMenu.Close();
		}
		else
		{
			_deployMenu.Open();
		}
	}

	/// <summary>The session's Lua goes with it.</summary>
	public override void _ExitTree()
	{
		_aiScripts?.Dispose();
		_aiScripts = null;
		_replayFile?.Dispose();
		_replayFile = null;
	}

	/// <summary>
	/// Feeds the frame's length to the game. Whether any hours come of it is the
	/// clock's decision - none while paused, as a new game is.
	/// </summary>
	public override void _Process(double delta)
	{
		long frameStart = Stopwatch.GetTimestamp();
		double simulation = 0;
		int hours = 0;
		// The readout is refreshed once a frame and not once an hour: at the top speed a
		// frame holds thousands of hours. A replay that has run out of its recording goes no further.
		if (_replayEnded)
		{
			_simulation.Clock.Paused = true;
		}
		else
		{
			hours = AdvanceUnlessTheReplayEnds(delta);
			simulation = Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds;
			if (hours > 0)
			{
				ShowProvince(_shownProvince);                       // its figures move with the days
			}
		}
		ProcessAfterHours();
		if (_frameLog is { } log)
		{
			log.Frame(delta, simulation, Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds - simulation, hours);
			if (log.IsDone)
			{
				log.WriteSummary();
				_frameLog = null;
				GetTree().Quit();
			}
		}
	}

	/// <summary>What a frame does after the game's hours: the counters, the map's colours and the province shown.</summary>
	private void ProcessAfterHours()
	{
		// The outliner steps aside while a country screen is up (outliner.md, section 1).
		_outliner?.SetCovered(new CCountryView?[] { _production, _technology, _diplomacy, _politics, _espionage }
			.Any(view => view is { IsOpen: true }));

		// The counters are worked out again when a day has gone by, a unit of the
		// player's has been placed or ordered, or an hour has gone by with a unit on the
		// march - and once a frame at most.
		int waiting = Player?.Deployments.Count ?? 0;
		// The weather's pictures follow the hour; its colours do not (weather.md, section 6).
		if (_hourPassed)
		{
			_mapView.Counters?.RefreshWeather();
		}
		if (_ordered || _hourPassed)
		{
			// The hour a unit arrives it is on the march no longer, and that hour is the
			// one its counter has to change province in: so whether one was moving before
			// counts as much as whether one is now.
			// A battle wears its units down hour by hour, and its last hour sends them
			// away: so a battle counts as movement does.
			// And a unit that is given a way and gets to its end inside one hour is on the
			// march neither before nor after it: so where every unit is counts too.
			bool moving = AnyUnitMoving() || State.Combats.Count > 0;
			long whereabouts = Whereabouts();
			if (_ordered || moving || _wasMoving || whereabouts != _whereabouts)
			{
				_unitsMoved = true;
			}
			_wasMoving = moving;
			_whereabouts = whereabouts;
		}
		_ordered = false;
		_hourPassed = false;
		// The map is coloured by who holds what, so it is coloured again when that changes.
		if (State.ControlChanges != _controlChanges)
		{
			_controlChanges = State.ControlChanges;
			_mapView.RefreshColours();
			_unitsMoved = true;
		}
		// The supply map mode is coloured by the day's carrying, so again each day.
		long day = State.TotalHours / CGameState.HoursPerDay;
		if (day != _dayColoured)
		{
			_dayColoured = day;
			// And the revolt map mode by the day's revolt risk (ours: when the original colours it again was not read).
			if (_mapView.Mode is CMapView.MapMode.Supply or CMapView.MapMode.Revolt)
			{
				_mapView.RefreshColours();
			}
			// The air map mode's rings, pictures and icons are made again each day.
			_mapView.RefreshAirMap();
		}
		// Its colours, when one of the player's fighter missions has started or gone.
		if (_mapView.Mode == CMapView.MapMode.Air)
		{
			long missions = CAirMapColours.Figure(State, Player?.Tag);
			if (missions != _airMissions)
			{
				_airMissions = missions;
				_mapView.RefreshColours();
			}
		}
		if (_unitsMoved || waiting != _waiting)
		{
			_unitsMoved = false;
			_waiting = waiting;
			_mapView.Counters?.Refresh();
			ShowProvince(_shownProvince);
		}
	}

	/// <summary>A figure that changes when any unit is in another province than it was.</summary>
	private long Whereabouts()
	{
		long figure = 17;
		foreach (CCountry country in State.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				figure = unchecked(figure * 31 + unit.Id * 7919L + unit.Location);
			}
		}
		return figure;
	}

	private bool AnyUnitMoving()
	{
		foreach (CCountry country in State.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IsMoving)
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// A right click on the map with units selected: each is given it in turn, in the order selected. A fleet or an
	/// air unit that would bring the orders window up finds it up already for the first and does nothing, so without
	/// Shift several of them move nobody (selection.md, section 3).
	/// </summary>
	private void OrderUnits(IReadOnlyList<CUnit> units, int province, bool append)
	{
		if (Player is null)
		{
			return;
		}
		// While a window of orders is up the click is the window's: the air one aims or
		// moves its area with it, the naval one does nothing.
		if (_orders is { IsOpen: true })
		{
			_orders.MapRightClick(province, append);
			return;
		}
		foreach (CUnit unit in units)
		{
			if (_orders is { IsOpen: true })
			{
				continue;
			}
			OrderUnit(unit, province, append);
		}
	}

	/// <summary>
	/// The right click for one unit. Only the player's own units take it. Sent to where it already stands a unit is
	/// halted - unless the order is to be added to a path it has, when it is one more leg of that.
	/// </summary>
	private void OrderUnit(CUnit unit, int province, bool append)
	{
		if (Player is null || unit.Owner != Player.Tag)
		{
			return;
		}
		int unitId = unit.Id;
		CCommand command;
		if (unit.Kind == EUnitKind.Air)
		{
			// An air unit is always asked what it is to do there.
			_orders?.Open(unit, province);
			return;
		}
		if (unit.Kind == EUnitKind.Navy)
		{
			// A fleet where it is, while under way, halts. Otherwise a plain click asks
			// what it is to do there, and one with shift sends it straight there.
			if (unit.IsMoving && province == unit.Location)
			{
				command = new CCancelMovementCommand(Player.Tag, unitId);
			}
			else if (!append && !unit.IsFighting)
			{
				// A coast the sea does not reach opens the window too where what the fleet
				// carries may land there (transport.md, section 12).
				if (CMovementRules.SeaEnd(State, State.Province(province)) is null
					&& !CTransportText.OpensOrdersOnCoast(State, Player, unit, State.Province(province)))
				{
					GD.Print($"{unit.Name} cannot reach {State.Province(province).Name} by sea.");
					return;
				}
				_orders?.Open(unit, province);
				return;
			}
			else
			{
				// With shift, or out of a battle, it is simply sent - and a fleet in a
				// battle then counts four hours before it goes.
				command = new CMoveUnitCommand(Player.Tag, unitId, province, append);
			}
		}
		else
		{
			command = province == unit.Location && (!append || !unit.IsMoving)
				? new CCancelMovementCommand(Player.Tag, unitId)
				: new CMoveUnitCommand(Player.Tag, unitId, province, append);
		}
		if (!command.MayExecute(State))
		{
			GD.Print($"{unit.Name} may not go to {State.Province(province).Name}.");
			return;
		}
		_simulation.Post(command);
		_ordered = true;
	}

	/// <summary>Space or Pause stops and starts time; plus and minus change its speed.</summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}
		if (Debug.CDebugMenu.IsMenuKey(key))
		{
			_debugMenu?.Toggle();
			return;
		}
		switch (key.Keycode)
		{
			case Key.Space or Key.Pause:
				_simulation.Clock.Paused = !_simulation.Clock.Paused;
				break;
			case Key.KpAdd or Key.Plus or Key.Equal:
				_simulation.Clock.Speed++;
				break;
			case Key.KpSubtract or Key.Minus:
				_simulation.Clock.Speed--;
				break;
			case Key.H:
				// The original's key for its render_hierarchy setting.
				if (_mapView.Counters is { } counters)
				{
					counters.HierarchyShown = !counters.HierarchyShown;
				}
				break;
			default:
				return;
		}
	}

	/// <summary>
	/// The switches given after <c>--</c> on the command line, one each. A launch that
	/// asks for them in one box hands all of them over as a single argument, so an
	/// argument is cut wherever a space is followed by <c>--</c> - and nowhere else,
	/// which leaves a path with a space in it whole.
	/// </summary>
	private static IEnumerable<string> UserArguments()
	{
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			foreach (string one in System.Text.RegularExpressions.Regex.Split(argument.Trim(), @"\s+(?=--)"))
			{
				if (one.Length > 0)
				{
					yield return one;
				}
			}
		}
	}

	/// <summary>
	/// The frame's hours, but where a replay reaches the end of its recording: the clock is stopped there for good and
	/// that is said, the hour it ended in being left part run (docs/replay.md, section 6).
	/// </summary>
	private int AdvanceUnlessTheReplayEnds(double delta)
	{
		int hours = 1;
		UnlessTheReplayEnds(() => hours = _simulation.Advance(delta));
		return hours;
	}

	/// <summary>
	/// Runs hours some way - a frame's, or a developer switch's - unless a replay has run out of its recording, and
	/// answers whether it may go on. Where it runs out, the clock is stopped for good and that is said.
	/// </summary>
	private bool UnlessTheReplayEnds(System.Action run)
	{
		if (_replayEnded)
		{
			return false;
		}
		try
		{
			run();
			return true;
		}
		catch (CReplayEndedException ended)
		{
			_replayEnded = true;
			_simulation.Clock.Paused = true;
			GD.Print($"--replay: {ended.Message}; the clock stops here.");
			return false;
		}
	}

	/// <summary>What follows a switch on the command line - the recording of <c>--replay=</c> or <c>--record=</c> - or null.</summary>
	private static string? Argument(string switchName)
	{
		foreach (string argument in UserArguments())
		{
			if (argument.StartsWith(switchName) && argument.Length > switchName.Length)
			{
				return argument[switchName.Length..];
			}
		}
		return null;
	}

	/// <summary>
	/// The tag given with <c>--country=</c> on the command line, or null. A tag is upper
	/// case in every file, so one typed in lower case is taken for the same country.
	/// </summary>
	private static string? PlayerArgument()
	{
		foreach (string argument in UserArguments())
		{
			if (argument.StartsWith("--country=")
				&& CCountryTag.TryParse(argument["--country=".Length..].ToUpperInvariant(), out CCountryTag tag))
			{
				return tag.Value;
			}
		}
		return null;
	}

	private void ShowProvince(int provinceId)
	{
		_shownProvince = provinceId;
		// The readout stands where the deploy menu does, and gives way to it - and to
		// the window of orders, which comes up over it.
		if (provinceId == 0 || _deployMenu is { IsOpen: true } || _orders is { IsOpen: true })
		{
			_provinceLabel.Text = "";
			return;
		}

		// With the unit panel up the readout stands beside it.
		bool panelUp = _unitPanel is { IsOpen: true } || _combat is { IsOpen: true };
		_provinceLabel.Position = new Vector2(panelUp ? ReadoutBesidePanel : ReadoutAtEdge, 96);

		CProvince province = State.Province(provinceId);
		string text = $"{province.Name}  ({province.Id})";
		// What the panel does not say yet, the original keeping it for a tooltip: where
		// the selected unit's way ends, and when it will be there.
		if (_mapView.Units.First is { IsMoving: true } selected && State.Country(selected.Owner) is { } mover)
		{
			long hours = CMovement.HoursToArrive(State, mover, selected);
			text = $"{selected.Name} to {State.Province(selected.Path[^1]).Name}"
				+ (hours < 0 ? "" : $", there in {hours / 24} days {hours % 24} hours") + "\n" + text;
		}
		var here = new System.Collections.Generic.List<string>();
		foreach (CCountry country in State.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Location == provinceId)
				{
					here.Add(unit.Name);
				}
			}
		}
		if (here.Count > 0)
		{
			text += "\nUnits: " + string.Join(", ", here.Take(6)) + (here.Count > 6 ? $" and {here.Count - 6} more" : "");
		}
		// Ours, until the original's battle marker and its window are built: who is
		// fighting whom here, how long for, and how many stand on each front.
		if (CCombats.In(State, provinceId, ECombatKind.Naval) is { } atSea)
		{
			text += $"\nBattle at sea, hour {atSea.Duration}: "
				+ $"{string.Join(", ", atSea.Attacker.Countries.Select(tag => CountryName(tag)))} with "
				+ $"{atSea.Attacker.Units.Sum(fleet => fleet.SubUnits.Count)} ships ({atSea.Attacker.Sunk.Count} sunk) against "
				+ $"{string.Join(", ", atSea.Defender.Countries.Select(tag => CountryName(tag)))} with "
				+ $"{atSea.Defender.Units.Sum(fleet => fleet.SubUnits.Count)} ({atSea.Defender.Sunk.Count} sunk)";
		}
		if (CBombing.In(State, provinceId) is { } bombing)
		{
			string what = bombing.Kind == ECombatKind.GroundBombing ? "of the ground"
				: bombing.Kind == ECombatKind.NavalBombing ? "of ships" : "of land units";
			text += $"\nBombing {what}, hour {bombing.Duration}: "
				+ $"{string.Join(", ", bombing.Attacker.Countries.Select(tag => CountryName(tag)))} with "
				+ $"{bombing.Attacker.Units.Sum(unit => unit.SubUnits.Count)} wings against "
				+ $"{string.Join(", ", bombing.Defender.Countries.Select(tag => CountryName(tag)))} with "
				+ $"{bombing.Defender.Units.Count} units";
		}
		if (CCombats.In(State, provinceId, ECombatKind.Air) is { } inTheAir)
		{
			text += $"\nBattle in the air, hour {inTheAir.Duration}: "
				+ $"{string.Join(", ", inTheAir.Attacker.Countries.Select(tag => CountryName(tag)))} with "
				+ $"{inTheAir.Attacker.Units.Sum(unit => unit.SubUnits.Count)} wings against "
				+ $"{string.Join(", ", inTheAir.Defender.Countries.Select(tag => CountryName(tag)))} with "
				+ $"{inTheAir.Defender.Units.Sum(unit => unit.SubUnits.Count)}";
		}
		if (CCombats.In(State, provinceId) is { } combat)
		{
			text += $"\nBattle, hour {combat.Duration}: "
				+ $"{string.Join(", ", combat.Attacker.Countries.Select(tag => CountryName(tag)))} attacking with "
				+ $"{combat.Attacker.Front.Count} of {combat.Attacker.Units.Count}, "
				+ $"{string.Join(", ", combat.Defender.Countries.Select(tag => CountryName(tag)))} defending with "
				+ $"{combat.Defender.Front.Count} of {combat.Defender.Units.Count}";
		}
		if (_mapView.Units.First is { } chosen && (chosen.IsFighting || chosen.IsRetreating
			|| chosen.AttackDelay > CFixedPoint.Zero))
		{
			text += $"\n{chosen.Name}: "
				+ (chosen.IsFighting ? "fighting" : chosen.IsRetreating ? "falling back" : "resting")
				+ (chosen.AttackDelay > CFixedPoint.Zero ? $", {chosen.AttackDelay.ToInt()} hours before it may attack again" : "");
		}
		if (province.IsSea)
		{
			text += "\nSea";
		}
		else
		{
			if (province.Terrain is not null)
			{
				text += "\nTerrain: " + _localisation.Localise(province.Terrain.Name);
			}
			// Ours, until there is a supply map mode: where the province draws from.
			CSupplyNetwork supply = CSupplyNetworks.Refresh(State);
			int depot = supply.DepotOf(provinceId);
			text += "\nSupply: " + (depot == 0 ? "draws on no depot"
				: depot == provinceId ? "a depot"
				: $"draws on {State.Province(depot).Name}, {supply.DistanceOf(provinceId)} away")
				+ $"; passes {CSupplyFigures.Capacity(State, province)} a day, a load loses {CSupplyFigures.Loss(State, province)}";
			// And what lies there for the units, what they ask for in a day, and what the
			// day asked of the province and took through it.
			text += $"\nSupplies {province.Pool[EGoods.Supplies]}, fuel {province.Pool[EGoods.Fuel]}"
				+ $" (its units ask {province.SupplyNeed[EGoods.Supplies]} and {province.SupplyNeed[EGoods.Fuel]} a day)"
				+ $"; asked of it today {province.SupplyDrawn[EGoods.Supplies]} and {province.SupplyDrawn[EGoods.Fuel]}"
				+ $", passed on {province.SupplyThroughput[EGoods.Supplies]} and {province.SupplyThroughput[EGoods.Fuel]}";
			text += "\nOwner: " + CountryName(province.Owner);
			if (State.Country(province.Owner) is { } owner)
			{
				text += $"   (IC {owner.TotalIc}, base {owner.BaseIc})";
			}
			if (State.Country(province.Owner) is { } producer && producer.HomeProduced.ToString() is { Length: > 0 } home)
			{
				text += "\nHome production: " + home;
			}
			if (State.Country(province.Owner) is { } holder && State.Stockpile(holder)?.ToString() is { Length: > 0 } stockpile)
			{
				text += "\nStockpile: " + stockpile + (State.IsAtWar(holder.Tag) ? "   (at war)" : "");
			}
			if (State.Country(province.Owner) is { } spender)
			{
				text += "\nShares of IC: " + string.Join("  ", spender.ProductionShares.Select(share => share.ToString()));
				if (spender.Usage.ToString() is { Length: > 0 } usage)
				{
					text += "\nUsed today: " + usage;
				}
				if (spender.Dissent > CFixedPoint.Zero)
				{
					text += "\nDissent: " + spender.Dissent;
				}
				text += $"\nManpower: {spender.Manpower}   ({spender.MonthlyManpower} a month)";
				if (spender.StrategicResources.Count > 0)
				{
					text += "\nStrategic resources: " + string.Join(", ", spender.StrategicResources
						.Where(held => held.Value > CFixedPoint.Zero)
						.Select(held => $"{_localisation.Localise(held.Key)} {held.Value}"));
				}
				text += $"\nLeadership: {spender.TotalLeadership}   (research projects {CLeadership.ResearchSlots(spender)})";
				text += $"\nOfficers {spender.Officers}   Influence {spender.DiplomaticInfluence}   Free spies {spender.FreeSpies}";
				if (spender.ResearchQueue.Count > 0)
				{
					text += "\nResearching: " + string.Join(", ", spender.ResearchQueue.Take(4)
						.Select(name => $"{_localisation.Localise(name)} " +
							(spender.ResearchProgress.TryGetValue(name, out CFixed15 progress) ? progress : CFixed15.Zero)))
						+ (spender.ResearchQueue.Count > 4 ? $" and {spender.ResearchQueue.Count - 4} more" : "");
				}
			}
			string built = string.Join(", ", State.Buildings
				.Where(building => province.BuildingLevel(building) > CFixedPoint.Zero)
				.Select(building => $"{_localisation.Localise(building.Key)} {province.BuildingLevel(building).ToInt()}"));
			if (built.Length > 0)
			{
				text += "\n" + built;
			}
			if (!Equals(province.Owner, province.Controller))
			{
				text += "\nController: " + CountryName(province.Controller);
			}
			if (province.Cores.Count > 0)
			{
				text += "\nCores: " + string.Join(", ", province.Cores.Select(core => CountryName(core)));
			}
		}
		_provinceLabel.Text = text;
	}

	private string CountryName(CCountryTag? tag)
	{
		if (tag is null)
		{
			return "nobody";
		}
		return State.Country(tag)?.Name ?? tag.Value.Value;
	}

	/// <summary>
	/// Arguments after <c>--</c> on the command line, for looking at the map without
	/// driving it by hand:
	/// <c>--centre=1861</c> puts a province in the middle and selects it,
	/// <c>--country=ENG</c> plays that country and not Germany,
	/// <c>--record=path.gz</c> records the game played - the player's orders and the AI's - and <c>--replay=path.gz</c>
	/// plays a recording back, played by whoever played it and taking no orders, the map alone shown where nobody did, the
	/// clock stopping where the recording ends (<c>docs/replay.md</c>, sections 6 and 8; both read as the session loads,
	/// not here), <c>--hash</c> prints the game's hash each midnight,
	/// <c>--zoom=4</c> sets the zoom, <c>--mode=terrain</c> starts on the terrain map (<c>--mode=weather</c> on the weather map)
	/// (<c>supply</c>, <c>air</c> and <c>revolt</c> on those),
	/// <c>--screen=production</c> or <c>--screen=technology</c> opens that screen and
	/// <c>--screen=deploy</c> the menu a finished unit is placed from,
	/// <c>--days=7</c> runs that many days before anything after it is done,
	/// <c>--research=key,key</c> puts technologies on the research queue,
	/// <c>--all-units</c> lets the country played order every unit type that can be built
	/// (and <c>--own-units</c> says the opposite, which is how it is anyway: a launch
	/// that asks has to pass something for either answer),
	/// <c>--war=GER:POL</c> puts those two countries at war, and nobody else,
	/// <c>--occupy=GER:POL</c> gives the first every province the second owns to hold, as a capture would,
	/// <c>--underground=POL:1440:0</c> finishes an underground of that country in that province, with an action,
	/// <c>--money=GER:500</c> sets that country's money, so that it may pay for a law,
	/// <c>--neutrality=GER:0</c> sets that country's neutrality, so that it may declare a war,
	/// <c>--declare=TUR:IRQ</c> has the first declare war on the second with the first goal it has,
	/// <c>--sail=ENG:10531</c> sends the strongest fleet of that country that can reach it
	/// to that sea province,
	/// <c>--mission=ENG:patrol:10531</c> gives such a fleet a mission there - reserve,
	/// patrol, intercept, sortie or convoyraid, with <c>:passive</c> or <c>:defensive</c>
	/// after the province where it is not to be aggressive - and
	/// <c>--mission=GER:airsuperiority:2313</c> or <c>airintercept</c> gives the country's
	/// best fighters that mission over the province, with <c>:300</c> after it for every
	/// province within that distance as well - and likewise a bombing mission, for the
	/// air unit with the most of what it fires: <c>groundattack</c>, <c>interdiction</c>,
	/// <c>navalstrike</c>, <c>strategicbombing</c>, <c>logisticalstrike</c>,
	/// <c>runwaycratering</c>, <c>installationstrike</c> or <c>portstrike</c>,
	/// <c>--order=type,type</c> orders one unit made of those brigades, ships or wings,
	/// <c>--builder=division:type,type</c> (or <c>ship:</c>, <c>air:</c>, <c>brigade:</c> and one
	/// type) opens the window that orders one, with that in it,
	/// <c>--technology=key</c> chooses one on the technology screen,
	/// <c>--screen=politics</c> the politics screen, <c>--screen=intelligence</c> the intelligence screen, <c>--screen=diplomacy</c> opens the diplomacy screen with its decisions, <c>--screen=strategiceffects</c> the Strategic Effects window, <c>--event=2602</c>
	/// fires that event for the player, its trigger not asked, <c>--event=2602:1861</c> for a province of his and
	/// <c>--event=2001:SPR</c> for another country, which answers it as the AI does,
	/// <c>--lock=Supplies</c> locks that slider of the production screen,
	/// <c>--select=Luftflotte II</c> selects the player's unit of that name,
	/// <c>--click=700,310;r1240,886;h48</c> clicks at those points of the canvas, in turn -
	/// with r the right button, with s shift held, with c Ctrl held, with t1.5: before the point
	/// the button held down 1.5 seconds of real time, and h48 lets 48 hours go by, m460,40 moves
	/// the pointer there without clicking, d700,310>900,500 drags from the one point to the
	/// other - sd with shift held as it lets go, D holding on at the end - ck1 presses Ctrl and 1,
	/// and w2 waits two seconds of real time, which is
	/// what a tooltip's second half waits on - once everything else is done,
	/// <c>--run</c> starts the clock at the top speed instead of paused,
	/// <c>--perf=60</c> writes once a second where a frame's time goes - the game's hours,
	/// the session's own work, the drawing - and quits after sixty seconds with the sums, and
	/// <c>--screenshot=path.png</c> saves what is on screen after a few frames and quits.
	/// </summary>
	private async void RunDeveloperArguments()
	{
		string? screenshot = null;
		string? clicks = null;
		string? select = null;
		int centre = 0;
		float? zoom = null;
		foreach (string argument in UserArguments())
		{
			if (argument.StartsWith("--screenshot="))
			{
				screenshot = argument["--screenshot=".Length..];
			}
			else if (argument.StartsWith("--click="))
			{
				clicks = argument["--click=".Length..];
			}
			else if (argument.StartsWith("--centre="))
			{
				int.TryParse(argument["--centre=".Length..], NumberStyles.Integer,
					CultureInfo.InvariantCulture, out centre);
			}
			else if (argument == "--hash")
			{
				// Each midnight, the game's hash part by part, as the headless run's hash writes it: a recording and its
				// replay should print the same lines (docs/replay.md, section 5).
				_simulation.DayPassed += () =>
				{
					ulong[] parts = CStateHash.Of(State);
					GD.Print($"{State.Date} hash {CStateHash.Whole(parts):x16} " +
						string.Join(" ", parts.Select((part, i) => $"{CStateHash.Parts[i]} {part:x16}")));
				};
			}
			else if (argument.StartsWith("--perf="))
			{
				// Where a frame's time goes, once a second, and the game quits after so many seconds (Debug/CFrameLog).
				if (double.TryParse(argument["--perf=".Length..], NumberStyles.Float, CultureInfo.InvariantCulture,
					out double seconds))
				{
					_frameLog = new Debug.CFrameLog(seconds, GetViewport().GetViewportRid());
				}
			}
			else if (argument == "--run")
			{
				_simulation.Clock.Paused = false;
				_simulation.Clock.Speed = CClock.SpeedCount - 1;
			}
			else if (argument == "--mode=terrain")
			{
				_mapView.Mode = CMapView.MapMode.Terrain;
			}
			else if (argument == "--mode=supply")
			{
				_mapView.Mode = CMapView.MapMode.Supply;
			}
			else if (argument == "--mode=weather")
			{
				_mapView.Mode = CMapView.MapMode.Weather;
			}
			else if (argument == "--mode=air")
			{
				_mapView.Mode = CMapView.MapMode.Air;
			}
			else if (argument == "--mode=revolt")
			{
				_mapView.Mode = CMapView.MapMode.Revolt;
			}
			else if (argument == "--screen=production")
			{
				Toggle(_production);
			}
			else if (argument == "--screen=technology")
			{
				Toggle(_technology);
			}
			else if (argument == "--screen=deploy")
			{
				_deployMenu?.Open();
			}
			else if (argument == "--screen=politics")
			{
				Toggle(_politics);
			}
			else if (argument == "--screen=intelligence")
			{
				Toggle(_espionage);
			}
			else if (argument == "--screen=strategiceffects")
			{
				_strategicEffects?.SetOpen(true);
			}
			else if (argument == "--screen=diplomacy")
			{
				Toggle(_diplomacy);
			}
			else if (argument.StartsWith("--event=") && Player is not null)
			{
				// Fires an event for the player, its trigger not asked: --event=2602, or with a
				// province of his for a province's window, --event=2602:1861, or for another
				// country, which answers it as the AI does, --event=2001:SPR.
				string[] parts = argument["--event=".Length..].Split(':');
				int province = 0;
				CCountryTag whose = Player.Tag;
				if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
					&& (parts.Length < 2
						|| int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out province)
						|| CCountryTag.TryParse(parts[1].ToUpperInvariant(), out whose)))
				{
					_simulation.Post(new CFireEventCommand(id, whose, province));
					_simulation.Pump();
				}
			}
			else if (argument.StartsWith("--days=")
				&& int.TryParse(argument["--days=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int days))
			{
				// Runs that many days at once, whatever the clock says: a unit ordered or
				// under construction can then be found finished.
				UnlessTheReplayEnds(() => _simulation.RunHours(days * 24));
			}
			else if (argument.StartsWith("--lock=")
				&& System.Enum.TryParse(argument["--lock=".Length..], ignoreCase: true, out EProductionCategory locked))
			{
				_production?.Lock(locked);
			}
			else if (argument.StartsWith("--technology="))
			{
				_technology?.SelectByKey(argument["--technology=".Length..]);
			}
			else if (argument.StartsWith("--research=") && Player is not null)
			{
				// Queues technologies by key, in the order given: --research=a,b,c. One the
				// country may not research would have the whole queue refused, so it is
				// left out and named.
				var queue = new System.Collections.Generic.List<string>();
				foreach (string key in argument["--research=".Length..].Split(','))
				{
					if (State.Technologies.TryGetValue(key, out CTechnology? technology)
						&& CResearch.MayResearchNext(State, Player, technology))
					{
						queue.Add(key);
					}
					else
					{
						GD.Print($"--research: '{key}' is not a technology {Player.Tag} may research now.");
					}
				}
				_simulation.Post(new CSetResearchQueueCommand(Player.Tag, queue));
			}
			else if (argument.StartsWith("--war="))
			{
				// Puts two countries at war at once and nobody else, for trials: --war=GER:POL.
				// A declaration is the diplomacy screen's (war.md). Carried out before anything after it.
				string[] tags = argument["--war=".Length..].Split(':');
				if (tags.Length == 2 && CCountryTag.TryParse(tags[0], out CCountryTag first)
					&& CCountryTag.TryParse(tags[1], out CCountryTag second)
					&& new CStartWarCommand(first, second) is var war && war.MayExecute(State))
				{
					_simulation.Post(war);
					_simulation.Pump();
				}
				else
				{
					GD.Print($"--war: '{argument["--war=".Length..]}' is not two countries at peace with each other.");
				}
			}
			else if (argument.StartsWith("--occupy="))
			{
				// Gives the first country every province the second owns to hold, as a capture would and with
				// nothing damaged: --occupy=GER:POL. So that the occupied countries and their policies may be tried.
				string[] tags = argument["--occupy=".Length..].Split(':');
				if (tags.Length == 2 && CCountryTag.TryParse(tags[0], out CCountryTag holder)
					&& CCountryTag.TryParse(tags[1], out CCountryTag owner)
					&& new Sim.Commands.CHoldLandCommand(holder, owner) is var hold && hold.MayExecute(State))
				{
					_simulation.Post(hold);
					_simulation.Pump();
				}
				else
				{
					GD.Print($"--occupy: '{argument["--occupy=".Length..]}' is not two countries.");
				}
			}
			else if (argument.StartsWith("--underground="))
			{
				// An underground of the country finished in the province as a line of the queue would finish one, the
				// queue not holding buildings yet: --underground=POL:1440, and its action after a third part, 0 to 2.
				string[] parts = argument["--underground=".Length..].Split(':');
				if (parts.Length >= 2 && CCountryTag.TryParse(parts[0], out CCountryTag builder)
					&& int.TryParse(parts[1], out int province)
					&& new Sim.Commands.CPlaceUndergroundCommand(builder, province) is var place && place.MayExecute(State))
				{
					_simulation.Post(place);
					if (parts.Length >= 3 && int.TryParse(parts[2], out int action))
					{
						_simulation.Post(new Sim.Commands.CChangeUndergroundMissionCommand(province, action));
					}
					_simulation.Pump();
				}
				else
				{
					GD.Print($"--underground: '{argument["--underground=".Length..]}' is not a country and a province.");
				}
			}
			else if (argument.StartsWith("--declare="))
			{
				// One country declares war on another as the declare-war dialog would, with the first goal it
				// offers, whatever its neutrality: --declare=TUR:IRQ. So that an AI's war, and its calls to arms,
				// may be tried; the AI declares nothing yet.
				string[] tags = argument["--declare=".Length..].Split(':');
				if (tags.Length == 2 && State.Country(CCountryTag.TryParse(tags[0], out CCountryTag one) ? one : null) is { } actor
					&& State.Country(CCountryTag.TryParse(tags[1], out CCountryTag other) ? other : null) is { } recipient
					&& Sim.Wars.CWarGoals.FirstOffered(State, actor, recipient) is { } goal)
				{
					_simulation.Post(new Sim.Commands.COfferCommand(new Sim.Agreements.COffer(Sim.Agreements.EOfferKind.DeclareWar,
						actor.Tag, recipient.Tag, Sim.Agreements.EOfferStage.Accepted, true, State.TotalHours) { WarGoal = goal }));
					_simulation.Pump();
				}
				else
				{
					GD.Print($"--declare: '{argument["--declare=".Length..]}' is not two countries one has a goal against.");
				}
			}
			else if (argument.StartsWith("--neutrality="))
			{
				// Sets a country's neutrality, so that it may declare a war on a new game: --neutrality=GER:0.
				string[] parts = argument["--neutrality=".Length..].Split(':');
				if (parts.Length == 2 && CCountryTag.TryParse(parts[0], out CCountryTag neutral)
					&& CFixedPoint.TryParse(parts[1], out CFixedPoint neutrality)
					&& new CSetNeutralityCommand(neutral, neutrality) is var set && set.MayExecute(State))
				{
					_simulation.Post(set);
					_simulation.Pump();
				}
				else
				{
					GD.Print($"--neutrality: '{argument["--neutrality=".Length..]}' is not a country and a figure.");
				}
			}
			else if (argument.StartsWith("--money="))
			{
				// Sets a country's money, so that it may pay for a law on a new game: --money=GER:500.
				string[] parts = argument["--money=".Length..].Split(':');
				if (parts.Length == 2 && CCountryTag.TryParse(parts[0], out CCountryTag rich)
					&& CFixedPoint.TryParse(parts[1], out CFixedPoint money)
					&& new CSetMoneyCommand(rich, money) is var set && set.MayExecute(State))
				{
					_simulation.Post(set);
					_simulation.Pump();
				}
				else
				{
					GD.Print($"--money: '{argument["--money=".Length..]}' is not a country and a figure.");
				}
			}
			else if (argument.StartsWith("--sail="))
			{
				// Sends a country's strongest fleet to a sea province, there being no AI to
				// send one: --sail=ENG:10531. It is what gives the player's own fleet an
				// enemy to find at sea. Carried out before anything after it.
				string[] parts = argument["--sail=".Length..].Split(':');
				if (parts.Length == 2 && CCountryTag.TryParse(parts[0], out CCountryTag owner)
					&& int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sea)
					&& State.Country(owner) is { } sailor
					&& sailor.AllUnits().Where(unit => unit.Kind == EUnitKind.Navy && unit.Location > 0)
						.OrderByDescending(unit => unit.SubUnits.Sum(ship => ship.Stats[CNavalFire.SeaAttack].Raw))
						.FirstOrDefault(unit => new CSetUnitOrderCommand(owner, unit.Id, EUnitOrder.Move, sea).MayExecute(State))
						is { } fleet)
				{
					// The strongest of its fleets that has the province within its reach.
					_simulation.Post(new CSetUnitOrderCommand(owner, fleet.Id, EUnitOrder.Move, sea));
					_simulation.Pump();
					GD.Print($"--sail: {fleet.Name} of {sailor.Name} sails for {State.Province(sea).Name}.");
				}
				else
				{
					GD.Print($"--sail: '{argument["--sail=".Length..]}' is not a country with a fleet and a sea province it can reach.");
				}
			}
			else if (argument.StartsWith("--mission="))
			{
				// Gives a country's strongest fleet that may have it a mission for a province,
				// there being no AI to give one: --mission=ENG:patrol:10531, or with a stance
				// after it, --mission=GER:convoyraid:10538:passive. The missions are reserve,
				// patrol, intercept, sortie and convoyraid. Carried out before anything after it.
				string[] parts = argument["--mission=".Length..].Split(':');
				int stance = parts.Length > 3 && parts[3].Equals("passive", System.StringComparison.OrdinalIgnoreCase) ? CMission.Passive
					: parts.Length > 3 && parts[3].Equals("defensive", System.StringComparison.OrdinalIgnoreCase) ? CMission.Defensive
					: CMission.Aggressive;
				// An air mission instead, for the country's air unit with the most air attack:
				// --mission=GER:airsuperiority:2313 or --mission=GER:airintercept:1861, with a
				// radius after the province where it is to cover more than the one.
				if (parts.Length >= 3 && CCountryTag.TryParse(parts[0], out CCountryTag flier)
					&& System.Enum.TryParse(parts[1], ignoreCase: true, out EUnitOrder airMission)
					&& (CAirMissions.IsAirMission(airMission) || CBombingOrders.IsBombingMission(airMission))
					&& int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int over))
				{
					// A bombing mission likewise - groundattack, interdiction, navalstrike,
					// strategicbombing, logisticalstrike, runwaycratering, installationstrike or
					// portstrike - for the air unit with the most of the attack it fires.
					string[] fired = CBombingOrders.GoesForLandUnits(airMission)
						? new[] { CUnitTotals.SoftAttack, CUnitTotals.HardAttack }
						: CBombingOrders.GoesForShips(airMission) ? new[] { CNavalFire.SeaAttack }
						: CBombingOrders.IsBombingMission(airMission) ? new[] { CBombingOrders.StrategicAttack }
						: new[] { CAirMissions.AirAttack };
					CUnit? wing = State.Country(flier)?.AllUnits()
						.Where(unit => unit.Kind == EUnitKind.Air && unit.Location > 0 && unit.CarriedBy is null)
						.OrderByDescending(unit => unit.SubUnits.Sum(sub => fired.Sum(statistic => sub.Stats[statistic].Raw)))
						.FirstOrDefault(unit => over > 0 && over < State.Provinces.Count
							&& CUnitOrders.IsInRange(State, unit, airMission, State.Province(over))
							&& new CSetUnitOrderCommand(flier, unit.Id, airMission, over).MayExecute(State));
					if (wing is not null && State.Country(flier) is { } air)
					{
						List<int> area = parts.Length > 3 && int.TryParse(parts[3], NumberStyles.Integer,
								CultureInfo.InvariantCulture, out int radius)
							? CAirMissionArea.Circle(State, air, wing, airMission, over, CFixedPoint.FromInt(radius))
							: new List<int> { over };
						_simulation.Post(new CSetUnitOrderCommand(flier, wing.Id, airMission, over, provinces: area));
						_simulation.Pump();
						GD.Print($"--mission: {wing.Name} of {air.Name}, based in {State.Province(wing.Base).Name}, "
							+ $"is given {airMission} over {State.Province(over).Name} and {area.Count - 1} provinces more.");
					}
					else
					{
						GD.Print($"--mission: '{argument["--mission=".Length..]}' is not a country with an air unit that may be given that.");
					}
				}
				else if (parts.Length >= 3 && CCountryTag.TryParse(parts[0], out CCountryTag owner)
					&& System.Enum.TryParse(parts[1], ignoreCase: true, out EUnitOrder mission) && CMissions.IsMission(mission)
					&& int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int where)
					&& State.Country(owner) is { } sender
					&& sender.AllUnits().Where(unit => unit.Kind == EUnitKind.Navy && unit.Location > 0)
						.OrderByDescending(unit => unit.SubUnits.Sum(ship =>
							ship.Stats[mission == EUnitOrder.ConvoyRaid ? CConvoyRaid.ConvoyAttack : CNavalFire.SeaAttack].Raw))
						.FirstOrDefault(unit => new CSetUnitOrderCommand(owner, unit.Id, mission, where, stance).MayExecute(State))
						is { } fleet)
				{
					_simulation.Post(new CSetUnitOrderCommand(owner, fleet.Id, mission, where, stance));
					_simulation.Pump();
					GD.Print($"--mission: {fleet.Name} of {sender.Name}, in {State.Province(fleet.Location).Name}, "
						+ $"is given {mission} for {State.Province(where).Name}.");
				}
				else
				{
					GD.Print($"--mission: '{argument["--mission=".Length..]}' is not a country with a fleet, a mission it may be given and a province for it.");
				}
			}
			else if (argument == "--all-units" && Player is not null)
			{
				// Gives the country played every unit type to order, whatever technology
				// or country its file keeps it to - a carrier and its air groups in 1936.
				// Carried out at once, so that a switch after this one finds it done.
				_simulation.Post(new CUnlockEveryUnitCommand(Player.Tag));
				_simulation.Pump();
			}
			else if (argument.StartsWith("--builder="))
			{
				// Opens one of the windows a unit is ordered from over the production
				// screen: --builder=division:infantry_brigade,artillery_brigade.
				if (_production is { IsOpen: false })
				{
					Toggle(_production);
				}
				_production?.OpenBuilder(argument["--builder=".Length..]);
			}
			else if (argument.StartsWith("--order=") && Player is not null)
			{
				// Orders one unit of the brigades, ships or wings named: --order=a,b,c. Each
				// is called by its type's own name, there being no builder to name it in.
				var brigades = new System.Collections.Generic.List<(string, string)>();
				foreach (string key in argument["--order=".Length..].Split(','))
				{
					brigades.Add((key, _localisation.Localise(key)));
				}
				var order = new COrderUnitCommand(Player.Tag, brigades);
				if (order.MayExecute(State))
				{
					_simulation.Post(order);
				}
				else
				{
					GD.Print($"--order: {Player.Tag} may not order '{argument["--order=".Length..]}' now.");
				}
			}
			else if (argument.StartsWith("--zoom=")
				&& float.TryParse(argument["--zoom=".Length..], NumberStyles.Float,
					CultureInfo.InvariantCulture, out float parsed))
			{
				zoom = parsed;
			}
			else if (argument.StartsWith("--select="))
			{
				select = argument["--select=".Length..];
			}
		}

		if (centre > 0)
		{
			_mapView.CentreOn(centre, zoom);
			_mapView.Select(centre);
			ShowProvince(centre);
		}

		// A unit of the player's by its name, for one that no click can reach - an air
		// unit at its base, whose stack lies under the land units' there.
		if (select is not null && Player is not null)
		{
			foreach (CUnit unit in Player.AllUnits())
			{
				if (unit.Name == select)
				{
					_mapView.SelectUnit(unit);
					break;
				}
			}
		}

		if (clicks is not null)
		{
			await Click(clicks);
		}

		if (screenshot is null)
		{
			return;
		}
		for (int frame = 0; frame < 8; frame++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error error = GetViewport().GetTexture().GetImage().SavePng(screenshot);
		GD.Print(error == Error.Ok ? $"Saved {screenshot}" : $"Could not save {screenshot}: {error}");
		GetTree().Quit();
	}

	/// <summary>
	/// Clicks the left mouse button at each of some points of the 1920 x 1080 canvas, in
	/// turn, as a hand would: the pointer moves there, the button goes down and comes up.
	/// The clicks go in at the same door as real ones, so what would stop a real click
	/// stops these.
	/// </summary>
	private async System.Threading.Tasks.Task Click(string points)
	{
		foreach (string entry in points.Split(';', System.StringSplitOptions.RemoveEmptyEntries))
		{
			// h48 is not a click: it lets that many hours go by, so that what a click set
			// going can be seen further on.
			if (entry.StartsWith('h') && int.TryParse(entry[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours))
			{
				// An hour to a frame, as the game runs them, and nothing told to redraw: what
				// the screen shows afterwards is what a player would see.
				for (int hour = 0; hour < hours; hour++)
				{
					await Frames(1);
					if (!UnlessTheReplayEnds(() => _simulation.RunHours(1)))
					{
						break;
					}
				}
				await Frames(2);
				continue;
			}
			// kH is not a click either: it presses and lets go of the key of that letter or digit, and K4 the same with
			// Shift held - K4 opens the debug menu.
			// ck1 the same with Ctrl held: Ctrl and a digit keeps a selection group.
			bool withCtrl = entry.Length == 3 && entry[0] == 'c';
			string keyEntry = withCtrl ? entry[1..] : entry;
			if (keyEntry.Length == 2 && keyEntry[0] is 'k' or 'K' && char.IsLetterOrDigit(keyEntry[1]))
			{
				Key key = (Key)char.ToUpperInvariant(keyEntry[1]);
				bool shifted = keyEntry[0] == 'K';
				await Frames(2);
				GetViewport().PushInput(new InputEventKey
				{
					Keycode = key, PhysicalKeycode = key, ShiftPressed = shifted, CtrlPressed = withCtrl, Pressed = true,
				}, true);
				await Frames(1);
				GetViewport().PushInput(new InputEventKey
				{
					Keycode = key, PhysicalKeycode = key, ShiftPressed = shifted, CtrlPressed = withCtrl, Pressed = false,
				}, true);
				await Frames(2);
				continue;
			}
			// w2 waits two seconds of real time, which a tooltip's second half waits on.
			if (entry.StartsWith('w') && double.TryParse(entry[1..], NumberStyles.Float, CultureInfo.InvariantCulture, out double wait))
			{
				await ToSignal(GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
				await Frames(2);
				continue;
			}
			// d700,310>900,500 holds the left button down at the first point, moves to the second
			// in ten steps and lets go there: a drag. sd lets go with Shift held, and D does not let go,
			// for a screenshot of the drag box.
			bool shiftDrag = entry.StartsWith("sd");
			string dragEntry = shiftDrag ? entry[1..] : entry;
			if (dragEntry.StartsWith('d') || dragEntry.StartsWith('D'))
			{
				bool letGo = dragEntry[0] == 'd';
				string[] ends = dragEntry[1..].Split('>');
				string[] a = ends[0].Split(',');
				string[] b = ends.Length == 2 ? ends[1].Split(',') : System.Array.Empty<string>();
				if (a.Length == 2 && b.Length == 2
					&& float.TryParse(a[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax)
					&& float.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay)
					&& float.TryParse(b[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float bx)
					&& float.TryParse(b[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float by))
				{
					var from = new Vector2(ax, ay);
					var to = new Vector2(bx, by);
					await Frames(3);
					GetViewport().PushInput(new InputEventMouseMotion { Position = from, GlobalPosition = from }, true);
					await Frames(1);
					GetViewport().PushInput(new InputEventMouseButton
					{
						Position = from, GlobalPosition = from, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left,
					}, true);
					for (int step = 1; step <= 10; step++)
					{
						await Frames(1);
						Vector2 there = from.Lerp(to, step / 10f);
						GetViewport().PushInput(new InputEventMouseMotion { Position = there, GlobalPosition = there, ButtonMask = MouseButtonMask.Left }, true);
					}
					await Frames(1);
					if (letGo)
					{
						GetViewport().PushInput(new InputEventMouseButton
						{
							Position = to, GlobalPosition = to, ButtonIndex = MouseButton.Left, Pressed = false, ShiftPressed = shiftDrag,
						}, true);
					}
					await Frames(2);
				}
				else
				{
					GD.Print($"--click: '{entry}' is not a drag.");
				}
				continue;
			}
			// m before a point only moves the pointer there, which is how a tooltip is asked for.
			if (entry.StartsWith('m'))
			{
				string[] to = entry[1..].Split(',');
				if (to.Length == 2
					&& float.TryParse(to[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float mx)
					&& float.TryParse(to[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float my))
				{
					await Frames(3);
					GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(mx, my), GlobalPosition = new Vector2(mx, my) }, true);
					await Frames(2);
				}
				else
				{
					GD.Print($"--click: '{entry}' is not a point to move to.");
				}
				continue;
			}
			// r before a point clicks the right button there, s holds shift down and c Ctrl,
			// and t1.5: before the point holds the button down that many seconds.
			string point = entry.TrimStart('r', 's', 'c');
			string keys = entry[..(entry.Length - point.Length)];
			MouseButton which = keys.Contains('r') ? MouseButton.Right : MouseButton.Left;
			bool shift = keys.Contains('s');
			bool ctrl = keys.Contains('c');
			double holdFor = 0;
			int colon = point.IndexOf(':');
			if (point.StartsWith('t') && colon > 0)
			{
				double.TryParse(point[1..colon], NumberStyles.Float, CultureInfo.InvariantCulture, out holdFor);
				point = point[(colon + 1)..];
			}
			string[] parts = point.Split(',');
			if (parts.Length != 2
				|| !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
				|| !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
			{
				GD.Print($"--click: '{point}' is not a point.");
				continue;
			}
			var at = new Vector2(x, y);
			await Frames(3);
			// A key held is told to the input singleton, which is what is asked whether it is down.
			if (ctrl)
			{
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = true });
			}
			if (shift)
			{
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = true });
			}
			GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
			await Frames(1);
			GetViewport().PushInput(new InputEventMouseButton
			{
				Position = at, GlobalPosition = at, ButtonIndex = which, Pressed = true, ShiftPressed = shift,
				CtrlPressed = ctrl, ButtonMask = which == MouseButton.Right ? MouseButtonMask.Right : MouseButtonMask.Left,
			}, true);
			if (holdFor > 0)
			{
				await ToSignal(GetTree().CreateTimer(holdFor), SceneTreeTimer.SignalName.Timeout);
			}
			else
			{
				await Frames(2);
			}
			GetViewport().PushInput(new InputEventMouseButton
			{
				Position = at, GlobalPosition = at, ButtonIndex = which, Pressed = false, ShiftPressed = shift,
				CtrlPressed = ctrl,
			}, true);
			if (ctrl)
			{
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false });
			}
			if (shift)
			{
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = false });
			}
			await Frames(2);
		}
	}

	private async System.Threading.Tasks.Task Frames(int count)
	{
		for (int frame = 0; frame < count; frame++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
}
