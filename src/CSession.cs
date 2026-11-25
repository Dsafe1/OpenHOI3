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
using OpenHOI3.Interface.Sound;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Console;
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
/// so freeing it ends the game and adding another starts a new one - which is what
/// the front end's Play, a load and Resign do (<c>docs/mechanics/frontend.md</c>).
/// </summary>
public partial class CSession : Node
{
	private CDirectorySettings _directories = null!;
	private CAiScripts? _aiScripts;

	/// <summary>The Lua state a dialog asks an offer's chance of, this session's own and made when first asked.</summary>
	private CAiScripts? _displayScripts;
	private bool _displayScriptsFailed;
	private CMapView _mapView = null!;
	private CLocalisation _localisation = null!;

	/// <summary>The soundtrack, which the end-of-game screen and the console's <c>nextsong</c> ask; set before the node enters the tree.</summary>
	public Audio.CMusic? Music { get; init; }

	/// <summary>The localisation the game was made with, which the loading screen writes its lines from after a resign.</summary>
	public CLocalisation Localisation => _localisation;
	private CSimulation _simulation = null!;
	private CTopBar? _topBar;
	private COutliner? _outliner;

	/// <summary>The order of battle browser, which the unit panel and the outliner drag units and leaders into.</summary>
	private COobBrowser? _browser;
	private CProductionView? _production;
	private CTechnologyView? _technology;
	private CDiplomacyView? _diplomacy;
	private CPoliticsView? _politics;
	private CEspionageView? _espionage;
	private CTheatreView? _theatres;
	private CLedgerView? _statistics;
	private CMenuBackground? _menuBackground;
	private CWarfareView? _warfare;
	private CStrategicEffectsView? _strategicEffects;
	private CVictoryConditionsView? _victoryConditions;
	private CanvasLayer? _hud;
	private CGuiContext? _gui;

	/// <summary>The sound effects (<c>docs/mechanics/sound.md</c>): none where the game was made without them.</summary>
	private Audio.CSoundEffects? Sounds => _gui?.Sounds;
	private FrontEnd.CEndGameScreen? _endGame;
	private bool _gameWasFinished;
	private bool _victoryDialogShown;
	private CProvinceView? _provinceView;
	private CEventWindows? _events;
	private CDeployMenu? _deployMenu;
	private CAlerts? _alerts;
	private CUnitPanel? _unitPanel;

	/// <summary>The hour the player's empty units were last looked at.</summary>
	private long _emptyUnitsHour = -1;
	private COrdersWindow? _orders;
	private CCombatWindow? _combat;
	private CInGameMenu? _menu;
	private Gfx.CBrightnessLayer? _brightness;
	private Label _provinceLabel = null!;
	private Debug.CDebugMenu? _debugMenu;
	private CConsoleView? _console;
	private CChatView? _chat;
	private CTooltips? _tooltips;
	private bool _tooltipDebug;
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

	/// <summary>Where the province readout stands: at the screen's edge, and clear of the unit panel when that is up.</summary>
	private const float ReadoutAtEdge = 16f;
	private const float ReadoutBesidePanel = 420f;

	public CGameState State { get; private set; } = null!;

	/// <summary>The country the interface is about, or null when the install has none of that tag.</summary>
	public CCountry? Player { get; private set; }

	public CMap Map { get; private set; } = null!;

	/// <summary>The player's settings, read as the session starts: <c>docs/mechanics/settings.md</c>.</summary>
	public Content.Settings.CUserSettings Settings { get; private set; } = Content.Settings.CUserSettings.Default;
	// The game options window changes them, and the session writes them as it ends (gameoptions.md).

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="prepared">The game made ready, by the lobby or from the developer switches (<see cref="CSwitchedGame"/>).</param>
	/// <param name="carried">What the session before kept, where this one takes its game over.</param>
	/// <param name="replayFile">The recording <c>--replay=</c> plays or <c>--record=</c> writes, which the session then owns.</param>
	public void Setup(CDirectorySettings directories, FrontEnd.CPreparedGame prepared, Debug.CSessionCarry? carried = null,
		CReplayFile? replayFile = null)
	{
		_directories = directories;
		_prepared = prepared;
		_carried = carried;
		_replayFile = replayFile;
		// The player's settings, from the original's own settings.txt in the Documents folder (settings.md); what of them
		// is not supported is reported as it is read. Read here and not as the node enters the tree, because the music asks
		// for them from the moment the session is made, and the defaults would have it play at full volume until then.
		Settings = Content.Settings.CUserSettingsReader.Read(Content.Settings.CUserSettingsReader.DefaultPath(_directories));
		// An enhanced game's own, from the file beside it; a faithful game reads none and has the defaults, which are the original's.
		_enhancedGame = Content.Rules.CRulesReader.IsEnhanced(_directories);
		if (_enhancedGame)
		{
			EnhancedSettings = Content.Settings.CEnhancedSettingsFile.Read(Content.Settings.CEnhancedSettingsFile.DefaultPath(_directories));
		}
	}

	private Debug.CSessionCarry? _carried;

	/// <summary>
	/// Raised when the screens are to be made again around the same game: the console's <c>tag</c>, <c>reloadinterface</c> and
	/// <c>reloadfx</c>. Every screen is told who plays and what it is built from as it is made.
	/// </summary>
	public event System.Action<FrontEnd.CPreparedGame, Debug.CSessionCarry>? Replaced;

	/// <summary>Whether what the session holds has gone to the next, which then owns it.</summary>
	private bool _handedOver;

	/// <summary>Whether the next session is to read the interface again.</summary>
	private bool _readInterfaceAgain;

	private bool _wireframe;

	private FrontEnd.CPreparedGame _prepared = null!;

	/// <summary>
	/// Whether <c>--click=</c> is this session's to play: where the developer switches made the game, and not where the
	/// front end handed it over, having played the clicks itself. Every game has come prepared since 2026-11-11, so that
	/// can no longer be told from the prepared game, and until 2026-11-12 no game played them.
	/// </summary>
	public bool PlaysClicks { get; init; }

	/// <summary>
	/// The program's keeper of the map's surface: the main menu's is taken over where it was made from the same map and
	/// files, rather than made again (CMapSurfaceKeeper).
	/// </summary>
	public required CMapSurfaceKeeper Surfaces { get; init; }

	/// <summary>Raised when the player resigns: the game is to end and the front end come back (frontend.md, section 1).</summary>
	public event System.Action? Resigned;

	public override void _Ready()
	{
		// The game is made before the session, on the loading screen: by the lobby, or from the developer switches
		// (CSwitchedGame). What is left here makes the map and the interface, on the main thread, under that screen.
		ReadyPrepared(_prepared);
	}

	/// <summary>
	/// A game made ready: what it read is the session's now, the Lua among it, and whoever its state says plays plays it
	/// (frontend.md, section 3). The developer switches that make a game have been asked by then; the rest are asked here.
	/// </summary>
	private void ReadyPrepared(FrontEnd.CPreparedGame prepared)
	{
		_localisation = prepared.Localisation;
		Map = prepared.Map;
		_aiScripts = prepared.AiScripts;
		State = prepared.State;
		// Which engine runs the game, which an enhanced save records (docs/launcher.md, section 8).
		State.EngineVersion = Content.Launch.CEngineFile.VersionBeside(OS.GetExecutablePath());
		_simulation = new CSimulation(State);
		// From here on screens look at the game between its commands, and a look keeps nothing in it: who stands where and
		// the supply network are made by a running command alone (docs/multiplayer.md, section 3).
		State.ScreensLook = true;
		// And what a dialog asks the install's Lua - an offer's chance - it asks a Lua state of its own, made the first
		// time one is asked: never the state the AI's ministers run in (docs/multiplayer.md, section 12).
		State.DisplayScripts = () =>
		{
			if (_displayScripts is null && !_displayScriptsFailed)
			{
				var clock = Stopwatch.StartNew();
				try
				{
					_displayScripts = new CAiScripts(_directories);
					GD.Print($"Lua: a state for the dialogs' chances made in {clock.ElapsedMilliseconds} ms.");
				}
				catch (System.Exception failed)
				{
					// Without it the game's own state answers, as it did.
					_displayScriptsFailed = true;
					CDiagnostics.Warn($"lua: no state of its own for the dialogs' chances, so the game's answers: {failed.Message}");
				}
			}
			return _displayScripts;
		};
		// Between several machines the game's played countries are every player's, and this machine's is the one the host
		// gave it (docs/multiplayer.md, section 6).
		Player = Net?.Country is { } given ? State.Country(given)
			: State.Played.Count > 0 ? State.Country(State.Played.First()) : null;
		_enteredAs = _carried is { } carried ? carried.EnteredAs : Player?.Tag;
		_mapCache = prepared.Cache;
		_quadTiles = prepared.Tiles;
		ShowGame(prepared.Gui);
		// The host's or the client's session made around the simulation, after the pace a session before left was taken
		// up: a client's clock shows the host's from here on.
		if (Net is not null)
		{
			// As the host writes the game it sends: a client in step makes the same bytes and is sent nothing.
			Net.SaveNow = () => System.Text.Encoding.Latin1.GetBytes(Content.Saves.CSaveWriter.Write(State, _localisation.Localise));
		}
		Net?.Bind(_simulation);
		// The game's hours are started after each frame is drawn, and run beside the next (StartHours).
		RenderingServer.FramePostDraw += StartHours;
	}

	/// <summary>
	/// The country the game was entered with. The original gives the top bar and the country screens its pictures once, as
	/// the game is entered, and the console's <c>tag</c> does not give them again (<c>docs/mechanics/console.md</c>,
	/// section 3), so a session made again around the game keeps the first one's.
	/// </summary>
	private CCountryTag? _enteredAs;

	/// <summary>The map cache and its quad tiles as read before the session, handed on to the next session made around the game.</summary>
	private CMapCache? _mapCache;
	private IReadOnlyList<CQuadTile>? _quadTiles;

	/// <summary>The map, the interface and the developer switches, once the game and its simulation are made.</summary>
	private void ShowGame(CGuiContext gui)
	{
		_mapView = new CMapView { Name = "Map", PlanSigns = new CBattlePlanSigns { Name = "BattlePlanSigns" } };
		_mapView.Setup(Map, State);
		_mapView.Player = Player?.Tag;
		// Each region's colour, handed out once from interface/colors.txt: the region and theatre modes paint with them.
		IReadOnlyList<CColor> interfaceColours = CInterfaceColorsReader.Read(_directories);
		_mapView.RegionColours = CRegionMapColours.Assign(State.Regions, interfaceColours);
		_mapView.ContinentColours = CRegionMapColours.AssignContinents(Map.ContinentNames, interfaceColours);
		_mapView.SetupCounters(gui, CCounterTypes.Load(_directories), CBillboardTypes.Load(_directories),
			CArrowTypes.Load(_directories));
		// The player's settings the counters are drawn by, as they are read at start and as Apply changes them.
		if (_mapView.Counters is { } drawn)
		{
			drawn.CounterScale = Settings.CounterScale;
			drawn.HierarchyShown = Settings.RenderHierarchy;
			drawn.ShowUnitNames = UnitNamesShown(Settings);
		}
		CMapCache cache = _mapCache ??= CMapView.OpenCache(_directories, Map);
		_quadTiles ??= CQuadCache.Load(cache, problem => GD.PushWarning($"map: {problem}"));
		_mapView.SetupSurface(gui, _directories, cache, Settings.SimpleWater, Settings.Waves,
			Settings.RenderTrees || UserArguments().Contains("--trees"), Surfaces, _quadTiles);
		AddChild(_mapView);

		// F11 and F12, the original's two pictures (docs/mechanics/screenshots.md).
		var pictureKeys = new Screenshots.CPictureKeys { Name = "PictureKeys" };
		pictureKeys.Setup(_directories, _localisation.Localise, () => GameBusy, playersOnly => new Screenshots.CMapShot(
			Interface.Screenshots.CScreenshotNames.MapStem(Player?.Tag.Value ?? "---", Net is not null, State.Date, State.Hour),
			Map.Width, Map.Height, Interface.Screenshots.CMapPicture.Make(State, Map, playersOnly)));
		AddChild(pictureKeys);

		BuildHud(gui);
		// The readout is of the province selected, not of the one under the mouse: it is
		// long, and following the mouse it jumped about.
		_mapView.ProvinceSelected += ShowProvince;

		_mapView.RenderLayers = Settings.RenderTrees || UserArguments().Contains("--trees") ? 0xFFFF : 0xFFFE;
		if (_carried is { } carried)
		{
			TakeUp(carried);
		}
		else if (Player is { ActingCapital: > 0 } played)
		{
			// A game opens over the player's capital, at the height the original's camera starts at - a new game and a
			// loaded one alike (decided on 2026-11-12). Before the map modes are prepared, so that the view
			// the loading screen makes ready is the one the game opens on.
			_mapView.CentreOnFrom(played.ActingCapital, CMapCamera.StartHeight);
		}
	}

	/// <summary>
	/// The loading screen's last steps, the map shown under it: every map mode's surface made, and the first frames drawn
	/// until they come at their ordinary pace, so that neither stops the game once it is shown
	/// (<c>docs/mechanics/loadingscreen.md</c>).
	/// </summary>
	public async System.Threading.Tasks.Task PrepareMapModes(Loading.CLoadingScreen screen)
	{
		screen.Say(Loading.CLoadingSteps.MapModes);
		await screen.Drawn();
		var clock = Stopwatch.StartNew();
		while (_mapView.Surface3D?.PrepareNow() == true)
		{
			await screen.Drawn();
		}
		await screen.Settled();
		GD.Print($"Map modes prepared in {clock.ElapsedMilliseconds} ms.");
	}

