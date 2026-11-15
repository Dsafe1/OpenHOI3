using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Frontend;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Content.Settings;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Lua;
using OpenHOI3.Map;
using OpenHOI3.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The front end: the main menu and the lobby, with the political map of the world the lobby offers behind them - hidden
/// under the main menu's backdrop, seen behind the lobby. It holds that world - the start, or a save loaded - and makes the
/// game Play asks for, handing it over with all it read. A new one each visit, as the original makes one. See
/// <c>docs/mechanics/frontend.md</c>; <c>reversing/findings/FINDINGS-frontend.md</c>.
/// </summary>
public partial class CFrontEnd : Node
{
	private CDirectorySettings _directories = null!;
	private CLocalisation _localisation = null!;
	private CCountries _countries = null!;
	private CMap _map = null!;
	private CAiScripts? _aiScripts;
	private CGuiContext _gui = null!;
	private CMapView? _mapView;
	private System.Collections.Generic.IReadOnlyList<Content.Map.CQuadTile>? _quadTiles;
	private CMapCache? _mapCache;
	private CMainMenu _menu = null!;
	private CLobby _lobby = null!;
	private CLobbyOptions? _lobbyOptions;
	private CGameOptionsWindow? _options;
	private Gfx.CBrightnessLayer? _brightness;
	private bool _handedOver;

	/// <summary>The world the lobby offers: the start's, or the save's that was chosen.</summary>
	public CGameState World { get; private set; } = null!;

	/// <summary>Whether <see cref="World"/> came from a save.</summary>
	public bool SaveLoaded { get; private set; }

	/// <summary>The historical bookmarks of <c>common/bookmarks.txt</c>, in its order: the lobby's Historical Start rows.</summary>
	public System.Collections.Generic.IReadOnlyList<CBookmark> Bookmarks { get; private set; } = Array.Empty<CBookmark>();

	/// <summary>The bookmark chosen, whose date the world stands at; null with none chosen (docs/mechanics/bookmarks.md).</summary>
	public CBookmark? Bookmark { get; private set; }

	/// <summary>The Quick Start pressed, whose game <see cref="Prepare"/> makes in place of the lobby's; null for Play.</summary>
	private CQuickStart? _quickStart;

	/// <summary>
	/// The difficulty and game mode the Options panel has chosen: the world's as it is made or loaded, the panel's after,
	/// and the game's at Play (frontend.md, section 4).
	/// </summary>
	public Sim.Settings.CGamePlaySettings ChosenPlay { get; } = new();

	/// <summary>What the Options panel has the player hand to the AI, on the same terms as <see cref="ChosenPlay"/>.</summary>
	public Sim.Settings.CPlayerAutomation ChosenAutomation { get; } = new();

	public CGuiContext Gui => _gui;

	/// <summary>The program's keeper of the map's surface, which the game Play makes takes it from (CMapSurfaceKeeper).</summary>
	public required CMapSurfaceKeeper Surfaces { get; init; }

	public CDirectorySettings Directories => _directories;

	/// <summary>The player's settings, read as the front end comes up and written by the settings window's Apply.</summary>
	public CUserSettings Settings { get; private set; } = CUserSettings.Default;

	/// <summary>
	/// Play pressed for the country chosen: the game is to be made, by <see cref="Prepare"/> on the loading screen, and
	/// handed over (frontend.md, section 3; <c>docs/mechanics/loadingscreen.md</c>).
	/// </summary>
	public event Action<CCountryTag>? PlayRequested;

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="content">What the front end is made from, read under the loading screen.</param>
	/// <param name="first">Whether this is the front end the program came up on, which alone plays <c>--click=</c>.</param>
	public void Setup(CDirectorySettings directories, CFrontEndContent content, bool first = false)
	{
		_directories = directories;
		_content = content;
		_first = first;
	}

	private bool _first;
	private CFrontEndContent _content = null!;

	/// <summary>The main menu's version label and the lobby's checksum: ours, with the original's four letters.</summary>
	public string VersionLabel => Interface.Screens.CVersionText.Label(_content.Checksum);

