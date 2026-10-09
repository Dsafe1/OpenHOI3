using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Map;
using OpenHOI3.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
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
	private CMapView _mapView = null!;
	private CLocalisation _localisation = null!;
	private CSimulation _simulation = null!;
	private CTopBar? _topBar;
	private CProductionView? _production;
	private CTechnologyView? _technology;
	private CDeployMenu? _deployMenu;
	private CAlerts? _alerts;
	private Label _provinceLabel = null!;
	private int _shownProvince;
	private bool _unitsMoved;
	private bool _hourPassed;
	private bool _ordered;
	private bool _wasMoving;
	private int _waiting;

	/// <summary>The country played when nothing says which. There is no screen to choose one on yet.</summary>
	private const string DefaultPlayer = "GER";

	public CGameState State { get; private set; } = null!;

	/// <summary>The country the interface is about, or null when the install has none of that tag.</summary>
	public CCountry? Player { get; private set; }

	public CMap Map { get; private set; } = null!;

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
		CLocalisation localisation = CLocalisation.Load(_directories);
		_localisation = localisation;
		CCountries countries = CCountries.Load(_directories, localisation);
		Map = CMap.Load(_directories);
		State = CGameStateLoader.LoadStart(_directories, Map, localisation, countries);
		GD.Print($"Loaded {Map.ProvinceCount - 1} provinces and {countries.Count} countries " +
			$"for {State.Date} in {clock.ElapsedMilliseconds} ms.");

		_simulation = new CSimulation(State);
		Player = State.Country(new CCountryTag(PlayerArgument() ?? DefaultPlayer))
			?? State.Country(new CCountryTag(DefaultPlayer));

		clock.Restart();
		CGuiContext gui = CGuiContext.Load(_directories, localisation);
		GD.Print($"Loaded {gui.Types.Count} interface elements and {gui.Sprites.Count} sprites " +
			$"in {clock.ElapsedMilliseconds} ms.");

		_mapView = new CMapView { Name = "Map" };
		_mapView.Setup(Map, State);
		_mapView.SetupCounters(gui, CCounterTypes.Load(_directories), CBillboardTypes.Load(_directories),
			CArrowTypes.Load(_directories));
		AddChild(_mapView);

		BuildHud(gui);
		_mapView.ProvinceHovered += ShowProvince;

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

		if (Player is not null)
		{
			// The screens go under the top bar, which is drawn over whatever is open.
			_production = new CProductionView();
			_production.Setup(gui, _simulation, Player);
			layer.AddChild(_production);
			_technology = new CTechnologyView();
			_technology.Setup(gui, _simulation, Player);
			layer.AddChild(_technology);

			// The deploy menu and the alert that opens it. A unit chosen there is placed
			// by a click on the map, which is shown where it may go.
			_deployMenu = new CDeployMenu();
			_deployMenu.Setup(gui, _simulation, Player);
			layer.AddChild(_deployMenu);
			_deployMenu.PlacingChanged += unit => _mapView.ShowPlaces(
				unit is null ? null : CDeployment.Places(State, Player, unit));
			_deployMenu.Toggled += () => ShowProvince(_shownProvince);
			_mapView.ProvinceSelected += province => _deployMenu.PlaceIn(province);

			_alerts = new CAlerts();
			_alerts.Setup(gui, Player);
			layer.AddChild(_alerts);
			_alerts.DeployablePressed += ToggleDeployMenu;

			_topBar = new CTopBar();
			_topBar.Setup(gui, _simulation, Player);
			layer.AddChild(_topBar);
			_topBar.ProductionPressed += () => Toggle(_production);
			_topBar.TechnologyPressed += () => Toggle(_technology);
		}
		_simulation.DayPassed += () => _unitsMoved = true;
		_simulation.HourPassed += () => _hourPassed = true;
		_mapView.UnitSelected += _ => ShowProvince(_shownProvince);
		_mapView.UnitOrdered += OrderUnit;

		_provinceLabel = new Label
		{
			Position = new Vector2(16, 96),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_provinceLabel.AddThemeFontSizeOverride("font_size", 18);
		_provinceLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_provinceLabel.AddThemeConstantOverride("outline_size", 6);
		layer.AddChild(_provinceLabel);
	}

	/// <summary>
	/// A top bar button brings its screen up, or takes it down if it is up already. One
	/// screen at a time: the one that was up comes down. That the original does the
	/// same was not read; it is how the game is remembered.
	/// </summary>
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
		foreach (CCountryView? other in new CCountryView?[] { _production, _technology })
		{
			if (!ReferenceEquals(other, view))
			{
				other?.Close();
			}
		}
		view.Open();
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

	/// <summary>
	/// Feeds the frame's length to the game. Whether any hours come of it is the
	/// clock's decision - none while paused, as a new game is.
	/// </summary>
	public override void _Process(double delta)
	{
		// The readout is refreshed once a frame and not once an hour: at the top speed a
		// frame holds thousands of hours.
		if (_simulation.Advance(delta) > 0)
		{
			ShowProvince(_shownProvince);                       // its figures move with the days
		}

		// The counters are worked out again when a day has gone by, a unit of the
		// player's has been placed or ordered, or an hour has gone by with a unit on the
		// march - and once a frame at most.
		int waiting = Player?.Deployments.Count ?? 0;
		if (_ordered || _hourPassed)
		{
			// The hour a unit arrives it is on the march no longer, and that hour is the
			// one its counter has to change province in: so whether one was moving before
			// counts as much as whether one is now.
			bool moving = AnyUnitMoving();
			if (_ordered || moving || _wasMoving)
			{
				_unitsMoved = true;
			}
			_wasMoving = moving;
		}
		_ordered = false;
		_hourPassed = false;
		if (_unitsMoved || waiting != _waiting)
		{
			_unitsMoved = false;
			_waiting = waiting;
			_mapView.Counters?.Refresh();
			ShowProvince(_shownProvince);
		}
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
	/// A right click on the map with a unit selected. Only the player's own units take
	/// it. Sent to where it already stands a unit is halted - unless the order is to be
	/// added to a path it has, when it is one more leg of that.
	/// </summary>
	private void OrderUnit(int unitId, int province, bool append)
	{
		if (Player is null || _mapView.Counters?.Selected is not { } unit || unit.Id != unitId
			|| unit.Owner != Player.Tag)
		{
			return;
		}
		CCommand command = province == unit.Location && (!append || !unit.IsMoving)
			? new CCancelMovementCommand(Player.Tag, unitId)
			: new CMoveUnitCommand(Player.Tag, unitId, province, append);
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
			default:
				return;
		}
	}

	/// <summary>The tag given with <c>--country=</c> on the command line, or null.</summary>
	private static string? PlayerArgument()
	{
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument.StartsWith("--country=") && CCountryTag.TryParse(argument["--country=".Length..], out CCountryTag tag))
			{
				return tag.Value;
			}
		}
		return null;
	}

	private void ShowProvince(int provinceId)
	{
		_shownProvince = provinceId;
		// The readout stands where the deploy menu does, and gives way to it.
		if (provinceId == 0 || _deployMenu is { IsOpen: true })
		{
			_provinceLabel.Text = "";
			return;
		}

		CProvince province = State.Province(provinceId);
		string text = $"{province.Name}  ({province.Id})";
		if (_mapView.Counters?.Selected is { } selected)
		{
			string line = $"Selected: {selected.Name}  ({CCounterText.Figures(selected)})";
			if (selected.IsMoving && State.Country(selected.Owner) is { } owner)
			{
				long hours = CMovement.HoursToArrive(State, owner, selected);
				line += $"  to {State.Province(selected.Path[^1]).Name}"
					+ (hours < 0 ? "" : $", there in {hours / 24} days {hours % 24} hours");
			}
			text = line + "\n" + text;
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
				text += $"\nOfficers {spender.Officers}   Influence {spender.DiplomaticInfluence}   Spies in training {spender.SpiesInTraining}";
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
	/// <c>--zoom=4</c> sets the zoom, <c>--mode=terrain</c> starts on the terrain map,
	/// <c>--screen=production</c> or <c>--screen=technology</c> opens that screen and
	/// <c>--screen=deploy</c> the menu a finished unit is placed from,
	/// <c>--days=7</c> runs that many days before anything after it is done,
	/// <c>--research=key,key</c> puts technologies on the research queue,
	/// <c>--all-units</c> lets the country played order every unit type that can be built,
	/// <c>--order=type,type</c> orders one unit made of those brigades, ships or wings,
	/// <c>--builder=division:type,type</c> (or <c>ship:</c>, <c>air:</c>, <c>brigade:</c> and one
	/// type) opens the window that orders one, with that in it,
	/// <c>--technology=key</c> chooses one on the technology screen,
	/// <c>--lock=Supplies</c> locks that slider of the production screen,
	/// <c>--click=700,310;r1240,886;h48</c> clicks at those points of the canvas, in turn -
	/// with r the right button, with s shift held, and h48 lets 48 hours go by - once
	/// everything else is done,
	/// <c>--run</c> starts the clock at the top speed instead of paused, and
	/// <c>--screenshot=path.png</c> saves what is on screen after a few frames and quits.
	/// </summary>
	private async void RunDeveloperArguments()
	{
		string? screenshot = null;
		string? clicks = null;
		int centre = 0;
		float? zoom = null;
		foreach (string argument in OS.GetCmdlineUserArgs())
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
			else if (argument == "--run")
			{
				_simulation.Clock.Paused = false;
				_simulation.Clock.Speed = CClock.SpeedCount - 1;
			}
			else if (argument == "--mode=terrain")
			{
				_mapView.Mode = CMapView.MapMode.Terrain;
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
			else if (argument.StartsWith("--days=")
				&& int.TryParse(argument["--days=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int days))
			{
				// Runs that many days at once, whatever the clock says: a unit ordered or
				// under construction can then be found finished.
				_simulation.RunHours(days * 24);
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
		}

		if (centre > 0)
		{
			_mapView.CentreOn(centre, zoom);
			_mapView.Select(centre);
			ShowProvince(centre);
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
					_simulation.RunHours(1);
				}
				await Frames(2);
				continue;
			}
			// r before a point clicks the right button there, and s holds shift down.
			string point = entry.TrimStart('r', 's');
			string keys = entry[..(entry.Length - point.Length)];
			MouseButton which = keys.Contains('r') ? MouseButton.Right : MouseButton.Left;
			bool shift = keys.Contains('s');
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
			GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
			await Frames(1);
			GetViewport().PushInput(new InputEventMouseButton
			{
				Position = at, GlobalPosition = at, ButtonIndex = which, Pressed = true, ShiftPressed = shift,
				ButtonMask = which == MouseButton.Right ? MouseButtonMask.Right : MouseButtonMask.Left,
			}, true);
			await Frames(2);
			GetViewport().PushInput(new InputEventMouseButton
			{
				Position = at, GlobalPosition = at, ButtonIndex = which, Pressed = false, ShiftPressed = shift,
			}, true);
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