	/// <summary>
	/// The game shown, the loading screen gone: the developer switches run - after it, so that the clicks they send reach the
	/// game - where the session does not take a game over from the one before.
	/// </summary>
	public void Begin()
	{
		if (_carried is null || SwitchesAgain)
		{
			RunDeveloperArguments();
		}
	}

	/// <summary>
	/// Whether the developer switches are run in a session that takes another's place: a playback gone on into its next
	/// recording, whose days the switches ask for (docs/replay.md, section 10).
	/// </summary>
	public bool SwitchesAgain { get; init; }

	/// <summary>
	/// This machine's part in a game between several machines, which the boot scene keeps across the sessions of it; null
	/// in a game of one. Set before the node enters the tree.
	/// </summary>
	public Multiplayer.CNetGame? Net { get; init; }

	/// <summary>What shows what the game between machines is waiting for; made when there is first something to say.</summary>
	private Label? _netStatus;

	/// <summary>What was last said of it in the log.</summary>
	private string? _netStatusSaid;

	/// <summary>What there is to say of it this frame, and since when, in milliseconds.</summary>
	private string _netStatusNow = "";
	private ulong _netStatusSince;

	private readonly bool _netLog = UserArguments().Contains("--netlog");
	private CDate? _netLogged;
	private string _netStands = "";

	/// <summary>Whether <c>--netpart=</c> has done its one disturbance; kept for the whole run, the session being made again as the game is sent.</summary>
	private static bool _netPartDone;

	/// <summary>Whether <c>--say=</c> has typed its lines, and the hour this session's game stood at as it was entered.</summary>
	private static bool _saidDone;
	private long _saidFrom = long.MaxValue;

	/// <summary>
	/// A frame of a game between several machines, the game being this thread's to read: a host sends the game where a
	/// machine waits to join or has parted; else the lines of play are taken, and a client runs the hours that have
	/// come. Answers those hours, or -1 where the game on screen is over, another being loaded in its place.
	/// </summary>
	private int NetFrame(Multiplayer.CNetGame net)
	{
		if (net.Loading)
		{
			return -1;
		}
		// A machine that would join is shown the game as it stands, for the map it chooses its country on: saved here
		// and handed over, the playing going on (docs/multiplayer.md, section 11).
		if (net is Multiplayer.CNetHost { ViewWanted: true } shows)
		{
			shows.SetView(System.Text.Encoding.Latin1.GetBytes(Content.Saves.CSaveWriter.Write(State, _localisation.Localise)));
		}
		if (net is Multiplayer.CNetHost { ResendWanted: true } host)
		{
			// Every order given runs first, so that the save holds it.
			_simulation.Pump();
			if (host.SendGame(State, () => System.Text.Encoding.Latin1.GetBytes(
				Content.Saves.CSaveWriter.Write(State, _localisation.Localise))))
			{
				return -1;
			}
		}
		// The chat: a faction channel's line goes to the players of that faction's countries as they now stand, and this
		// machine's player is in his own country's channel.
		net.ChannelOf ??= tag => Interface.Chat.CChatChannels.OfFaction(State.Country(tag)?.Faction);
		int hours = net.Frame();
		if (Player is not null)
		{
			net.Chat.SetFactionChannel(Interface.Chat.CChatChannels.OfFaction(Player.Faction));
		}
		_chat?.SetUsers(net.PlayUsers, net.PlayUsersVersion);
		net.LogChat = _netLog;
		// --say=text types a line into the chat once, a few days into the game played, as the box would take it: a
		// slash command among them. For a check by processes, a client started by a switch playing no clicks.
		if (_saidFrom == long.MaxValue && net.PlayUsers.Count > 1)
		{
			_saidFrom = State.TotalHours;
		}
		if (!_saidDone && Argument("--say=") is { } say && _saidFrom != long.MaxValue && State.TotalHours - _saidFrom > 72)
		{
			_saidDone = true;
			foreach (string typed in say.Split('|'))
			{
				(Interface.Chat.EChatTyped kind, string target, string text) = net.Chat.Read(typed, net.PlayUsers.Select(user => user.Name));
				if (kind == Interface.Chat.EChatTyped.Line)
				{
					net.SayInPlay(net.Chat.Current, text);
				}
				else if (kind == Interface.Chat.EChatTyped.Whisper)
				{
					net.Whisper(target, text);
				}
				GD.Print($"Multiplayer: --say, '{typed}' read as {kind} {target} {text}.");
			}
		}
		ShowNetStatus(net.Status);
		// --netpart=1936.2.10 on a client puts its game out of step with the host's from that day, once in the run: a
		// little manpower its player's country never had. The next midnight's hash then differs, and the host sends the
		// game again - which nothing else brings about to be tried (docs/multiplayer.md, section 6).
		if (!net.IsHost && !_netPartDone && Argument("--netpart=") is { } partAt && CDate.TryParse(partAt, out CDate from)
			&& State.Date >= from && Player is not null)
		{
			_netPartDone = true;
			Player.Manpower += CFixedPoint.One;
			GD.Print($"Multiplayer: --netpart, this game put out of step on {State.Date}.");
		}
		// --netlog also says who plays and whether the clock stands, whenever either changes: a machine gone shows so.
		if (_netLog)
		{
			string stands = string.Join(",", State.Played.Select(tag => tag.Value)) + (_simulation.Pace.Paused ? " paused" : " running")
				+ (net.Primary ? ", primary here" : ", not primary here");
			if (stands != _netStands)
			{
				_netStands = stands;
				GD.Print($"Multiplayer: played {stands} ({State.Date}, {State.Hour}:00).");
			}
		}
		// --netlog says each new day in the log, and on a client how many midnights its hash has been the host's.
		if (_netLog && State.Date != _netLogged)
		{
			_netLogged = State.Date;
			GD.Print($"Multiplayer: {State.Date}, {Player?.Tag.Value} played here" +
				(net is Multiplayer.CNetClient client ? $"; {client.HashesAlike} midnights the host's hash." : "."));
		}
		return net.Loading ? -1 : hours;
	}