	/// <summary>
	/// The world and its map made, its surface's meshes with it, before the front end enters the tree: a step of the loading
	/// screen of its own, so that the window draws a frame between it and the rest (<c>loadingscreen.md</c>, section 3).
	/// Done by entering where it was not called.
	/// </summary>
	public void PrepareMap()
	{
		if (_mapView is not null)
		{
			return;
		}
		var clock = Stopwatch.StartNew();
		Settings = _content.Settings;
		_localisation = _content.Localisation;
		_countries = _content.Countries;
		_map = _content.Map;
		_aiScripts = _content.AiScripts;
		_gui = _content.Gui;
		_mapCache = _content.Cache;
		_quadTiles = _content.Tiles;
		World = _content.World;
		SaveLoaded = false;
		ShowOnMap(World);
		Bookmarks = _content.Bookmarks;
		Bookmark = null;
		GD.Print($"Front end's map made in {clock.ElapsedMilliseconds} ms.");
	}

	public override void _Ready()
	{
		var clock = Stopwatch.StartNew();
		PrepareMap();
		Callable.From(() => GD.Print($"Front end ready in {clock.ElapsedMilliseconds} ms.")).CallDeferred();

		var layer = new CanvasLayer { Name = "Interface" };
		AddChild(layer);
		_menu = new CMainMenu();
		_menu.Setup(_gui, VersionLabel);
		// The Quick Start panel first where the settings say simple, as the original picks its first view (bookmarks.md,
		// section 4); the toggle keeps simple in step.
		_menu.Begin(Settings.Simple);
		_menu.Switched += quick => Settings = Settings with { Simple = quick };
		_menu.QuickStartPressed += QuickStart;
		layer.AddChild(_menu);
		_lobby = new CLobby();
		_lobby.Setup(this);
		layer.AddChild(_lobby);
		_lobbyOptions = new CLobbyOptions();
		_lobbyOptions.Setup(this);
		layer.AddChild(_lobbyOptions);
		_lobbyOptions.Changed += line => _lobby.Say(line);
		_options = new CGameOptionsWindow();
		_options.Setup(_gui, () => Settings, settings => Settings = settings, CGameOptionsWindow.DisplayModes(Settings),
			new[] { 2, 4, 8 }, (Settings.Width, Settings.Height, Settings.RefreshRate), CGameOptionsWindow.FrontEndWindowName);
		layer.AddChild(_options);
		_options.HideWindow();
		// Closing the settings window clears simple and comes back to the main menu, as the original's does.
		_options.CloseRequested += () =>
		{
			_options.HideWindow();
			_menu.ShowMainMenu();
			_menu.ShowPanel(true);
		};
		// The player's settings are written as Apply stores them, so the game the front end hands over reads them so.
		_options.Applied += settings =>
		{
			CUserSettingsWriter.WriteFile(settings, settings.Path ?? CUserSettingsReader.DefaultPath());
			_brightness?.SetBrightness(CGameOptions.Brightness(settings.Gamma));
		};
		_options.GammaPreviewed += scale => _brightness?.SetBrightness(scale);

		_menu.SinglePlayerPressed += OpenLobby;
		_menu.SettingsPressed += () =>
		{
			_menu.ShowPanel(false);
			_options.Open();
		};
		_menu.ExitPressed += () => GetTree().Quit();
		_lobby.BackPressed += ShowMenu;
		_lobby.ChosenChanged += LightChosen;

		var tooltips = new CTooltips();
		tooltips.Setup(_gui);
		AddChild(tooltips);
		_brightness = new Gfx.CBrightnessLayer();
		AddChild(_brightness);
		_brightness.SetBrightness(CGameOptions.Brightness(Settings.Gamma));

		ShowMenu();
	}

	/// <summary>
	/// The front end shown, the loading screen gone: the developer's clicks played, by the first front end alone - after the
	/// screen, which would take them.
	/// </summary>
	public void Begin()
	{
		if (_first)
		{
			RunClicks();
		}
	}

	/// <summary>The localisation the front end was made with, which the loading screen writes its lines from.</summary>
	public CLocalisation Localisation => _localisation;

	/// <summary>
	/// The Lua goes with the front end unless a game took it. Play takes the front end out of the tree before
	/// <see cref="Prepare"/> hands the Lua to the game, so a front end that has handed it over keeps hold of it till then.
	/// </summary>
	public override void _ExitTree()
	{
		if (_handedOver)
		{
			return;
		}
		_aiScripts?.Dispose();
		_aiScripts = null;
	}

