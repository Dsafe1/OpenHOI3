using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Gui;
using OpenHOI3.Map;
using OpenHOI3.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

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
	private Label _provinceLabel = null!;
	private int _shownProvince;

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

		_mapView = new CMapView { Name = "Map" };
		_mapView.Setup(Map, State);
		AddChild(_mapView);

		BuildHud(localisation);
		_mapView.ProvinceHovered += ShowProvince;

		RunDeveloperArguments();
	}

	/// <summary>
	/// The game's own interface, from the original's interface files: so far the top
	/// bar. Under it a province readout in a plain engine label, which is scaffolding
	/// and goes when the province view comes.
	/// </summary>
	private void BuildHud(CLocalisation localisation)
	{
		var layer = new CanvasLayer { Name = "Hud" };
		AddChild(layer);

		var clock = Stopwatch.StartNew();
		CGuiContext gui = CGuiContext.Load(_directories, localisation);
		GD.Print($"Loaded {gui.Types.Count} interface elements and {gui.Sprites.Count} sprites " +
			$"in {clock.ElapsedMilliseconds} ms.");
		if (Player is not null)
		{
			// The screens go under the top bar, which is drawn over whatever is open.
			_production = new CProductionView();
			_production.Setup(gui, _simulation, Player);
			layer.AddChild(_production);
			_technology = new CTechnologyView();
			_technology.Setup(gui, _simulation, Player);
			layer.AddChild(_technology);

			_topBar = new CTopBar();
			_topBar.Setup(gui, _simulation, Player);
			layer.AddChild(_topBar);
			_topBar.ProductionPressed += () => Toggle(_production);
			_topBar.TechnologyPressed += () => Toggle(_technology);
		}

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
		if (provinceId == 0)
		{
			_provinceLabel.Text = "";
			return;
		}

		CProvince province = State.Province(provinceId);
		string text = $"{province.Name}  ({province.Id})";
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
	/// <c>--screen=production</c> or <c>--screen=technology</c> opens that screen,
	/// <c>--research=key,key</c> puts technologies on the research queue,
	/// <c>--technology=key</c> chooses one on the technology screen,
	/// <c>--lock=Supplies</c> locks that slider of the production screen,
	/// <c>--run</c> starts the clock at the top speed instead of paused, and
	/// <c>--screenshot=path.png</c> saves what is on screen after a few frames and quits.
	/// </summary>
	private async void RunDeveloperArguments()
	{
		string? screenshot = null;
		int centre = 0;
		float? zoom = null;
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument.StartsWith("--screenshot="))
			{
				screenshot = argument["--screenshot=".Length..];
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
}