	/// <summary>The line under the top bar that says who is waited for or that a game is being sent. *Ours.*</summary>
	private void ShowNetStatus(string text)
	{
		// Said only once it has stood a second: at the top speed the host waits a frame or two for a client all the time,
		// which is the window at work and nothing a player need be told.
		ulong now = Time.GetTicksMsec();
		if (text != _netStatusNow)
		{
			_netStatusNow = text;
			_netStatusSince = now;
		}
		if (text.Length > 0 && now - _netStatusSince < 1000)
		{
			text = "";
		}
		if (text != _netStatusSaid)
		{
			_netStatusSaid = text;
			GD.Print(text.Length > 0 ? $"Multiplayer: {text} ({State.Date}, {State.Hour}:00)." : $"Multiplayer: playing on ({State.Date}, {State.Hour}:00).");
		}
		if (_netStatus is null)
		{
			if (text.Length == 0 || _hud is null)
			{
				return;
			}
			_netStatus = new Label
			{
				Name = "NetStatus",
				Position = new Vector2(0, 130),
				Size = new Vector2(1920, 30),
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			_netStatus.AddThemeFontSizeOverride("font_size", 22);
			_netStatus.AddThemeColorOverride("font_outline_color", Colors.Black);
			_netStatus.AddThemeConstantOverride("outline_size", 6);
			_hud.AddChild(_netStatus);
		}
		if (_netStatus.Text != text)
		{
			_netStatus.Text = text;
		}
	}

	/// <summary>
	/// The game written to the player's saves as the host is lost, so that nothing played is lost with it
	/// (docs/multiplayer.md, section 6): `Multiplayer_` and the name the save dialog would offer. Written as this
	/// machine's player's - a save names one player, and the game is over here, so who is first among the played is
	/// put right for it. Answers the file, or null where it could not be written.
	/// </summary>
	public string? SaveAsTheHostIsLost()
	{
		if (Player is null)
		{
			return null;
		}
		CollectHours(finish: true);
		var others = State.Played.Where(tag => tag != Player.Tag).ToList();
		State.Played.Clear();
		State.Played.Add(Player.Tag);
		foreach (CCountryTag other in others)
		{
			State.Played.Add(other);
		}
		string path = System.IO.Path.Combine(_directories.UserFolder, Content.Saves.CSaveFiles.FolderFor(_directories),
			"Multiplayer_" + Content.Saves.CSaveFiles.OfferedName(Player.Name, State.TotalHours) + ".hoi3");
		try
		{
			_simulation.MarkSaved();
			Content.Saves.CSaveFiles.Write(State, _localisation.Localise, path);
			return path;
		}
		catch (System.Exception failed) when (failed is System.IO.IOException or System.UnauthorizedAccessException)
		{
			CDiagnostics.Warn($"Failed to write save file {path}: {failed.Message}");
			return null;
		}
	}

	/// <summary>
	/// What a session of a game between machines leaves the one that plays the game loaded in its place: what was read
	/// to make it, which the next need not read again, and where the map and the clock stood.
	/// </summary>
	public (FrontEnd.CPreparedGame Read, Debug.CSessionCarry Carry) LeaveForLoad()
	{
		(Vector2 centre, float zoom) = _mapView.View;
		var carried = new Debug.CSessionCarry(_console?.Lines.ToList() ?? new List<string>(), _console?.History.ToList() ?? new List<string>(),
			_console?.IsOpen == true, centre, zoom, _mapView.RenderLayers, _simulation.Clock.Paused, _simulation.Clock.Speed, null, _wireframe,
			_enteredAs);
		return (new FrontEnd.CPreparedGame(State, Map, _localisation, _gui!, _aiScripts!, _mapCache, _quadTiles), carried);
	}

	/// <summary>What the session before kept.</summary>
	private void TakeUp(Debug.CSessionCarry carried)
	{
		_mapView.View = (carried.Centre, carried.Zoom);
		_mapView.RenderLayers = carried.RenderLayers;
		_simulation.Pace.Set(carried.Paused, carried.Speed);
		// A session made around a game loaded for it is given its own recording, and takes none over.
		_replayFile = carried.ReplayFile ?? _replayFile;
		_wireframe = carried.Wireframe;
		GetViewport().DebugDraw = _wireframe ? Viewport.DebugDrawEnum.Wireframe : Viewport.DebugDrawEnum.Disabled;
		_console?.Restore(carried.ConsoleLines, carried.ConsoleHistory, carried.ConsoleOpen);
	}

	/// <summary>Everything made again around the same game, once the frame has ended.</summary>
	private void HandOver()
	{
		if (_gui is null || _aiScripts is null || _handedOver)
		{
			return;
		}
		_handedOver = true;
		// Hours running beside the frame finish before the game is handed to the session made around it.
		CollectHours(finish: true);
		State.ForgetListeners();
		CGuiContext gui = _readInterfaceAgain ? CGuiContext.Load(_directories, _localisation) : _gui;
		(Vector2 centre, float zoom) = _mapView.View;
		var carried = new Debug.CSessionCarry(_console?.Lines.ToList() ?? new List<string>(), _console?.History.ToList() ?? new List<string>(),
			_console?.IsOpen == true, centre, zoom, _mapView.RenderLayers, _simulation.Clock.Paused, _simulation.Clock.Speed, _replayFile, _wireframe,
			_enteredAs);
		Replaced?.Invoke(new FrontEnd.CPreparedGame(State, Map, _localisation, gui, _aiScripts, _mapCache, _quadTiles), carried);
	}

	/// <summary>
	/// The three ways a game ends (<c>docs/mechanics/endgame.md</c>, section 2): the victory conditions, which raise the dialog and pause once; the end date, past which
	/// the same dialog comes once and the game is held paused for good; and a player left with no provinces, who goes straight to the end.
	/// </summary>
	private void CheckTheGamesEnd()
	{
		if (_gui is null || _endGame is not null || !_simulation.TakesCommands || Player is null)
		{
			return;
		}
		bool justFinished = State.GameFinished && !_gameWasFinished;
		_gameWasFinished = State.GameFinished;
		// The end date's test is strictly after, by the hour.
		bool pastTheEnd = State.Date > State.EndDate || (State.Date == State.EndDate && State.Hour > 0);
		if (justFinished || pastTheEnd)
		{
			ShowVictoryDialog();
			_simulation.Pace.Stop();
		}
		if (Player.Tag.Value != "REB" && Sim.Countries.CCountryLists.OwnedCount(State, Player.Tag) < 1)
		{
			ShowEndGame();
		}
	}

	/// <summary>The dialog comes once in a session, whichever way the game came to its end.</summary>
	private void ShowVictoryDialog()
	{
		if (_victoryDialogShown || _gui is null || _hud is null || Player is null)
		{
			return;
		}
		_victoryDialogShown = true;
		System.Func<string, string> localise = _gui.Localisation.Localise;
		CConfirmDialog? dialog = CConfirmDialog.Open(_hud, _gui, Interface.Screens.CEndGameText.DialogName,
			Interface.Screens.CEndGameText.DialogTitle(localise, State, Player), Interface.Screens.CEndGameText.DialogText(localise, State));
		if (dialog is not null)
		{
			dialog.Agreed += ShowEndGame;
		}
	}

	/// <summary>The end-of-game screen over everything, with the world stopped behind it until Exit.</summary>
	private void ShowEndGame()
	{
		if (_endGame is not null || _gui is null || _hud is null || Player is null)
		{
			Resigned?.Invoke();
			return;
		}
		_simulation.Pace.Stop();
		// The victory march where the player's faction won, the dies irae otherwise (docs/mechanics/music.md, section 4).
		(int winner, _) = CEndGameText.Result(State);
		Music?.PlayEnd(winner >= 0 && CEndGameText.Factions[winner] == Player.Faction);
		_mapView.Visible = false;
		foreach (Node child in _hud.GetChildren())
		{
			if (child is CanvasItem item)
			{
				item.Visible = false;
			}
		}
		_endGame = new FrontEnd.CEndGameScreen();
		_endGame.Setup(_gui, State, Player);
		_endGame.Exited += () => Resigned?.Invoke();
		_hud.AddChild(_endGame);
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
		_hud = layer;
		_gui = gui;
		_mapView.Sounds = gui.Sounds;
		_gameWasFinished = State.GameFinished;
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
			_theatres = new CTheatreView();
			_theatres.Setup(gui, _simulation, Player);
			_theatres.MapSelection = () => mapView.Units.Units;
			_theatres.HeadquartersChosen += ChooseUnit;
			_theatres.ProvinceChosen += province => mapView.CentreOn(province);
			layer.AddChild(_theatres);
			_statistics = new CLedgerView();
			_statistics.Setup(gui, _simulation, Player);
			_statistics.UnitChosen += ChooseUnit;
			_statistics.Opening += CloseCountryViews;
			layer.AddChild(_statistics);
			// The six screens' pictures are the country's the game was entered with, a tag giving none (console.md, section 3).
			foreach (CCountryView view in new CCountryView[] { _production, _technology, _diplomacy, _politics, _espionage, _theatres })
			{
				view.SkinCountry = _enteredAs;
			}
			_warfare = new CWarfareView();
			_warfare.Setup(gui, _simulation);
			layer.AddChild(_warfare);

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
			_alerts = new CAlerts { Multiplayer = Net is not null };
			_alerts.Setup(gui, Player, _simulation);
			layer.AddChild(_alerts);
			_alerts.Pressed += AlertPressed;
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
		// The theatre editor: the button opens and closes it on the map, and what it paints goes to the theatre as a command
		// (attaching.md, section 7).
		_unitPanel.TheatreEditing = () => _mapView.TheatreEditing;
		_unitPanel.TheatreEditingWanted += headquarters => _mapView.SetTheatreEditing(headquarters);
		_mapView.TheatreEdited += (theatre, provinces) => _simulation.Post(new Sim.Commands.CUpdateTheatreCommand(theatre, provinces));
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

			// The window two selected units reorganise their brigades in (reorganise.md).
			var reorganise = new CReorgWindow();
			reorganise.Setup(gui, _simulation, Player);
			reorganise.Selection = () => _mapView.Units.Units;
			layer.AddChild(reorganise);
			_unitPanel.ReorganiseWanted += reorganise.Open;
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
		if (Player is not null)
		{
			var cards = new CBrigadeCards();
			cards.Setup(gui, _simulation, Player);
			_unitPanel.BrigadeChosen += cards.Open;
			_unitPanel.UnitCardWanted += cards.OpenUnit;

			// The order of battle browser, with the unit panel moved aside for it (docs/mechanics/oobbrowser.md).
			var browser = new COobBrowser { Selection = () => _mapView.Units.Units };
			browser.Setup(gui, _simulation, Player);
			layer.AddChild(browser);
			browser.UnitChosen += unit => ChooseUnit(unit);
			browser.CountryViewsClosing += CloseCountryViews;
			browser.WidthChanged += _ => LayOutLeftColumn();
			_unitPanel.BrowserWanted += browser.Toggle;
			_unitPanel.BrigadeDragStarted += browser.BeginDragSubUnit;
			// And from the panel's child rows and leader button, and the outliner's unit rows below (oobbrowser.md, section 6).
			_unitPanel.UnitDragStarted += browser.BeginDragUnit;
			_unitPanel.LeaderDragStarted += browser.BeginDragLeader;
			_browser = browser;
			layer.AddChild(cards);
		}
		_mapView.CombatSelected += ShowCombat;

		// With nobody played the bar has no country, and its buttons no screens to open.
		_topBar = new CTopBar();
		_topBar.Setup(gui, _simulation, Player);
		_topBar.SkinCountry = _enteredAs;
		layer.AddChild(_topBar);
		// The alert icons stand from 63 down, inside the top bar's window, which is 100 high: over the bar, or their upper
		// halves would not take a click. Only the icons take one; the rest of the row lets the mouse through.
		if (_alerts is not null)
		{
			// The alerts stand before the bar, so the bar's own place is the one just after it once the alerts are taken out.
			layer.MoveChild(_alerts, _topBar.GetIndex());
		}
		_topBar.ProductionPressed += () => Toggle(_production);
		_topBar.TechnologyPressed += () => Toggle(_technology);
		_topBar.DiplomacyPressed += () => Toggle(_diplomacy);
		_topBar.PoliticsPressed += () => Toggle(_politics);
		_topBar.IntelligencePressed += () => Toggle(_espionage);
		_topBar.TheatresPressed += () => Toggle(_theatres);
		// A screen's own close button is the same toggle in the original, the selection cleared with it (countryviews.md, section 1).
		foreach (CCountryView? view in new CCountryView?[] { _production, _technology, _diplomacy, _politics, _espionage, _theatres })
		{
			if (view is not null)
			{
				view.CloseRequested += () => Toggle(view);
			}
		}
		_topBar.StatisticsPressed += () => _statistics?.Toggle();
		_topBar.StrategicWarfarePressed += () => _warfare?.Toggle();

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
			if (_browser is { } dropTarget)
			{
				outliner.UnitDragStarted += dropTarget.BeginDragUnit;
				// And a deploy menu line, handed over once the pointer is in the browser (mapunits.md, section 6).
				if (_deployMenu is { } deploy)
				{
					deploy.BrowserOpen = () => dropTarget.IsOpen;
					deploy.OverBrowser = dropTarget.Holds;
					deploy.DraggedIntoBrowser += dropTarget.BeginDragDeployment;
				}
			}
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
			// screens the top bar opens - the original's, by an account of it on 2026-10-14
			// (docs/mechanics/countryviews.md).
			_events = new CEventWindows();
			_events.Setup(gui, _simulation, Player);
			_events.Shows = () => Net is not { Primary: false };
			layer.AddChild(_events);
			layer.MoveChild(_events, _production!.GetIndex());

			// The windows a mod defines for an enhanced game: a menu of pages on Insert, drawn with Dear ImGui over
			// everything (docs/mechanics/modwindows.md). Their Lua runs in the screens' own Lua state, made when first
			// asked. The session's own child and not the HUD's: a page is drawn every frame, the frames the HUD is not
			// processed in among them, and reads the game only when it may (GameBusy).
			if (_enhancedGame)
			{
				_modWindowsView = new CModWindows();
				_modWindowsView.Setup(_localisation.Localise, ModWindows, () => GameBusy, _simulation, Player.Tag);
				AddChild(_modWindowsView);
				// The key that shows it and its look are the player's, kept with this engine's other settings; a page
				// that offers them changes them, and they are written as they change.
				_modWindowsView.Apply(EnhancedSettings.WindowKey, EnhancedSettings.WindowTheme);
				_modWindowsView.SettingsChanged += (windowKey, windowTheme) =>
				{
					EnhancedSettings = EnhancedSettings with { WindowKey = windowKey, WindowTheme = windowTheme };
					Content.Settings.CEnhancedSettingsFile.WriteFile(EnhancedSettings,
						Content.Settings.CEnhancedSettingsFile.DefaultPath(_directories));
				};
				// A box of theirs typed into takes the keyboard from the game's own edit box (modwindows.md, section 7).
				gui.TypingElsewhere = () => _modWindowsView.IsTyping;
				// A picture a page draws, by its path below the install - a mod's own file by its folder, as the mod's Lua
				// names it, or a file of the game's found through the layers. Asked every frame, so what was found is kept.
				var pictures = new Dictionary<string, Texture2D?>(System.StringComparer.OrdinalIgnoreCase);
				_modWindowsView.Texture = path =>
				{
					if (!pictures.TryGetValue(path, out Texture2D? picture))
					{
						string absolute = System.IO.Path.Combine(_directories.GameRoot, path.Replace('\\', '/'));
						picture = System.IO.File.Exists(absolute) ? gui.Textures.LoadFromFile(absolute)
							: gui.Textures.Exists(path) ? gui.Textures.LoadFromGamePath(path) : null;
						pictures[path] = picture;
					}
					return picture;
				};
			}

			// Messages: the log on the bottom bar, and the popups beside the events.
			var messageLog = new CMessageLogView
			{
				ModeButtonsTakeRightClicks = _enhancedGame,
				// An enhanced game's: the three buttons with a list say so (custommapmodes.md, section 2).
				ModeButtonHint = button => _enhancedGame && ModeOfButton(button) is { } listed && CCustomMapModes.KindOf(listed) is not null
					? gui.Localisation.Localise(Interface.Map.CCustomMapLegend.ListHint)
					: null,
			};
			messageLog.Setup(gui);
			layer.AddChild(messageLog);
			layer.MoveChild(messageLog, 0);
			// The minimap over the bar, and the map modes' buttons on it (docs/mechanics/minimap.md).
			_minimap = new CMinimapView();
			_minimap.Setup(gui, _mapView);
			layer.AddChild(_minimap);
			layer.MoveChild(_minimap, 1);
			// The picture behind a country screen, over the map, the bar and the minimap (countryviews.md, section 6).
			_menuBackground = new CMenuBackground();
			_menuBackground.Setup(gui);
			layer.AddChild(_menuBackground);
			layer.MoveChild(_menuBackground, 2);
			_modeButtons = messageLog;
			messageLog.ModeButtonPressed += ChooseMapMode;
			// An enhanced game's native pages of the mod's: windows of the interface files its Lua fills (nativepages.md).
			if (_enhancedGame && Player is not null)
			{
				_nativePages = new CNativePages();
				_nativePages.Setup(gui, _simulation, ModWindows, () => GameBusy, HostScreenOpen, Player.Tag);
				layer.AddChild(_nativePages);
			}
			// An enhanced game's custom map modes: a right click on a mode's button lists what it may shade by.
			if (_enhancedGame)
			{
				var customModes = new CCustomMapModes();
				customModes.Setup(gui, _simulation, _mapView, EnhancedSettings.MapPalette, () => GameBusy);
				layer.AddChild(customModes);
				_customMapModes = customModes;
				customModes.PaletteChanged += palette =>
				{
					EnhancedSettings = EnhancedSettings with { MapPalette = palette };
					Content.Settings.CEnhancedSettingsFile.WriteFile(EnhancedSettings,
						Content.Settings.CEnhancedSettingsFile.DefaultPath(_directories));
				};
				messageLog.ModeButtonRightPressed += (button, where) =>
				{
					if (ModeOfButton(button) is { } host)
					{
						customModes.ToggleList(host, where);
					}
				};
			}
			var gotoBox = new CGotoBoxView();
			gotoBox.Setup(gui, _simulation.State);
			gotoBox.ProvinceChosen += province => _mapView.CentreOn(province);
			layer.AddChild(gotoBox);
			messageLog.GotoPressed += gotoBox.Show;

			// Battle plans: the window, what is drawn on the map and the bar's two buttons (docs/mechanics/battleplans.md).
			var planTypes = Content.BattlePlans.CPlanObjectTypes.Load(_directories);
			_mapView.PlanSigns?.Setup(Map, gui.Fonts, planTypes);
			var plans = new CBattlePlanWindow();
			plans.Setup(gui, _simulation.State, Player, _directories,
				System.IO.Path.Combine(_directories.UserFolder, "battle plans"), _mapView,
				Content.BattlePlans.CBattlePlanSettings.Load(_directories), planTypes);
			layer.AddChild(plans);
			foreach (Sim.Plans.CBattlePlan loaded in _simulation.State.LoadedBattlePlans)
			{
				plans.Book.Add(loaded);
			}
			_simulation.State.LoadedBattlePlans.Clear();
			_simulation.State.BattlePlansToSave = () => plans.Book.All(_simulation.State);
			messageLog.PlansTogglePressed += () => plans.ShowOnMap = !plans.ShowOnMap;
			messageLog.PlansOpenPressed += plans.Toggle;
			plans.ButtonsShown += messageLog.ShowPlans;
			messageLog.ZoomInPressed += () => _mapView.StepZoom(inwards: true);
			messageLog.ZoomOutPressed += () => _mapView.StepZoom(inwards: false);
			// The Strategic Effects window, which the bar's button by the minimap opens (docs/mechanics/strategiceffects.md).
			_strategicEffects = new CStrategicEffectsView();
			_strategicEffects.Setup(gui, _simulation, Player);
			layer.AddChild(_strategicEffects);
			layer.MoveChild(_strategicEffects, _production.GetIndex());
			messageLog.ModifiersPressed += _strategicEffects.Toggle;
			// The victory conditions' window, behind the bar's goals button and the alert; each shuts the other (endgame.md, section 5).
			_victoryConditions = new CVictoryConditionsView();
			_victoryConditions.Setup(gui, _simulation, Player);
			layer.AddChild(_victoryConditions);
			layer.MoveChild(_victoryConditions, _production.GetIndex());
			messageLog.GoalsPressed += _victoryConditions.Toggle;
			_victoryConditions.Opened += () => _strategicEffects.SetOpen(false);
			messageLog.ModifiersPressed += () =>
			{
				if (_strategicEffects.IsOpen)
				{
					_victoryConditions.Shut();
				}
			};
			var popups = new CMessagePopups();
			popups.Setup(gui, _simulation);
			layer.AddChild(popups);
			layer.MoveChild(popups, _production.GetIndex());
			CCountryTag player = Player.Tag;
			// A message is made from the game where the game raises it - on the hours' thread while they run beside the
			// frame - and shown on this one once they are collected. One whose type pauses stops the top speed's run of hours
			// after its hour, so the clock stops there (docs/mechanics/tick.md, section 2).
			void Tell(CMessage message)
			{
				// The console's debug nopopup: nothing at all, the log too.
				if (_simulation.State.NoPopups)
				{
					return;
				}
				_simulation.OnMain(() => Show(message), mayStopTheClock: message.Type.PausePopup);
			}
			_mapView.MapTextSpeed = () => _simulation.Pace.Speed;
			void Show(CMessage message)
			{
				if (message.Type.Log)
				{
					messageLog.Add(message);
				}
				if (message.Type.PopsUp)
				{
					popups.Open(message);
				}
				// Written on the map where its type says so: over its province, or with none over the first country's
				// acting capital, else the second's (messages.md, section 6).
				if (message.Type.OnMap)
				{
					int province = message.Province > 0 ? message.Province
						: _simulation.State.Country(message.FirstCountry)?.ActingCapital is > 0 and var first ? first
						: _simulation.State.Country(message.SecondCountry)?.ActingCapital ?? 0;
					_mapView.ShowMessageText(province, CMessageText.Text(gui.Localisation.Localise, message, "_MAP"));
				}
				// The original stops the clock of a game of one player, and of no other.
				if (message.Type.PausePopup)
				{
					_simulation.Pace.PauseByItself();
				}
			}
			_simulation.State.EventAnswered += (answered, scope, option) => Tell(CMessages.EventAnswered(
				gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, answered, scope, option));
			_simulation.State.DecisionTaken += (taken, scope) => Tell(CMessages.DecisionTaken(
				gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, taken, scope));
			popups.Goto = GoToMessage;
			messageLog.RowPressed += message => GoToMessage(message);
			messageLog.RowRightPressed += message => popups.OpenSettings(message.Type);
			_simulation.State.CombatBegan += combat =>
			{
				if (CMessages.CombatBegan(gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, combat) is { } message)
				{
					Tell(message);
				}
			};
			// A battle decided and a bombing ended (messages.md, sections 9.1 and 9.2).
			_simulation.State.CombatConcluded += (combat, attackerLost) =>
			{
				if (CBattleOverMessages.BattleOver(gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, combat, attackerLost) is { } message)
				{
					Tell(message);
				}
			};
			_simulation.State.BombingEnded += bombing =>
			{
				if (CBattleOverMessages.BombingEnded(gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, bombing) is { } message)
				{
					Tell(message);
				}
			};
			_simulation.State.UnitArrived += unit =>
			{
				if (CMessages.UnitArrived(gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, unit) is { } message)
				{
					Tell(message);
				}
			};
			// A raid on a convoy: ships lost, ships sunk, a convoy too strong (navalmissions.md, section 8.2).
			_simulation.State.ConvoyRaidMessaged += raid =>
			{
				if (CMessages.ConvoyRaid(gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, raid) is { } message)
				{
					Tell(message);
				}
			};
			// A patrol's new shadow (navalmissions.md, section 3.1).
			_simulation.State.PatrolTracked += track =>
			{
				if (CMessages.PatrolTrack(gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, track) is { } message)
				{
					Tell(message);
				}
			};
			_simulation.State.PactMessaged += pact =>
			{
				if (CMessages.Pact(gui.Localisation.Localise, gui.MessageTypes, _simulation.State, player, pact) is { } message)
				{
					Tell(message);
					// An answer to an offer and a revolt sound for the side told the first key (sound.md, section 7).
					string sound = CMessages.PactSound(pact, player);
					_simulation.OnMain(() => Sounds?.PlayUnlessPlaying(sound));
				}
			};
			// The simulation's other sounds, each for one country's player (sound.md, section 7).
			_simulation.State.SoundCued += (sound, heardBy) =>
			{
				if (heardBy == player)
				{
					_simulation.OnMain(() => Sounds?.PlayUnlessPlaying(sound));
				}
			};
		}
		_simulation.DayPassed += () => _unitsMoved = true;
		_simulation.DayPassed += DecideAutosave;
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
		if (gui is not null)
		{
			_console = new CConsoleView();
			_console.Setup(gui, new Interface.Console.CConsoleRunner(new CConsoleHost(this)));
			layer.AddChild(_console);
		}
		// The chat in play, made only in a game between machines, as the original's is (docs/mechanics/chat.md). It reads
		// nothing of the game: the frame hands it who plays what and the player's channel (NetFrame).
		if (gui is not null && Net is { } chatting)
		{
			_chat = new CChatView { CountryOf = tag => State.Country(tag), OwnName = chatting.PlayerName };
			_chat.Setup(gui, chatting.Chat);
			_chat.Said += chatting.SayInPlay;
			_chat.Whispered += chatting.Whisper;
			layer.AddChild(_chat);
		}
		_debugMenu.AddSwitch("Province readout", false, on => _provinceLabel.Visible = on);
		// The original's console command debug fow, here for the countries played alone (intelligence.md, section 3;
		// compatibility.md, entry 21), as a command.
		_debugMenu.AddSwitch("Fog of war (debug fow)", State.FogOfWar, on => _simulation.Post(new CFogOfWarCommand(on)));
		// The original's console command cpaint, an editor switch left in the release (mapmodes.md, section 7).
		_debugMenu.AddSwitch("Continent painting (cpaint)", false, on => _mapView.ContinentPainting = on);

		// The in-game menu, over the other windows: the top bar's menu button and Escape with nothing selected open it,
		// and opening it lets the selection go and shuts the side menus and a country view (docs/mechanics/ingamemenu.md).
		// Its Game Options window holds the player's settings (docs/mechanics/gameoptions.md).
		var options = new CGameOptionsWindow();
		options.Setup(gui, () => Settings, settings => Settings = settings, DisplayModes(), new[] { 2, 4, 8 },
			(Settings.Width, Settings.Height, Settings.RefreshRate));
		options.SettingsPath = Content.Settings.CUserSettingsReader.DefaultPath(_directories);
		options.GammaPreviewed += scale => _brightness?.SetBrightness(scale);
		options.Applied += ApplySettings;
		// An enhanced game's own options, on the Game tab below the autosave frequency (gameoptions.md, section 5).
		if (_enhancedGame)
		{
			options.EnhancedPath = Content.Settings.CEnhancedSettingsFile.DefaultPath(_directories);
			options.EnhancedApplied += ApplyEnhancedSettings;
		}
		ApplyEnhancedSettings(EnhancedSettings);
		_menu = new CInGameMenu();
		_menu.Setup(gui, _directories, _simulation, Player, options);
		layer.AddChild(_menu);
		// The arrow keys scroll the map only where nothing before it takes them (interface.md, section 8; ingamemenu.md,
		// section 1). The console goes through its history on Up and Down.
		bool mainWindow() => _menu.IsOpen || _production?.IsOpen == true || _technology?.IsOpen == true
			|| _diplomacy?.IsOpen == true || _politics?.IsOpen == true || _espionage?.IsOpen == true || _theatres?.IsOpen == true;
		_mapView.ScrollKeysHeld = () => mainWindow() || _modWindowsView?.IsTyping == true || _modWindowsView?.HasKeyboard == true
			? (true, true)
			: (gui.KeyboardFocus is { HasKeyboard: true }, _console?.IsOpen == true);
		_mapView.WheelHeld = mainWindow;
		_mapView.CounterDistance = () => Settings.CounterDistance;
		_mapView.CameraSettings = () => (Settings.ZoomSpeed, Settings.UseLegacyCamera);
		_topBar.MenuPressed += _menu.Toggle;
		_menu.Resigned += ShowEndGame;
		_menu.Opening += () =>
		{
			_deployMenu?.Close();
			_mapView.ClearSelection();
			foreach (CCountryView? view in new CCountryView?[] { _production, _technology, _diplomacy, _politics, _espionage, _theatres })
			{
				view?.Close();
			}
		};

		// Every tooltip, over everything else.
		var tooltips = new CTooltips();
		_tooltips = tooltips;
		tooltips.Setup(gui);
		// Over a counter the map's tooltip is the unit's own, else the province's (tooltips.md, section 7.1).
		tooltips.MapTooltip = _ => _mapView.HoveredBase is { } hoveredBase
			? new CTooltip { Text = CBaseTooltip.Build(_localisation.Localise, State, hoveredBase.Province, hoveredBase.Kind, Player?.Tag) }
			: _mapView.HoveredUnit is { } unit
				? new CTooltip { Text = CUnitCounterTooltip.Build(_localisation.Localise, State, unit, Player?.Tag) }
				: ProvinceTooltip(_mapView.HoveredProvince);
		tooltips.MapTooltipAnchor = () => _minimap?.Corner;
		AddChild(tooltips);

		// The gamma setting over all of it, and what else of the settings shows at once; the multisampling the original
		// takes at its start (gameoptions.md, section 3).
		_brightness = new Gfx.CBrightnessLayer();
		AddChild(_brightness);
		ApplySettings(Settings);
		GetViewport().Msaa2D = Settings.MultiSampling switch
		{
			>= 8 => Viewport.Msaa.Msaa8X,
			>= 4 => Viewport.Msaa.Msaa4X,
			>= 2 => Viewport.Msaa.Msaa2X,
			_ => Viewport.Msaa.Disabled,
		};
	}