	/// <summary>The main menu on its backdrop, the lobby hidden; a loaded save is let go and the world is the start again.</summary>
	public void ShowMenu()
	{
		if (SaveLoaded)
		{
			MakeWorld();
		}
		_lobby.Close();
		if (_lobbyOptions is not null)
		{
			_lobbyOptions.Visible = false;
		}
		_menu.Visible = true;
		_menu.ShowPanel(true);
		if (_mapView is not null)
		{
			_mapView.ClearSelection();
		}
	}

	/// <summary>Single Player: the main menu hidden and the lobby open on the start (frontend.md, section 3).</summary>
	private void OpenLobby()
	{
		_menu.Visible = false;
		_lobby.Open();
		ShowOptions();
	}

	/// <summary>
	/// The Options panel shown with the world's choices, as the original's shows it as the lobby opens and after each save
	/// loaded (<c>reversing/findings/FINDINGS-frontend.md</c>, C.1 and C.3).
	/// </summary>
	private void ShowOptions()
	{
		ChosenPlay.CopyFrom(World.GamePlay);
		ChosenAutomation.CopyFrom(World.Automation);
		if (_lobbyOptions is not null)
		{
			_lobbyOptions.Refresh();
			_lobbyOptions.Visible = true;
		}
	}

	/// <summary>
	/// The world at the defines' start date, made as for a new game up to its setting-up, and shown on the map, no bookmark
	/// chosen: what the start's tab makes of a save's world, as the original's reset does (bookmarks.md, section 2).
	/// </summary>
	public void MakeWorld()
	{
		MakeWorldAt(null);
		Bookmark = null;
	}

	/// <summary>
	/// A bookmark chosen: the world made at its date, held between the defines' start and end dates, as the original's
	/// lobby holds it, and shown on the map - unless the world from no save stands at that hour already (bookmarks.md,
	/// section 2).
	/// </summary>
	public void ChooseBookmark(CBookmark bookmark)
	{
		long first = (long)World.StartDate.DayNumber * CGameState.HoursPerDay;
		long last = (long)World.EndDate.DayNumber * CGameState.HoursPerDay;
		long hours = Math.Clamp(bookmark.Hours, first, Math.Max(first, last));
		Bookmark = bookmark;
		if (!SaveLoaded && World.TotalHours == hours)
		{
			return;
		}
		MakeWorldAt(hours);
	}

	/// <summary>
	/// The world made as for a new game up to its setting-up, at an hour or the defines' start date, from countries read
	/// afresh - the history is written onto them, so a world at another date cannot be made on the last one's - and shown.
	/// </summary>
	private void MakeWorldAt(long? hours)
	{
		var clock = Stopwatch.StartNew();
		World = BuildWorldAt(hours);
		SaveLoaded = false;
		ShowOnMap(World);
		GD.Print($"World made at {World.Date}.{World.Hour} in {clock.ElapsedMilliseconds} ms.");
	}

	/// <summary>The world at an hour, or at the defines' start date, on countries read afresh, which become the front end's.</summary>
	private CGameState BuildWorldAt(long? hours)
	{
		_countries = CCountries.Load(_directories, _localisation);
		CDate? date = hours is { } at ? CDate.FromDayNumber((int)(at / CGameState.HoursPerDay)) : null;
		CGameState world = CGameStateLoader.BuildWorld(_directories, _map, _localisation, _countries, CDiagnostics.Warn, _aiScripts, date);
		if (hours is { } exact)
		{
			world.SetClock(exact);
		}
		return world;
	}

	/// <summary>
	/// A Quick Start pressed: a game at its hour for its country, with no lobby - made by <see cref="Prepare"/> on the loading
	/// screen, as Play's is, with the five areas handed to the AI and the tech minister on sliders and research
	/// (<c>docs/mechanics/bookmarks.md</c>, section 4).
	/// </summary>
	private void QuickStart(CQuickStart start)
	{
		if (_aiScripts is null)
		{
			return;
		}
		_quickStart = start;
		var automation = new Sim.Settings.CPlayerAutomation();
		foreach (Sim.Settings.EAutomation area in new[]
		{
			Sim.Settings.EAutomation.Diplomacy, Sim.Settings.EAutomation.Production, Sim.Settings.EAutomation.Technology,
			Sim.Settings.EAutomation.Politics, Sim.Settings.EAutomation.Intelligence,
		})
		{
			automation.Flags[(int)area] = 1;
		}
		automation.Technology = 1;
		ChosenAutomation.CopyFrom(automation);
		_handedOver = true;
		PlayRequested?.Invoke(start.Tag);
	}

	/// <summary>A save loaded at once into the lobby's world, as the original's row loads it; answers whether it loaded.</summary>
	public bool LoadSave(string path)
	{
		try
		{
			// On countries read afresh, as every world the front end makes is.
			CCountries countries = CCountries.Load(_directories, _localisation);
			(CGameState state, CSaveReport _) = CSaveLoader.Load(_directories, _map, _localisation, countries, path,
				aiScripts: _aiScripts);
			World = state;
			_countries = countries;
		}
		catch (Exception failed) when (failed is IOException or InvalidDataException or FormatException)
		{
			CDiagnostics.Warn($"Could not load '{path}': {failed.Message}");
			return false;
		}
		SaveLoaded = true;
		ShowOnMap(World);
		ShowOptions();
		return true;
	}

	/// <summary>
	/// The political map of a world, a new view for each world, under the interface - its camera where the last view's
	/// stood: the original keeps one camera for the front end, so a save loaded leaves the lobby's fixed zoom as it was.
	/// </summary>
	private void ShowOnMap(CGameState state)
	{
		(Vector2 Centre, float Zoom)? kept = null;
		if (_mapView is not null)
		{
			kept = _mapView.View;
			RemoveChild(_mapView);
			_mapView.QueueFree();
		}
		_mapView = new CMapView { Name = "Map" };
		_mapView.Setup(_map, state);
		// A province clicked in the lobby clicks, as a country chosen there does in the original (sound.md, section 7).
		_mapView.Sounds = _gui.Sounds;
		// The quad cache read once for every world the front end shows.
		_mapCache ??= CMapView.OpenCache(_directories, _map);
		_quadTiles ??= CQuadCache.Load(_mapCache, problem => GD.PushWarning($"map: {problem}"));
		_mapView.SetupSurface(_gui, _directories, _mapCache, Settings.SimpleWater, Settings.Waves, Settings.RenderTrees, Surfaces,
			_quadTiles);
		// The countries to choose from by their colours: the original's front end sets the political style, where a game
		// starts in the terrain mode (reversing/findings/FINDINGS-frontend.md, A.2; minimap.md, section 3). Set before the
		// view enters the tree, so that its surface is made in that style alone and not in the terrain's first, which the
		// lobby never draws - 0.6 s of a still window (loadingscreen.md, section 3).
		_mapView.IsLobby = true;
		_mapView.Mode = CMapView.MapMode.Political;
		AddChild(_mapView);
		MoveChild(_mapView, 0);
		if (kept is { } view)
		{
			_mapView.View = view;
		}
		_mapView.ProvinceSelected += province =>
		{
			_lobby?.ProvinceClicked(province);
			LightChosen();
		};
	}

	/// <summary>Every province the chosen country owns lit on the map, in place of the one clicked.</summary>
	private void LightChosen()
	{
		_mapView?.Light(_lobby?.ChosenProvinces() ?? Array.Empty<int>());
	}

	/// <summary>The map put over a province, as a shield or a save's country does.</summary>
	public void CentreOn(int province)
	{
		_mapView?.CentreOn(province);
	}

	/// <summary>
	/// The camera where the original's front end puts it as the lobby opens: over the map's (2900, 1600), its rows counted
	/// from the bottom (<c>reversing/findings/FINDINGS-frontend.md</c>, A.2) - at a zoom of ours, measured on the
	/// maintainer's screenshot of the original's lobby, Europe from Ireland to Turkey across the screen.
	/// </summary>
	public void LookAtStart()
	{
		_mapView?.LookAt(StartCameraX, StartCameraRow, StartZoom);
	}

	private const float StartCameraX = 2900f;
	private const int StartCameraRow = 1600;
	private const float StartZoom = 1.43f;