	/// <summary>
	/// What of the player's settings takes effect at once: the gamma, the water, the counters' scale and names and the
	/// hierarchy's lines on the map.
	/// </summary>
	private void ApplySettings(Content.Settings.CUserSettings settings)
	{
		_brightness?.SetBrightness(CGameOptions.Brightness(settings.Gamma));
		_mapView.SimpleWater = settings.SimpleWater;
		if (_mapView.Counters is { } counters)
		{
			counters.HierarchyShown = settings.RenderHierarchy;
			counters.CounterScale = settings.CounterScale;
			counters.ShowUnitNames = UnitNamesShown(settings);
		}
	}

	/// <summary>The settings' <c>show_unit_names</c>, unless <c>--unitnames=</c> says otherwise for the run.</summary>
	private static bool UnitNamesShown(Content.Settings.CUserSettings settings)
	{
		IEnumerable<string> arguments = UserArguments();
		return arguments.Contains("--unitnames=no") ? false : arguments.Contains("--unitnames=yes") || settings.ShowUnitNames;
	}

	/// <summary>The game options' resolutions: <see cref="CGameOptionsWindow.DisplayModes"/>.</summary>
	private List<CDisplayMode> DisplayModes() => CGameOptionsWindow.DisplayModes(Settings);

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
			CMapView.MapMode.Infrastructure => EMapTooltipMode.Infrastructure,
			CMapView.MapMode.VictoryPoints => EMapTooltipMode.VictoryPoints,
			CMapView.MapMode.Strength => EMapTooltipMode.Strength,
			CMapView.MapMode.Resources => EMapTooltipMode.Resources,
			CMapView.MapMode.SimplifiedTerrain => EMapTooltipMode.SimplifiedTerrain,
			CMapView.MapMode.Region => EMapTooltipMode.Region,
			CMapView.MapMode.Diplomatic => EMapTooltipMode.Diplomatic,
			CMapView.MapMode.Intel => EMapTooltipMode.Intel,
			CMapView.MapMode.Theatre => EMapTooltipMode.Theatre,
			CMapView.MapMode.Naval => EMapTooltipMode.Naval,
			_ => EMapTooltipMode.Political,
		};
		return CProvinceTooltip.Build(_localisation.Localise, State, State.Province(provinceId), mode, Player?.Tag,
			_mapView.Units.First, _mapView.SelectedProvince, () => CSupplyNetworks.Refresh(State), _mapView.ContinentPainting, debug: _tooltipDebug);
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
	/// <summary>Does what a message's Goto does, answering whether its popup stays. See <c>docs/mechanics/messages.md</c>, section 4.1.</summary>
	private bool GoToMessage(CMessage message)
	{
		string type = message.Type.Name;
		switch (CMessageGotos.Of(type))
		{
			case EMessageGoto.Battle:
				bool air = type.StartsWith("AIR", System.StringComparison.Ordinal);
				return ShowFound(State.Combats.FirstOrDefault(combat => combat.Province == message.Province
					&& !Sim.Combat.CBombing.IsBombing(combat.Kind) && (!air || combat.Kind == ECombatKind.Air)));
			case EMessageGoto.Bombing:
				return ShowFound(Sim.Combat.CBombing.In(State, message.Province));
			case EMessageGoto.Province:
				CentreOnMessage(message);
				return true;
			case EMessageGoto.Capital:
				if (State.Country(message.FirstCountry) is { ActingCapital: > 0 } country)
				{
					_mapView.CentreOn(country.ActingCapital);
				}
				return true;
			case EMessageGoto.Invasion:
				return CentreOnMessage(message);
			case EMessageGoto.Unit:
				// The original's GotoUnitForMessage answers false whatever it found, so the popup closes. It finds the unit by
				// its number whoever holds it now - an expeditionary force returned is the second country's, not the first's.
				if (message.Unit != 0 && State.Countries.SelectMany(country => country.AllUnits())
					.FirstOrDefault(unit => unit.Id == message.Unit && !unit.IsRemoved) is { } found)
				{
					_mapView.ClearSelection();
					_mapView.SelectUnit(found);
					_mapView.CentreOn(found.Location);
				}
				return false;
			case EMessageGoto.Screen:
				_orders?.Close();
				_mapView.SelectUnit(null);
				_mapView.ClearSelection();
				if (type == CMessageGotos.AgreementExpired)
				{
					if (message.Province > 0)
					{
						_mapView.Select(message.Province);
					}
					Toggle(_diplomacy);
				}
				else if (CMessageGotos.OpensTechnology(type))
				{
					Toggle(_technology);
				}
				else if (type == CMessageGotos.BuildingDone)
				{
					CentreOnMessage(message);
				}
				return true;
			default:
				return false;
		}
	}

	private bool ShowFound(CCombat? combat)
	{
		if (combat is null)
		{
			return false;
		}
		ShowCombat(combat);
		_mapView.CentreOn(combat.Province);
		return true;
	}

	/// <summary>The map centred on the message's province, or else on the acting capital of its first country or its second, answering whether there was one.</summary>
	private bool CentreOnMessage(CMessage message)
	{
		int province = message.Province > 0 ? message.Province
			: State.Country(message.FirstCountry is { } first ? first : message.SecondCountry)?.ActingCapital ?? 0;
		if (province <= 0)
		{
			return false;
		}
		_mapView.CentreOn(province);
		return true;
	}

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

	/// <summary>
	/// A country screen opened, or shut where it is the one open - the original's one toggle for the top bar's buttons, the
	/// screens' close buttons, the alerts and the rest. Either way the side menus close and **the selection is cleared**, units
	/// and provinces alike, as the original's toggle ends (countryviews.md, section 1).
	/// </summary>
	private void Toggle(CCountryView? view)
	{
		if (view is null)
		{
			return;
		}
		_deployMenu?.Close();
		if (view.IsOpen)
		{
			view.Close();
			_mapView.ClearSelection();
			return;
		}
		_statistics?.Shut();
		foreach (CCountryView? other in new CCountryView?[] { _production, _technology, _diplomacy, _politics, _espionage, _theatres })
		{
			if (!ReferenceEquals(other, view))
			{
				other?.Close();
			}
		}
		view.Open();
		_mapView.ClearSelection();
	}

	/// <summary>
	/// Every country screen shut, as the Statistics screen does when it opens: through the original's toggle where one is
	/// open, so the selection is cleared then and only then (topscreens.md, section B.1).
	/// </summary>
	private void CloseCountryViews()
	{
		foreach (CCountryView? view in new CCountryView?[] { _production, _technology, _diplomacy, _politics, _espionage, _theatres })
		{
			if (view is { IsOpen: true })
			{
				Toggle(view);
			}
		}
	}

	/// <summary>
	/// The unit panel is up for the unit selected on the map, the deploy menu open or not: the original stands the two side by
	/// side, the order of battle browser between them (<c>docs/mechanics/mapunits.md</c>, section 6).
	/// </summary>
	private void ShowSelectedUnit()
	{
		_unitPanel?.ShowSelection(_mapView.Units.Units);
		LayOutLeftColumn();
	}

	/// <summary>
	/// The deploy menu at the screen's left, where it is open, the order of battle browser at its right edge, and the unit
	/// panel at the browser's - each pushed aside by those before it, as the original's screenshots stand them.
	/// </summary>
	private void LayOutLeftColumn()
	{
		float left = _deployMenu is { IsOpen: true } deploy ? deploy.Width : 0f;
		_browser?.SetLeft(left);
		_unitPanel?.SetLeft(left + (_browser?.TotalWidth ?? 0f));
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
	/// decided on 2026-10-09 - where the original's row in a unit's panel
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
		// Opening a battle's window closes the in-game menu, as its Close does (ingamemenu.md, section 2).
		_menu?.Close();
	}

	/// <summary>
	/// The battle window's retreat button: every unit of the player's in the battle, the attackers' first, sent where a
	/// beaten one goes - the original's move order to its retreat province - out of every battle it is in (combatview.md,
	/// section 2). A fleet is set breaking away.
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
			// A land or an air unit is sent where a beaten one goes, out of a battle or a bombing.
			var fallBack = new CFallBackCommand(Player.Tag, unit.Id);
			if (fallBack.MayExecute(State))
			{
				_simulation.Post(fallBack);
				_ordered = true;
			}
		}
	}

	/// <summary>The player's units left with no brigades go as the hours pass, but for one that is selected (reorganise.md, section 5).</summary>
	private void DisbandEmptyUnits()
	{
		if (Player is null || State.TotalHours == _emptyUnitsHour)
		{
			return;
		}
		_emptyUnitsHour = State.TotalHours;
		IReadOnlyList<CUnit> selected = _mapView.Units.Units;
		foreach (CUnit unit in Player.AllUnits().Where(unit => CEmptyUnits.IsToGo(unit) && !selected.Contains(unit)).ToList())
		{
			_simulation.Post(new CDisbandCommand(unit.Id));
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
	/// The picture up behind a country screen or the Statistics, and with a country screen the minimap down so that the
	/// picture shows where it was (<c>countryviews.md</c>, section 6).
	/// </summary>
	private void UpdateMenuBackground()
	{
		bool countryView = _production?.IsOpen == true || _technology?.IsOpen == true || _diplomacy?.IsOpen == true
			|| _politics?.IsOpen == true || _espionage?.IsOpen == true || _theatres?.IsOpen == true;
		bool shown = countryView || _statistics?.IsOpen == true;
		if (_menuBackground is not null && _menuBackground.Visible != shown)
		{
			_menuBackground.SetShown(shown);
		}
		if (_minimap is not null && _minimap.Visible == countryView)
		{
			_minimap.Visible = !countryView;
		}
	}

	/// <summary>
	/// An alert clicked (<c>docs/mechanics/alerts.md</c>, section 4): the orders window closed, the selection let go but for
	/// the two that go to a unit, and the view or the unit each one answers to. Six have no answer and only do the first.
	/// </summary>
	private void AlertPressed(Interface.Alerts.EAlert alert)
	{
		_orders?.Close();
		CUnit? kept = alert == Interface.Alerts.EAlert.DeployableUnits ? _mapView.Units.First : null;
		if (alert is not (Interface.Alerts.EAlert.BadSupply or Interface.Alerts.EAlert.LackOfTroops))
		{
			_mapView.ClearSelection();
		}
		if (alert != Interface.Alerts.EAlert.DeployableUnits)
		{
			_deployMenu?.Close();
		}
		switch (alert)
		{
			case Interface.Alerts.EAlert.DeployableUnits:
				foreach (CCountryView? view in new CCountryView?[] { _production, _technology, _diplomacy, _politics, _espionage, _theatres })
				{
					view?.Close();
				}
				ToggleDeployMenu();
				if (kept is not null)
				{
					_mapView.SelectUnit(kept);
				}
				break;
			case Interface.Alerts.EAlert.NationalDecisions or Interface.Alerts.EAlert.CanInviteToFaction:
				Toggle(_diplomacy);
				break;
			case Interface.Alerts.EAlert.WastedIc:
				Toggle(_production);
				break;
			case Interface.Alerts.EAlert.FreeTechSlots or Interface.Alerts.EAlert.TechFarAhead:
				Toggle(_technology);
				break;
			case Interface.Alerts.EAlert.PossibleLaw:
				Toggle(_politics);
				break;
			case Interface.Alerts.EAlert.LackOfTroops or Interface.Alerts.EAlert.BadSupply:
				if (Interface.Alerts.CAlertPass.NextUnit(_simulation.State, Player, alert, _mapView.Units.First) is { } unit)
				{
					_mapView.SelectUnit(unit);
					_mapView.CentreOn(unit.Location);
				}
				break;
			case Interface.Alerts.EAlert.VictoryConditions:
				if (_victoryConditions is { IsOpen: false })
				{
					_victoryConditions.Toggle();
				}
				break;
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

	/// <summary>
	/// The session's Lua goes with it - and the player's settings are written, as the original writes them every time a
	/// game is left (gameoptions.md, section 5).
	/// </summary>
	public override void _ExitTree()
	{
		RenderingServer.FramePostDraw -= StartHours;
		// Hours still running beside the frame finish before the game is let go or handed on.
		CollectHours(finish: true);
		Content.Settings.CUserSettingsWriter.WriteFile(Settings, Settings.Path ?? Content.Settings.CUserSettingsReader.DefaultPath(_directories));
		// The Lua kept for looking is this session's own, whoever has the game next.
		State.DisplayScripts = null;
		_displayScripts?.Dispose();
		_displayScripts = null;
		if (_handedOver)
		{
			return;
		}
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
		_owedSeconds += delta;
		WriteHeldAutosaves();
		// The hours run beside the last frames, where they are done: what they told is told now, on this thread.
		int hours = CollectHours(finish: false);
		double simulation = hours > 0 ? _simulation.LastRunMilliseconds : 0;
		bool busy = _simulation.IsBusy;
		HoldInterface(busy);
		if (busy)
		{
			// The game is the hours' until they are collected: the map's camera moves and nothing else reads it.
			_frameLog?.Frame(delta, 0, Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds, 0);
			return;
		}
		if (Net is { } net)
		{
			int came = NetFrame(net);
			if (came < 0)
			{
				// Another game is being loaded in this one's place: nothing more of this one runs.
				return;
			}
			hours += came;
		}
		ReplayHeldInput();
		DisbandEmptyUnits();
		UpdateMenuBackground();
		// The readout is refreshed once a frame and not once an hour: at the top speed a
		// frame holds thousands of hours. A replay that has run out of its recording goes no further.
		if (_replayEnded)
		{
			_simulation.Pace.Stop();
		}
		else if (hours > 0)
		{
			ShowProvince(_shownProvince);                       // its figures move with the days
		}
		ProcessAfterHours();
		CheckTheGamesEnd();
		if (_frameLog is { } log)
		{
			log.Frame(delta, simulation, Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds, hours);
			if (log.IsDone)
			{
				log.WriteSummary();
				_frameLog = null;
				GetTree().Quit();
			}
		}
	}

	private bool _debugSavesSaid;

	private CModWindows? _modWindowsView;
	private Content.Windows.IModPages? _modWindows;
	private bool _modWindowsFailed;

	/// <summary>
	/// The mod's own windows as its Lua gives them, made the first time they are asked for, over the Lua state the
	/// screens have for themselves; null where that state cannot be had, which is said once.
	/// </summary>
	private Content.Windows.IModPages? ModWindows()
	{
		if (_modWindows is null && !_modWindowsFailed)
		{
			if (State.DisplayScripts?.Invoke() is CAiScripts scripts)
			{
				_modWindows = new CLuaWindows(scripts, _directories, _localisation.Localise);
			}
			else
			{
				_modWindowsFailed = true;
				CDiagnostics.Warn("The mod's windows have no Lua to ask: the screens' Lua state could not be made");
			}
		}
		return _modWindows;
	}

	/// <summary>Whether the folders played make an enhanced game, which has options of its own (docs/enhanced.md).</summary>
	private bool _enhancedGame;

	/// <summary>An enhanced game's own settings, from <c>openhoi_settings.txt</c>; the defaults, which change nothing, in a faithful game.</summary>
	public Content.Settings.CEnhancedSettings EnhancedSettings { get; private set; } = Content.Settings.CEnhancedSettings.Default;

	/// <summary>What the extra autosaves remember between days: this session's, and no part of the game.</summary>
	private readonly Content.Saves.CExtraAutosaves _extraAutosaves = new();

	private readonly System.Diagnostics.Stopwatch _realTime = System.Diagnostics.Stopwatch.StartNew();

	/// <summary>
	/// The enhanced settings in force: the map's scrolling at the screen's edge - off too in a run a script drives,
	/// whose pointer lies wherever it was left - and where an event's window comes up.
	/// </summary>
	private void ApplyEnhancedSettings(Content.Settings.CEnhancedSettings settings)
	{
		EnhancedSettings = settings;
		bool scripted = UserArguments().Any(argument => argument.StartsWith("--click=") || argument.StartsWith("--screenshot="));
		_mapView.EdgeScrolling = settings.EdgeScrolling && !scripted;
		if (_events is not null)
		{
			_events.Place = settings.EventWindow;
		}
	}

	/// <summary>
	/// An enhanced game's two extra autosaves, asked as the day begins like the original's own and written beside it
	/// in files of their own (docs/mechanics/savegame.md, section 11.1). Where both they and the original's fall on one
	/// day, both are written.
	/// </summary>
	private void DecideExtraAutosaves()
	{
		if (!_enhancedGame)
		{
			return;
		}
		Content.Saves.EExtraAutosave kind = _extraAutosaves.Decide(EnhancedSettings, State.Date, _realTime.ElapsedMilliseconds);
		if (kind == Content.Saves.EExtraAutosave.None)
		{
			return;
		}
		HoldAutosave(Content.Saves.CExtraAutosaves.FilesOf(kind));
	}

	/// <summary>
	/// The autosave, as each day begins - the hour that turned it run whole: where the settings' frequency names today,
	/// the three files shifted along and the game written to the first at once, so that it holds the turn of the day
	/// whatever the speed, where the original's waits a frame and at speed holds an hour later (docs/compatibility.md,
	/// entry 16). With <c>debug_saves</c> set the original saves otherwise, which is not built: said once, and nothing
	/// saved. A failure is logged as the original logs it.
	/// </summary>
	private void DecideAutosave()
	{
		// Between several machines the autosave is the host's alone (docs/multiplayer.md, section 2).
		if (Net is { IsHost: false })
		{
			return;
		}
		// A game played back saves nothing: its autosaves would be written over the player's own.
		if (State.Replay is { IsReplaying: true })
		{
			return;
		}
		DecideExtraAutosaves();
		if (Settings.DebugSaves != 0)
		{
			if (!_debugSavesSaid)
			{
				_debugSavesSaid = true;
				CDiagnostics.Warn($"settings.txt: debug_saves = {Settings.DebugSaves} - its saves are not built, and nothing autosaves");
			}
			return;
		}
		if (!Content.Saves.CAutosave.IsDue(Settings.Autosave, State.Date))
		{
			return;
		}
		HoldAutosave(Content.Saves.CAutosave.Files);
	}

	/// <summary>The autosaves decided on, held for the frame that shows their notice (loadingscreen.md, section 6).</summary>
	private readonly Interface.Loading.CHeldAutosaves _heldAutosaves = new();

	private Loading.CScreenNotice? _notice;

	/// <summary>The key of the notice an autosave is written under; no table of the original's has it, so it shows as itself.</summary>
	private const string AutosavingKey = "AUTOSAVING";

	/// <summary>
	/// An autosave wanted: the notice goes up, the frame that shows it is drawn with no hour of the game started, and
	/// the next frame writes (<see cref="WriteHeldAutosaves"/>) - so the file still holds the turn of the day.
	/// </summary>
	private void HoldAutosave(string[] files)
	{
		_heldAutosaves.Ask(files);
		if (_notice is null)
		{
			_notice = new Loading.CScreenNotice { Name = "Notice" };
			_notice.Setup(_directories);
			AddChild(_notice);
		}
		_notice.Line = _localisation.Localise(AutosavingKey);
	}

	/// <summary>
	/// As a frame begins: the autosaves the last frame showed the notice for are written, that frame standing on the
	/// screen while they are, and the notice is taken down. A failure is logged as the original logs it.
	/// </summary>
	private void WriteHeldAutosaves(bool whetherShownOrNot = false)
	{
		IReadOnlyList<string[]> due = _heldAutosaves.Take(whetherShownOrNot);
		if (due.Count == 0)
		{
			return;
		}
		string folder = System.IO.Path.Combine(_directories.UserFolder, Content.Saves.CSaveFiles.FolderFor(_directories));
		foreach (string[] files in due)
		{
			try
			{
				// A recorded game marks the place: the save names it (docs/replay.md, section 10).
				_simulation.MarkSaved();
				Content.Saves.CAutosave.Write(State, _localisation.Localise, folder, files);
			}
			catch (System.Exception failed) when (failed is System.IO.IOException or System.UnauthorizedAccessException)
			{
				CDiagnostics.Warn($"Failed to write save file {folder}: {failed.Message}");
			}
		}
		if (_notice is not null)
		{
			_notice.Line = "";
		}
	}

	private CMinimapView? _minimap;
	private CMessageLogView? _modeButtons;
	private int _shownModeButton = -1;
	private long _diplomacyColoured;
	private long _navalMissions;
	private bool _fogOfWarShown = true;

	/// <summary>
	/// The mode each map mode button sets, by its number from one (<c>docs/mechanics/minimap.md</c>, section 4).
	/// </summary>
	private static CMapView.MapMode? ModeOfButton(int button) => button switch
	{
		1 => CMapView.MapMode.Terrain,
		2 => CMapView.MapMode.Political,
		3 => CMapView.MapMode.Weather,
		4 => CMapView.MapMode.Intel,
		5 => CMapView.MapMode.Revolt,
		6 => CMapView.MapMode.Diplomatic,
		7 => CMapView.MapMode.Region,
		8 => CMapView.MapMode.Supply,
		9 => CMapView.MapMode.Infrastructure,
		10 => CMapView.MapMode.VictoryPoints,
		11 => CMapView.MapMode.Theatre,
		12 => CMapView.MapMode.Strength,
		13 => CMapView.MapMode.Resources,
		14 => CMapView.MapMode.SimplifiedTerrain,
		15 => CMapView.MapMode.Air,
		16 => CMapView.MapMode.Naval,
		_ => null,
	};

	private static int MapModeButton(CMapView.MapMode mode)
	{
		for (int button = 1; button <= Interface.Map.CMapModeButtons.Count; button++)
		{
			if (ModeOfButton(button) == mode)
			{
				return button;
			}
		}
		return 0;
	}

	private CCustomMapModes? _customMapModes;
	private CNativePages? _nativePages;

	/// <summary>Whether the country screen a native page names as its host is open, by the name the page gives it.</summary>
	private bool HostScreenOpen(string host)
	{
		CCountryView? view = host.ToLowerInvariant() switch
		{
			"production" => _production,
			"technology" => _technology,
			"diplomacy" => _diplomacy,
			"politics" => _politics,
			"intelligence" => _espionage,
			"theatres" => _theatres,
			_ => null,
		};
		return view is { IsOpen: true };
	}

	/// <summary>A map mode button pressed: its mode set, as the game has it - a custom source goes.</summary>
	private void ChooseMapMode(int button)
	{
		_customMapModes?.CloseList();
		_mapView.ClearCustom();
		if (ModeOfButton(button) is { } mode)
		{
			// A mode chosen clicks unless it is already on (sound.md, section 7).
			if (_mapView.Mode != mode)
			{
				Sounds?.PlayUnlessPlaying("click");
			}
			_mapView.Mode = mode;
		}
	}

	/// <summary>What a frame does after the game's hours: the counters, the map's colours and the province shown.</summary>
	private void ProcessAfterHours()
	{
		// The outliner steps aside while a country screen is up (outliner.md, section 1), and the minimap is hidden
		// (minimap.md, section 1).
		bool covered = new CCountryView?[] { _production, _technology, _diplomacy, _politics, _espionage, _theatres }
			.Any(view => view is { IsOpen: true });
		_outliner?.SetCovered(covered);
		if (_minimap is not null)
		{
			_minimap.Visible = !covered;
			_minimap.SetHour(State.Hour);
		}
		// The button of the map's mode down, whoever set it (minimap.md, section 4).
		int button = MapModeButton(_mapView.Mode);
		if (button != _shownModeButton)
		{
			_shownModeButton = button;
			_modeButtons?.ShowModeButton(button);
		}

		// The counters are worked out again when a day has gone by, a unit of the
		// player's has been placed or ordered, or an hour has gone by with a unit on the
		// march - and once a frame at most.
		int waiting = Player?.Deployments.Count ?? 0;
		// And whenever a command has run, paused or not: a unit renamed, reorganised, merged or split changes its counter at
		// once in the original, not at the next hour (mapunits.md, section 3).
		if (_simulation.Executed != _executedSeen)
		{
			_executedSeen = _simulation.Executed;
			_unitsMoved = true;
		}
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
		// The resources, region, simplified terrain and naval modes are not: the original colours those only as they are
		// chosen (mapmodes.md, section 1).
		CMapView.MapMode mode = _mapView.Mode;
		bool chosenOnly = mode is CMapView.MapMode.Resources or CMapView.MapMode.Region
			or CMapView.MapMode.SimplifiedTerrain or CMapView.MapMode.Naval;
		if (State.ControlChanges != _controlChanges)
		{
			_controlChanges = State.ControlChanges;
			if (!chosenOnly)
			{
				_mapView.RefreshColours();
			}
			_unitsMoved = true;
		}
		// The modes the original's refresh follows diplomacy for: again when a war, an alliance, a pact, military access or
		// a faction changes. The supply mode among them - an alliance or a faction changes what the player knows of its
		// friends' land, which its colours ask (supplymap.md, section 2).
		if (mode is CMapView.MapMode.Political or CMapView.MapMode.Diplomatic or CMapView.MapMode.Intel
			or CMapView.MapMode.Theatre or CMapView.MapMode.Infrastructure or CMapView.MapMode.VictoryPoints
			or CMapView.MapMode.Strength or CMapView.MapMode.Supply)
		{
			long diplomacy = CMapModeTriggers.DiplomacyFigure(State);
			if (diplomacy != _diplomacyColoured)
			{
				_diplomacyColoured = diplomacy;
				_mapView.RefreshColours();
			}
		}
		// The naval mode's, when one of the player's patrols, raids or escorts starts or goes.
		if (mode == CMapView.MapMode.Naval)
		{
			long naval = CNavalMapColours.Figure(State, Player?.Tag);
			if (naval != _navalMissions)
			{
				_navalMissions = naval;
				_mapView.RefreshColours();
			}
		}
		// The fog of war turned off or on: what everyone knows is made again with it, so the map and its counters follow.
		if (State.FogOfWar != _fogOfWarShown)
		{
			_fogOfWarShown = State.FogOfWar;
			_mapView.RefreshColours();
			_mapView.Counters?.Refresh();
		}
		// The supply map mode is coloured by the day's carrying, so again each day.
		long day = State.TotalHours / CGameState.HoursPerDay;
		if (day != _dayColoured)
		{
			_dayColoured = day;
			// And the revolt map mode by the day's revolt risk (ours: when the original colours it again was not read), and the
			// intel mode by the day's intelligence (ours: the original follows the edge of what the player's units see, by a
			// pass whose caller was not found - mapmodes.md, section 9).
			if (_mapView.Mode is CMapView.MapMode.Supply or CMapView.MapMode.Revolt or CMapView.MapMode.Intel)
			{
				_mapView.RefreshColours();
			}
			// The air and naval map modes' rings, pictures and icons are made again each day.
			_mapView.RefreshModeIcons();
			// And the country names' factions, which no change of hands marks.
			_mapView.RefreshNames();
			// And the counters and buildings by the day's intelligence, which decides what of each the player sees
			// (mapunits.md, section 3).
			_mapView.Counters?.Refresh();
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
		// With show_unit_names off a near counter carries its leader's name, and the original writes it again the moment a
		// leader is put on or taken off (mapunits.md, section 2): so a change of leader counts, paused or not.
		if (_mapView.Counters is { ShowUnitNames: false })
		{
			long leaders = Leaders();
			if (leaders != _leaders)
			{
				_leaders = leaders;
				_unitsMoved = true;
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

	private long _leaders;

	/// <summary>How many commands had run when the counters last looked (<c>CSimulation.Executed</c>).</summary>
	private long _executedSeen;

	/// <summary>A figure that changes when any unit has another leader than it had.</summary>
	private long Leaders()
	{
		long figure = 17;
		foreach (CCountry country in State.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				figure = unchecked(figure * 31 + unit.Id * 7919L + (unit.Leader?.Id ?? 0));
			}
		}
		return figure;
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
	/// Shift several fleets move nobody and several air units never do - but one in a battle is still sent straight
	/// there, wherever it comes in the selection (selection.md, section 3).
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
			OrderUnit(unit, province, append);
		}
	}

	/// <summary>
	/// The orders window brought up for a unit - nothing where the click has brought it up already for a unit before
	/// this one, as the original's <c>CInGameIdler::OpenOrdersView</c> does nothing where a window of that kind is up
	/// (selection.md, section 3).
	/// </summary>
	private void OpenOrders(CUnit unit, int province)
	{
		if (_orders is { IsOpen: false })
		{
			_orders.Open(unit, province);
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
		// A unit in a battle is never added to (movement.md, section 1).
		if (unit.IsFighting)
		{
			append = false;
		}
		// A headquarters' own agent takes the click for its plan; a unit under an agent above it takes no order at all
		// (mapunits.md, section 12).
		if (CPlanRowsText.AgentAboveItsOwn(unit))
		{
			return;
		}
		if (CMovementRules.IsLandUnit(unit) && unit.Kind != EUnitKind.Division && unit.Plan.Active)
		{
			if (!append)
			{
				_simulation.Post(new CTogglePlanObjectiveCommand(Player.Tag, unitId, province));
			}
			else if (CRouteFinder.Find(State, Player, unit, unit.Location, province) is { } route)
			{
				_simulation.Post(new CSetPlanAxisCommand(Player.Tag, unitId, route, new List<int>()));
			}
			else
			{
				// An axis with no way to it: the original's click ends there, unheard and unmarked (movement.md, section 7).
				return;
			}
			Sounds?.PlayUnlessPlaying(CSelectionSounds.Moved(unit));
			_mapView.ShowOrderMark(unit, province, legal: true);
			return;
		}
		if (unit.Kind == EUnitKind.Air)
		{
			// An air unit in no battle is asked what it is to do there, Shift or not; one in a battle is sent straight
			// there, marked sent with nothing asked. Either sounds its move (fleetsandair.md, section 4; sound.md, section 7).
			if (!unit.IsFighting)
			{
				OpenOrders(unit, province);
			}
			else
			{
				_mapView.ShowOrderMark(unit, province, legal: true);
				_simulation.Post(new CMoveUnitCommand(Player.Tag, unitId, province, append));
				_ordered = true;
			}
			Sounds?.PlayUnlessPlaying(CSelectionSounds.Moved(unit));
			return;
		}
		// A land unit with Ctrl held is asked too, in the land window (landorders.md, section 1).
		if (CMovementRules.IsLandUnit(unit) && Input.IsKeyPressed(Key.Ctrl))
		{
			OpenOrders(unit, province);
			return;
		}
		if (unit.Kind == EUnitKind.Navy)
		{
			// A fleet where it is, while under way, halts. Otherwise a plain click asks
			// what it is to do there, and one with shift sends it straight there.
			bool connected = CMovementRules.SeaConnected(State, Player, State.Province(province), CMovementRules.OwnSea(State, unit));
			if (unit.IsMoving && province == unit.Location)
			{
				command = new CCancelMovementCommand(Player.Tag, unitId);
			}
			else if (!connected && (append || unit.IsFighting
				|| !CTransportText.OpensOrdersOnCoast(State, Player, unit, State.Province(province))))
			{
				// Where the sea does not reach, refused before anything else - the window coming up only for a coast what the
				// fleet carries may land on, without shift and out of a battle (transport.md, section 12): marked and erring,
				// its move unheard (movement.md, section 7).
				GD.Print($"{unit.Name} cannot reach {State.Province(province).Name} by sea.");
				_mapView.ShowOrderMark(unit, province, legal: false);
				Sounds?.PlayUnlessPlaying(CSelectionSounds.Refused);
				return;
			}
			else if (!append && !unit.IsFighting)
			{
				OpenOrders(unit, province);
				return;
			}
			else
			{
				// With shift, or out of a battle, it is simply sent - and a fleet in a
				// battle then counts four hours before it goes. Its move sounds before its range is asked.
				command = new CMoveUnitCommand(Player.Tag, unitId, province, append);
				Sounds?.PlayUnlessPlaying(CSelectionSounds.Moved(unit));
			}
		}
		else
		{
			command = province == unit.Location && (!append || !unit.IsMoving)
				? new CCancelMovementCommand(Player.Tag, unitId)
				: new CMoveUnitCommand(Player.Tag, unitId, province, append);
		}
		bool land = unit.Kind != EUnitKind.Navy;
		// A land unit's click asks the original's whole test of where it may be sent, a unit in a battle sent only where it
		// may fall back among it; the command asks less (movement.md, section 1).
		bool refused = land && command is CMoveUnitCommand
			&& !CMovementRules.MayMoveTo(State, Player, unit, State.Province(province));
		if (refused || !command.MayExecute(State))
		{
			GD.Print($"{unit.Name} may not go to {State.Province(province).Name}.");
			// A land unit refused errs; a fleet has sounded its move already. Either is marked refused (movement.md,
			// section 7).
			if (command is CMoveUnitCommand)
			{
				_mapView.ShowOrderMark(unit, province, legal: false);
			}
			if (land)
			{
				Sounds?.PlayUnlessPlaying(CSelectionSounds.Refused);
			}
			return;
		}
		// A land unit halted where it stands is silent and unmarked; one sent sounds its move, and is marked sent.
		if (command is CMoveUnitCommand)
		{
			_mapView.ShowOrderMark(unit, province, legal: true);
			if (land)
			{
				Sounds?.PlayUnlessPlaying(CSelectionSounds.Moved(unit));
			}
		}
		_simulation.Post(command);
		_ordered = true;
	}

	/// <summary>Space or Pause stops and starts time; plus and minus change its speed.</summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true } key)
		{
			return;
		}
		// A held key repeats only the speed keys: the others are switches, which a repeat would flick on and off.
		if (key.Echo && key.Keycode is not (Key.KpAdd or Key.Plus or Key.Equal or Key.KpSubtract or Key.Minus))
		{
			return;
		}
		if (Debug.CDebugMenu.IsMenuKey(key))
		{
			_debugMenu?.Toggle();
			return;
		}
		if (_console is not null && CConsoleView.IsConsoleKey(key))
		{
			_console.Toggle();
			return;
		}
		// An enhanced game's window of the mod's own pages, on the key the player chose for it - Insert until he
		// chooses another - before the game's own keys, so that a key of theirs given to it is its own
		// (modwindows.md, sections 5 and 6.4).
		if (_modWindowsView is { } modWindows && key.Keycode == modWindows.ToggleKey)
		{
			modWindows.Toggle();
			return;
		}
		switch (key.Keycode)
		{
			case Key.Space or Key.Pause:
				_simulation.Pace.TogglePause();
				break;
			case Key.Escape:
				// What nothing took first - the map lets a selection go on the first, a shown button with the shortcut
				// takes its own - opens the in-game menu, or closes it (ingamemenu.md, section 1).
				_menu?.Toggle();
				break;
			case Key.KpAdd or Key.Plus or Key.Equal:
				// A change of speed clicks, by key as by button (sound.md, section 7).
				if (_simulation.Pace.ChangeSpeed(1))
				{
					Sounds?.PlayUnlessPlaying("click");
				}
				break;
			case Key.KpSubtract or Key.Minus:
				if (_simulation.Pace.ChangeSpeed(-1))
				{
					Sounds?.PlayUnlessPlaying("click");
				}
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
	internal static IEnumerable<string> UserArguments()
	{
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			foreach (string one in System.Text.RegularExpressions.Regex.Split(argument.Trim(), @"\s+(?=--)"))
			{
				if (one.Length > 0)
				{
					yield return WithFullPath(one);
				}
			}
		}
	}

	/// <summary>The switches whose value is a file, which a relative path names in the folder the game was started in.</summary>
	private static readonly string[] PathSwitches = { "--screenshot=", "--loadingshot=", "--record=", "--replay=", "--load=" };

	/// <summary>
	/// The folder the game was started in, kept before the working folder is made the install's (<c>Main</c>;
	/// docs/mechanics/mods.md, section 6), or null while it has not been changed.
	/// </summary>
	internal static string? LaunchFolder { get; set; }

	/// <summary>A path switch's relative value made whole against the folder the game was started in.</summary>
	private static string WithFullPath(string argument)
	{
		foreach (string name in PathSwitches)
		{
			if (LaunchFolder is not null && argument.StartsWith(name, System.StringComparison.Ordinal) && argument.Length > name.Length)
			{
				string value = argument[name.Length..];
				return System.IO.Path.IsPathRooted(value) ? argument : name + System.IO.Path.GetFullPath(value, LaunchFolder);
			}
		}
		return argument;
	}

	/// <summary>
	/// The frame's hours, but where a replay reaches the end of its recording: the clock is stopped there for good and
	/// that is said, the hour it ended in being left part run (docs/replay.md, section 6).
	/// </summary>
	/// <summary>Real time owed to the clock since hours were last started: a frame's, or every frame's an hour ran beside.</summary>
	private double _owedSeconds;

	/// <summary>
	/// After a frame is drawn, with nothing running and the replay not run out: the hours the time owed makes due started
	/// beside the frames that follow (<see cref="CSimulation.BeginAdvance"/>) - drawn first, so that nothing drawing this
	/// frame reads the game while they run - or, with none due, the orders given run here.
	/// </summary>
	private void StartHours()
	{
		if (_simulation.IsBusy || _replayEnded || !IsInsideTree())
		{
			return;
		}
		// An autosave decided on waits for the frame just drawn, which shows its notice, and no hour runs meanwhile.
		if (_heldAutosaves.Waiting)
		{
			_heldAutosaves.FrameDrawn();
			return;
		}
		// A client's hours are the host's, started as they have come; and a game another is being loaded in place of runs none.
		if (Net is { IsHost: false } or { Loading: true })
		{
			if (Net is { Loading: false })
			{
				Net.StartHours();
			}
			return;
		}
		double owed = _owedSeconds;
		_owedSeconds = 0;
		UnlessTheReplayEnds(() => _simulation.BeginAdvance(owed));
	}

	/// <summary>
	/// The hours run beside the frame collected where they are done - with <paramref name="finish"/>, waited for - and how
	/// many there were. Collected always, a run-out replay's last ones among them: until it is, nothing reads the game.
	/// </summary>
	private int CollectHours(bool finish)
	{
		try
		{
			return finish ? _simulation.Finish() : _simulation.Collect();
		}
		catch (CReplayEndedException ended)
		{
			if (!_replayEnded)
			{
				ReplayRanOut(ended);
			}
			return 0;
		}
	}

	/// <summary>Whether the game is the hours' running beside the frame: nothing may read it until they are collected.</summary>
	public bool GameBusy => _simulation?.IsBusy == true;

	/// <summary>
	/// The interface held while hours run beside the frame: the screens, the HUD and the tooltips keep what they last
	/// showed and do nothing, and the map moves its camera and reads nothing of the game. Let go once they are collected.
	/// </summary>
	private void HoldInterface(bool held)
	{
		if (held == _interfaceHeld)
		{
			return;
		}
		_interfaceHeld = held;
		ProcessModeEnum mode = held ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
		if (_hud is not null)
		{
			_hud.ProcessMode = mode;
		}
		if (_tooltips is not null)
		{
			_tooltips.ProcessMode = mode;
		}
		_mapView.GameBusy = held;
	}

	private bool _interfaceHeld;

	/// <summary>What was pressed or typed while hours ran beside the frame, in its order: given to the interface once they are collected.</summary>
	private readonly List<InputEvent> _heldInput = new();

	/// <summary>
	/// While hours run beside the frame, every event but those that only move the camera - the pointer moving, the wheel
	/// over the map, the middle button - is held, and given out again in its order once they are collected: what it
	/// would do asks the game.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!GameBusy || MovesOnlyTheCamera(@event))
		{
			return;
		}
		_heldInput.Add(@event);
		GetViewport().SetInputAsHandled();
	}

	private bool MovesOnlyTheCamera(InputEvent @event) => @event switch
	{
		InputEventMouseMotion => true,
		InputEventMouseButton { ButtonIndex: MouseButton.Middle } => true,
		// The wheel over the interface scrolls what it is over, which waits; over the map it zooms.
		InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } => GetViewport().GuiGetHoveredControl() is null,
		_ => false,
	};

	/// <summary>The held events given out, in their order, now that nothing runs beside the frame.</summary>
	private void ReplayHeldInput()
	{
		if (_heldInput.Count == 0)
		{
			return;
		}
		List<InputEvent> held = new(_heldInput);
		_heldInput.Clear();
		foreach (InputEvent @event in held)
		{
			GetViewport().PushInput(@event, true);
		}
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
			ReplayRanOut(ended);
			return false;
		}
	}

	/// <summary>
	/// Asked, once the frame has ended, where a playback has come to the end of its recording: whether another follows -
	/// the game played on from a save written here - in which case whoever answers loads it in this session's place
	/// (docs/replay.md, section 10).
	/// </summary>
	public System.Func<bool>? NextRecording { get; set; }

	/// <summary>A playback at the end of its recording: on into the next where one follows, else the clock stopped for good.</summary>
	private void ReplayRanOut(CReplayEndedException ended)
	{
		_replayEnded = true;
		// Whoever answers yes has begun loading the next in this session's place, which gives way once the frame has ended.
		if (NextRecording?.Invoke() == true)
		{
			_crossing = true;
			return;
		}
		_simulation.Pace.Stop();
		GD.Print($"--replay: {ended.Message}; the clock stops here.");
	}

	/// <summary>Whether this session's playback goes on in another, being loaded: nothing more is asked of this one.</summary>
	private bool _crossing;

	/// <summary>
	/// The seed a new game is made with: <c>--seed=N</c>'s, or else one taken from the clock as Play is pressed, as the
	/// original takes one - so two games of one start differ, and one played again with its seed given is the same game
	/// (<c>docs/mechanics/frontend.md</c>, *Ours*). It is printed, so a game can be had again.
	/// </summary>
	internal static uint NewGameSeed()
	{
		if (Argument("--seed=") is { } text && uint.TryParse(text, out uint given))
		{
			GD.Print($"--seed: the new game is seeded with {given}.");
			return given;
		}
		long ticks = System.DateTime.UtcNow.Ticks;
		uint seed = unchecked((uint)ticks ^ (uint)(ticks >> 32));
		GD.Print($"The new game is seeded with {seed}, from the clock; --seed={seed} makes it again.");
		return seed;
	}

	/// <summary>What follows a switch on the command line - the recording of <c>--replay=</c> or <c>--record=</c> - or null.</summary>
	internal static string? Argument(string switchName)
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
	internal static string? PlayerArgument()
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
	/// driving it by hand - each of them only in a game one of <c>--country=</c>, <c>--load=</c> or <c>--replay=</c>
	/// started, the main menu coming up without (<c>Main.SaysWhatToPlay</c>):
	/// <c>--mod=BlackICE GitHub</c> lays that mod of the install's <c>tfh/mod</c> over the install - and so does the original
	/// launcher's own <c>-mod=</c> - the main menu included, a game or none (read before anything is, by <c>Main</c>, not
	/// here; <c>docs/mechanics/mods.md</c>, section 3),
	/// <c>--nocull</c> draws every mesh of the map's surface, the camera seeing it or not, to set beside the culled picture
	/// (read as the surface is made; <c>docs/mechanics/mapdraw.md</c>, section 6),
	/// <c>--lua=5.1.4</c> runs the install's Lua on the original's Lua 5.1.4 and not on LuaJIT, for the comparisons that want
	/// a table's <c>pairs</c> in the original's order (read before anything is, by <c>Main</c>; <c>docs/mechanics/lua.md</c>, section 1),
	/// <c>--install=C:\Games\Hearts of Iron 3</c> plays that install, wherever the engine stands - how the launcher starts it
	/// (read before anything is, by <c>Main</c>; <c>docs/launcher.md</c>, section 5),
	/// <c>--centre=1861</c> puts a province in the middle and selects it,
	/// <c>--country=ENG</c> plays that country and not Germany, <c>--date=1939.9.1</c> begins its game at that date as the
	/// lobby's bookmark of it would (read as the game is made, not here), <c>--seed=12345</c> makes a new game - the
	/// lobby's too - with that seed and not one from the clock (read as the game is made; a replay takes its recording's),
	/// <c>--record=path.gz</c> records the game played - the player's orders and the AI's - and <c>--replay=path.gz</c>
	/// plays a recording back, played by whoever played it and taking no orders, the map alone shown where nobody did, the
	/// clock stopping where the recording ends (<c>docs/replay.md</c>, sections 6 and 8; both read as the session loads,
	/// not here), <c>--hash</c> prints the game's hash each midnight,
	/// <c>--trees</c> plants the map's trees whatever settings.txt's <c>renderTrees</c> says (read as the map is made, not here),
	/// <c>--unitnames=no</c> or <c>--unitnames=yes</c> gives the counters that <c>show_unit_names</c> for the run, the file
	/// left as it is (read as the map is made, not here),
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
	/// <c>--election</c> holds an election in the played country at once,
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
	/// <c>--select=Luftflotte II</c> selects the player's unit of that name, and with several apart by | each in turn,
	/// <c>--click=700,310;r1240,886;h48</c> clicks at those points of the canvas, in turn -
	/// with r the right button, with s shift held, with c Ctrl held, with t1.5: before the point
	/// the button held down 1.5 seconds of real time, and h48 lets 48 hours go by, m460,40 moves
	/// the pointer there without clicking, d700,310>900,500 drags from the one point to the
	/// other - sd with shift held as it lets go, D holding on at the end - ck1 presses Ctrl and 1, esc presses Escape,
	/// type:Text types the text into the edit box that has the keyboard, bs, del, left, right, home, end and enter press
	/// those keys,
	/// and w2 waits two seconds of real time, which is
	/// what a tooltip's second half waits on - once everything else is done,
	/// <c>--run</c> starts the clock at the top speed instead of paused,
	/// <c>--host</c> or <c>--host=port</c> hosts the game for other machines - with <c>--otherfiles</c>, letting in a
	/// machine whose files are not the host's (docs/multiplayer.md, section 15) - and <c>--join=address[:port]</c> joins one,
	/// asking for the country of <c>--country=</c> under the name of <c>--name=</c>, with <c>--netlog</c> writing each new
	/// day to the log and <c>--netpart=1936.3.10</c> on a client putting its game out of step from that day (docs/multiplayer.md, section 6),
	/// <c>--say=text|text</c> typing those lines into the chat in play once, three days in (docs/mechanics/chat.md),
	/// <c>--perf=60</c> writes once a second where a frame's time goes - the game's hours,
	/// the session's own work, the drawing - and quits after sixty seconds with the sums, and
	/// <c>--screenshot=path.png</c> saves what is on screen after a few frames and quits.
	/// </summary>
	/// <summary>The developer switches a game between several machines takes: those that only look at it.</summary>
	private static readonly string[] NetSwitches =
	{
		"--screenshot=", "--click=", "--centre=", "--hash", "--perf=", "--mode=", "--zoom=", "--select=",
	};

	private async void RunDeveloperArguments()
	{
		string? screenshot = null;
		string? clicks = null;
		string? select = null;
		int centre = 0;
		float? zoom = null;
		foreach (string argument in UserArguments())
		{
			// Between several machines only the switches that look are taken, and the host's --run: the others change the
			// game from outside its commands, on this machine alone.
			if (Net is { } net && !NetSwitches.Any(argument.StartsWith) && !(net.IsHost && argument == "--run"))
			{
				continue;
			}
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
				_simulation.Pace.Set(paused: false, CClock.SpeedCount - 1);
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
			else if (argument.StartsWith("--mode=", System.StringComparison.Ordinal)
				&& System.Enum.TryParse(argument["--mode=".Length..], ignoreCase: true, out CMapView.MapMode named))
			{
				// Any other mode by its name: --mode=diplomatic, --mode=victorypoints, --mode=simplifiedterrain and the rest.
				_mapView.Mode = named;
			}
			else if (argument == "--screen=victory")
			{
				_victoryConditions?.Toggle();
			}
			else if (argument == "--screen=modwindows")
			{
				// An enhanced game's menu of the mod's own windows, as the Insert key shows it (modwindows.md).
				_modWindowsView?.Toggle();
			}
			else if (argument.StartsWith("--screen=modwindows:", System.StringComparison.Ordinal))
			{
				// The same with a page open on it, by its file's name: --screen=modwindows:flags.
				_modWindowsView?.Show(argument["--screen=modwindows:".Length..]);
			}
			else if (argument.StartsWith("--screen=native:", System.StringComparison.Ordinal))
			{
				// A native page's window by the page's name, as its opener opens it: --screen=native:country_info/prodsliders.
				string wantedPage = argument["--screen=native:".Length..];
				if (_nativePages?.Toggle(wantedPage) != true)
				{
					GD.PrintErr($"--screen=native: no native page is named '{wantedPage}'");
				}
			}
			else if (argument.StartsWith("--custommap=", System.StringComparison.Ordinal))
			{
				// An enhanced game's custom map source by its kind and key, as its list would choose it:
				// --custommap=supply:days_of_supplies, --custommap=buildings:industry, --custommap=resources:energy.
				string[] parts = argument["--custommap=".Length..].Split(':', 2);
				CMapView.MapMode? customHost = parts[0] switch
				{
					"buildings" => CMapView.MapMode.Infrastructure,
					"resources" => CMapView.MapMode.Resources,
					"supply" => CMapView.MapMode.Supply,
					_ => null,
				};
				if (_customMapModes is not null && customHost is { } mode && parts.Length == 2 && CCustomMapModes.KindOf(mode) is { } kind
					&& Interface.Map.CCustomMapSources.Find(State, kind, parts[1]) is { } source)
				{
					_mapView.SetCustom(source, mode, _customMapModes.Palette);
				}
				else
				{
					GD.PrintErr($"--custommap: '{argument}' names no source of an enhanced game");
				}
			}
			else if (argument.StartsWith("--window=", System.StringComparison.Ordinal) && _gui is not null && _hud is not null)
			{
				// A window the interface files declare, by its name, put up as it is with nothing behind its elements:
				// for looking at a layout while it is written (builtin.md, section 6).
				string wanted = argument["--window=".Length..];
				if (Gui.CGuiWindow.Create(_gui, wanted) is { } shown)
				{
					_hud.AddChild(shown);
				}
				else
				{
					GD.PrintErr($"--window: no window is declared as '{wanted}'");
				}
			}
			else if (argument == "--screen=endgame")
			{
				ShowEndGame();
			}
			else if (argument == "--screen=vcconfirm")
			{
				ShowVictoryDialog();
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
			else if (argument == "--screen=warfare")
			{
				_warfare?.Toggle();
			}
			else if (argument == "--screen=statistics")
			{
				_statistics?.Toggle();
			}
			else if (argument == "--screen=theatres")
			{
				Toggle(_theatres);
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
				if (_crossing)
				{
					// The playback goes on in the session being loaded, which runs the switches for itself.
					return;
				}
				// Hours run with no frame between them: an autosave they decided on is written at once.
				WriteHeldAutosaves(whetherShownOrNot: true);
			}
			else if (argument.StartsWith("--save=") && State.Replay is not { IsReplaying: true })
			{
				// Saves the game now under that name in the folder of saves, as the menu's Save Game does.
				string path = System.IO.Path.Combine(_directories.UserFolder, Content.Saves.CSaveFiles.FolderFor(_directories),
					Content.Saves.CSaveFiles.FileName(argument["--save=".Length..]) ?? "switch.hoi3");
				_simulation.Pump();
				_simulation.MarkSaved();
				Content.Saves.CSaveFiles.Write(State, _localisation.Localise, path);
				GD.Print($"--save: {path}" + (State.ReplayLink is { } link ? $", at {link.Text} of its recording." : "."));
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
			else if (argument == "--election")
			{
				// Holds an election in the played country at once, for a look at its popup, whose date may be years away.
				if (Player is not null)
				{
					Sim.Politics.CElections.Hold(State, Player);
				}
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
			else if (argument.StartsWith("--nukes="))
			{
				// A country's bombs for a trial by hand: --nukes=GER:2.
				string[] parts = argument["--nukes=".Length..].Split(':');
				if (parts.Length == 2 && CCountryTag.TryParse(parts[0], out CCountryTag holder)
					&& int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bombs))
				{
					_simulation.Post(new CCheatCommand(ECheat.AddNukes, holder, null, bombs * 1000L, default, 0, ""));
					_simulation.Pump();
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
					&& (CAirMissions.IsAirMission(airMission) || CBombingOrders.IsBombingMission(airMission) || airMission == EUnitOrder.Nuke)
					&& int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int over))
				{
					// A bombing mission likewise - groundattack, interdiction, navalstrike,
					// strategicbombing, logisticalstrike, runwaycratering, installationstrike,
					// portstrike or airconvoyraid - for the air unit with the most of the attack it fires.
					string[] fired = CBombingOrders.GoesForLandUnits(airMission)
						? new[] { CUnitTotals.SoftAttack, CUnitTotals.HardAttack }
						: CBombingOrders.GoesForShips(airMission) || CBombingOrders.RaidsConvoys(airMission) ? new[] { CNavalFire.SeaAttack }
						: CBombingOrders.IsBombingMission(airMission) || airMission == EUnitOrder.Nuke ? new[] { CBombingOrders.StrategicAttack }
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
			// Several names apart by '|' select each in turn, the first first.
			string[] names = select.Split('|');
			for (int index = 0; index < names.Length; index++)
			{
				if (Player.AllUnits().FirstOrDefault(unit => unit.Name == names[index]) is not { } unit)
				{
					continue;
				}
				if (index == 0)
				{
					_mapView.SelectUnit(unit);
				}
				else
				{
					_mapView.ChangeUnits(selection => selection.Add(unit, Player.Tag));
				}
			}
		}

		// The clicks of a game the front end handed over were the front end's.
		// All of it through what outlives the session, the console's tag being able to replace it half way.
		// A session a game between machines has already replaced - the host's first, as a machine in its lobby is sent
		// the game - plays nothing more.
		if (!IsInstanceValid(this) || !IsInsideTree())
		{
			return;
		}
		Node host = GetParent();
		SceneTree tree = GetTree();
		Viewport viewport = GetViewport();
		if (clicks is not null && PlaysClicks)
		{
			await Click(clicks);
		}

		if (screenshot is null)
		{
			return;
		}
		for (int frame = 0; frame < 8; frame++)
		{
			await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
		await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error error = viewport.GetTexture().GetImage().SavePng(screenshot);
		GD.Print(error == Error.Ok ? $"Saved {screenshot}" : $"Could not save {screenshot}: {error}");
		tree.Quit();
	}

	/// <summary>The clicks of <c>--click=</c>, played into the session's viewport (<see cref="Debug.CClickScript"/>).</summary>
	private System.Threading.Tasks.Task Click(string points)
	{
		// A script's hours run with no frame between them: an autosave an earlier one decided on is written before the next.
		return new Debug.CClickScript(GetParent(), () =>
		{
			WriteHeldAutosaves(whetherShownOrNot: true);
			return UnlessTheReplayEnds(() => _simulation.RunHours(1));
		}).Run(points);
	}

	/// <summary>What the console's commands ask of the running game.</summary>
	private sealed class CConsoleHost : IConsoleHost
	{
		private const string WireframesAsked = "openhoi3_wireframes";

		private readonly CSession _session;

		public CConsoleHost(CSession session)
		{
			_session = session;
		}

		public CGameState State => _session.State;

		public CCountry? Player => _session.Player;

		public IReadOnlyList<int> SelectedProvinces => _session._mapView.SelectedProvinces;

		public bool TooltipsOn
		{
			get => _session._tooltips?.Enabled ?? true;
			set
			{
				if (_session._tooltips is not null)
				{
					_session._tooltips.Enabled = value;
				}
			}
		}

		public bool TooltipDebug
		{
			get => _session._tooltipDebug;
			set => _session._tooltipDebug = value;
		}

		public bool ContinentPainting
		{
			get => _session._mapView.ContinentPainting;
			set => _session._mapView.ContinentPainting = value;
		}

		public void Post(CCommand command)
		{
			_session._simulation.Post(command);
			_session._simulation.Pump();
		}

		public void CopyToClipboard(string text) => DisplayServer.ClipboardSet(text);

		public CUnit? SelectedUnit => _session._mapView.Units.First;

		// Not between several machines: the console's commands are no player's orders (docs/multiplayer.md, section 6).
		public bool TakesCommands => _session._simulation.TakesCommands && _session.Net is null;

		public int RenderLayers
		{
			get => _session._mapView.RenderLayers;
			set => _session._mapView.RenderLayers = value;
		}

		public int ModeLayers => _session._mapView.ModeLayers;

		public bool Fullscreen
		{
			get => DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
			set => DisplayServer.WindowSetMode(value ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
		}

		public bool Wireframe
		{
			get => _session._wireframe;
			set
			{
				_session._wireframe = value;
				// A mesh has its lines only if it is made after this, so the first time everything is made again.
				if (value && !RenderingServer.Singleton.HasMeta(WireframesAsked))
				{
					RenderingServer.Singleton.SetMeta(WireframesAsked, true);
					RenderingServer.SetDebugGenerateWireframes(true);
					_session.CallDeferred(MethodName.HandOver);
					return;
				}
				_session.GetViewport().DebugDraw = value ? Viewport.DebugDrawEnum.Wireframe : Viewport.DebugDrawEnum.Disabled;
			}
		}

		public bool EffectExists(string name)
		{
			return System.IO.File.Exists(_session._directories.Resolve(name)) || System.IO.File.Exists(_session._directories.Resolve("gfx/FX/" + name));
		}

		public void ShowCountryPlayed() => _session.CallDeferred(MethodName.HandOver);

		public void ReloadInterface()
		{
			_session._readInterfaceAgain = true;
			_session.CallDeferred(MethodName.HandOver);
		}

		public string NextSong() => _session.Music?.NextSong() ?? "";

		public void RefreshMapColours() => _session._mapView.RefreshColours();

		public void WriteDetailedLog(string name)
		{
			string path = System.IO.Path.Combine(_session._directories.UserFolder, "logs", name + ".log");
			System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
			Content.Saves.CSaveFiles.Write(_session.State, _session._localisation.Localise, path);
		}

		public void SetLeastHeight(int height) => _session._mapView.SetLeastHeight(height);

		public void Log(string line) => GD.Print(line);
	}
}