	/// <summary>The player's name, the settings' <c>lastplayer</c>, which the lobby's players box and chat show.</summary>
	public string PlayerName => Settings.LastPlayer.Length > 0 ? Settings.LastPlayer : "Player";

	/// <summary>
	/// Play: the lobby put away and the game asked for, which <see cref="Prepare"/> makes on the loading screen
	/// (frontend.md, section 3).
	/// </summary>
	public void Launch(CCountryTag chosen)
	{
		if (_lobbyOptions is not null)
		{
			_lobbyOptions.Visible = false;
		}
		if (_aiScripts is null)
		{
			return;
		}
		// The Lua goes with the game from here: the front end leaves the tree as the game is made, and keeps it alive.
		_handedOver = true;
		PlayRequested?.Invoke(chosen);
	}

	/// <summary>
	/// The game Play asked for: a new game set up from the start's world with the country chosen played, or the save's world
	/// going on with the country chosen played in its player's place. Touches no engine object, so it runs on the loading
	/// screen's thread; what was read goes with it, and the session owns the Lua from then.
	/// </summary>
	public CPreparedGame Prepare(CCountryTag chosen)
	{
		if (_quickStart is { } quick)
		{
			// A Quick Start's world at its own hour, with no hold to the defines' range; its game chooses no victory conditions
			// and takes the lobby's options at a fresh world's, the automation its own (bookmarks.md, section 4).
			var clock = Stopwatch.StartNew();
			CGameState world = BuildWorldAt(quick.Hours);
			world.Automation.CopyFrom(ChosenAutomation);
			CGameState made = CGameStateLoader.StartNewGame(_directories, world, _countries, new[] { chosen },
				chooseVictoryConditions: false);
			GD.Print($"Quick Start as {chosen} at {made.Date}.{made.Hour} set up in {clock.ElapsedMilliseconds} ms.");
			_handedOver = true;
			return new CPreparedGame(made, _map, _localisation, _gui, _aiScripts!, _mapCache, _quadTiles);
		}
		CGameState state = World;
		// The Options panel's choices are the game's from Play, a loaded save's too (frontend.md, section 4).
		state.GamePlay.CopyFrom(ChosenPlay);
		state.Automation.CopyFrom(ChosenAutomation);
		if (SaveLoaded)
		{
			bool automated = state.Played.Any(played => state.AutomateTrade.Remove(played));
			state.Played.Clear();
			state.Played.Add(chosen);
			if (automated)
			{
				state.AutomateTrade.Add(chosen);
			}
			// Another player or other choices: each country's ministers by who does its work now, and its modifier by the
			// difficulty it is now played at.
			Sim.Settings.CAutomation.SetMinisters(state);
			Sim.Modifiers.CCountryModifiers.Rebuild(state);
		}
		else
		{
			var clock = Stopwatch.StartNew();
			state = CGameStateLoader.StartNewGame(_directories, World, _countries, new[] { chosen });
			GD.Print($"New game as {chosen} set up in {clock.ElapsedMilliseconds} ms.");
		}
		_handedOver = true;
		return new CPreparedGame(state, _map, _localisation, _gui, _aiScripts!, _mapCache, _quadTiles);
	}

	/// <summary>
	/// <c>--click=</c>, where no switch started a game: played into the front end - and <c>--screenshot=path.png</c> after
	/// it, what is on screen saved a few frames on and the program ended.
	/// </summary>
	private async void RunClicks()
	{
		string? clicks = CSession.UserArguments().FirstOrDefault(argument => argument.StartsWith("--click=", StringComparison.Ordinal));
		string? screenshot = CSession.UserArguments()
			.FirstOrDefault(argument => argument.StartsWith("--screenshot=", StringComparison.Ordinal));
		if (clicks is not null)
		{
			await new Debug.CClickScript(this).Run(clicks["--click=".Length..]);
		}
		if (screenshot is null || !IsInsideTree())
		{
			return;
		}
		for (int frame = 0; frame < 8; frame++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string path = screenshot["--screenshot=".Length..];
		Error error = GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print(error == Error.Ok ? $"Saved {path}" : $"Could not save {path}: {error}");
		GetTree().Quit();
	}
}
