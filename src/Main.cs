using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Launch;
using OpenHOI3.Content.Mods;
using OpenHOI3.Content.Music;
using OpenHOI3.Loading;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3;

/// <summary>
/// Entry point, and the boot scene.
///
/// It finds the install and brings up the front end - the main menu and the lobby - which hands over the game Play makes;
/// a game resigned brings the front end back. A developer switch that says what to play starts that game at once. A game
/// is a <see cref="CSession"/> this node creates, frees and creates again (<c>docs/mechanics/frontend.md</c>, section 1).
/// Each of them is read and made under the loading screen (<c>docs/mechanics/loadingscreen.md</c>).
/// </summary>
public partial class Main : Control
{
	private CSession? _session;
	private FrontEnd.CFrontEnd? _frontEnd;
	private CDirectorySettings _directories = null!;

	/// <summary>The soundtrack, one for the whole run (docs/mechanics/music.md).</summary>
	private Audio.CMusic _music = null!;

	private Audio.CSoundEffects _sounds = null!;

	/// <summary>The map's 3D surface, kept from the main menu's map to the game's and on, one for the whole run (CMapSurfaceKeeper).</summary>
	private Map.CMapSurfaceKeeper _surfaces = null!;

	/// <summary>
	/// The player's settings the music goes by while no screen holds them: the file's as the program starts, then whatever the
	/// front end or the session last held as it went - its options window may have changed them.
	/// </summary>
	private Content.Settings.CUserSettings _startSettings = Content.Settings.CUserSettings.Default;

	/// <summary>The switches that say what game to play, which start it without the front end.</summary>
	private static readonly string[] GameSwitches = { "--country=", "--load=", "--replay=", "--join=" };

	/// <summary>
	/// This machine's part in a game between several machines - a host's with <c>--host</c>, a client's with
	/// <c>--join=</c> - kept across the sessions of it; null in a game of one (docs/multiplayer.md, section 6).
	/// </summary>
	private Multiplayer.CNetGame? _net;

	/// <summary>The connections looked after every frame, whatever is on screen; and a game the host has left, ended.</summary>
	public override void _Process(double delta)
	{
		if (_net is not { } net)
		{
			return;
		}
		net.Pump();
		// A game that parts is written beside the logs, each machine's own figures, and said in the log.
		net.DumpFolder ??= Content.Logs.CLogFiles.Folder(_directories);
		foreach (string note in net.TakeNotes())
		{
			GD.Print("Multiplayer: " + note);
		}
		if (net.Ended is { } why && _session is not null && !net.Loading)
		{
			GD.Print($"Multiplayer: {why}");
			// The host gone, a client keeps what was played: the game as it stands here goes to its saves.
			if (!net.IsHost)
			{
				string? saved = _session.SaveAsTheHostIsLost();
				GD.Print(saved is null ? "Multiplayer: the game could not be saved." : $"Multiplayer: the game saved as {saved}.");
				// Said on screen too, over the way back to the main menu and a while on it.
				ShowNotice(why + "\n" + (saved is null ? "The game could not be saved."
					: "The game was saved as " + System.IO.Path.GetFileName(saved)), seconds: 20);
			}
			LeaveNet();
			_music.LeaveGame();
			CallDeferred(MethodName.ShowFrontEnd);
		}
	}

	/// <summary>
	/// A few words over everything for a while - the loading screen and the main menu that follows among it: what a
	/// player has to be told as a game between machines ends under him. Ours; one at a time, a new one in the old's place.
	/// </summary>
	private void ShowNotice(string text, double seconds)
	{
		_notice?.QueueFree();
		var layer = new CanvasLayer { Name = "Notice", Layer = 200 };
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(0, 140),
			Size = new Vector2(1920, 120),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 36);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 10);
		layer.AddChild(label);
		AddChild(layer);
		_notice = layer;
		GetTree().CreateTimer(seconds).Timeout += () =>
		{
			if (_notice == layer)
			{
				_notice = null;
			}
			if (IsInstanceValid(layer))
			{
				layer.QueueFree();
			}
		};
	}

	private CanvasLayer? _notice;

	/// <summary>The game between machines left: its connections closed.</summary>
	private void LeaveNet()
	{
		_net?.Dispose();
		_net = null;
	}

	/// <summary>
	/// The arrow keys stop here, after everything under this node has had them - a text box or the console takes its own
	/// first - and before Godot's own interface. That answers an arrow, bound to its <c>ui_left</c> and the others, by
	/// looking through the whole tree for a control to give the focus to, when none has it; with some 21,000 nodes that
	/// took 26 ms an event, and a held arrow's repeats, thirty a second, took the map from 90 frames a second to 20
	/// (2026-11-12). Nothing here uses Godot's focus, and the map scrolls by asking whether a key is down, which a handled
	/// event leaves as it is.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey { Keycode: Key.Left or Key.Right or Key.Up or Key.Down })
		{
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>The kept surface given back to the engine as the program ends.</summary>
	public override void _ExitTree()
	{
		_surfaces?.Release();
		_net?.Dispose();
	}

	public override void _Ready()
	{
		// The content library reports to standard error until told otherwise, because
		// it has no engine in it. From here on its diagnostics reach Godot's own log.
		//
		// A warning is printed as a plain line and an error is pushed. Godot attaches a
		// stack trace to anything pushed, which is worth having for an error and is
		// noise for a warning: these describe the install's files, and where in our
		// code the line was written says nothing about them.
		CDiagnostics.WarnSink = message => GD.Print("WARNING: " + message);
		CDiagnostics.ErrorSink = message => GD.PushError(message);
		// A fault in the install's or the mods' own files goes to the validation log, one to a line (docs/mechanics/logs.md).
		CDiagnostics.ProblemSink = Debug.CValidationLog.Instance.Write;
		// Everything printed from here on goes to the log file too, once its folder is known (docs/mechanics/logs.md).
		Debug.CFileLog.Install();
		// An exception nothing catches - a crash inside the engine among them - is written beside the logs.
		Debug.CCrashLog.Install();

		GD.Print("OpenHOI3 starting.");
		MouseFilter = MouseFilterEnum.Ignore;
		ChooseLua();
		if (CSession.UserArguments().FirstOrDefault(a => a.StartsWith(Debug.CShaderCompileCheck.Switch)) is { } shaderCheck)
		{
			Debug.CShaderCompileCheck.Run(shaderCheck[Debug.CShaderCompileCheck.Switch.Length..], GetTree());
			return;
		}
		if (CSession.UserArguments().FirstOrDefault(a => a.StartsWith(Debug.CTextureCheck.Switch)) is { } textureCheck)
		{
			string? textureOut = CSession.UserArguments().FirstOrDefault(a => a.StartsWith(Debug.CTextureCheck.OutSwitch));
			Debug.CTextureCheck.Run(textureCheck[Debug.CTextureCheck.Switch.Length..],
				textureOut?[Debug.CTextureCheck.OutSwitch.Length..], GetTree());
			return;
		}

		CDirectorySettings? directories = FindInstall();
		if (directories is null)
		{
			return;
		}
		GD.Print($"Install: {directories.GameRoot}");
		// The mod is known before anything is read: the main menu is the mod's interface too (docs/mechanics/mods.md).
		directories = WithModsNamed(directories);
		_directories = directories;
		// The log in the player's folder's logs, the mod's with a mod, where the original keeps its own - under our own name,
		// the runs before it kept (docs/mechanics/logs.md); a crash's file beside it.
		string logs = Content.Logs.CLogFiles.Folder(directories);
		Debug.CFileLog.Instance?.Open(Content.Logs.CLogFiles.Start(logs));
		Debug.CValidationLog.Instance.Open(Content.Logs.CLogFiles.Validation(logs));
		Debug.CCrashLog.Folder = logs;
		// The install is the working folder, as the original's launcher starts it: a mod's script opens its files by paths
		// written against it (docs/mechanics/mods.md, section 6). A path switch is kept against the folder started in.
		CSession.LaunchFolder = System.IO.Directory.GetCurrentDirectory();
		System.IO.Directory.SetCurrentDirectory(directories.GameRoot);
		// The title theme from the load's first moment, as the original starts it before its first loading frame
		// (docs/mechanics/music.md, section 3). The settings it is played by are the screen's that has them, else the file's.
		_startSettings = Content.Settings.CUserSettingsReader.Read(Content.Settings.CUserSettingsReader.DefaultPath(directories));
		_music = new Audio.CMusic { Name = "Music" };
		_music.Setup(directories, () => _session?.Settings ?? _frontEnd?.Settings ?? _startSettings);
		_music.GameBusy = () => _session?.GameBusy == true;
		AddChild(_music);
		// The sound effects, by the same settings (docs/mechanics/sound.md); every screen's files are given them.
		_sounds = new Audio.CSoundEffects { Name = "SoundEffects" };
		_sounds.Setup(directories, () => _session?.Settings ?? _frontEnd?.Settings ?? _startSettings);
		AddChild(_sounds);
		_surfaces = new Map.CMapSurfaceKeeper(this);
		PlayTitleOnFirstFrame();
		if (CSession.UserArguments().Any(SaysWhatToPlay))
		{
			Guarded(StartSwitched());
		}
		else
		{
			// The game's switches want a game, which nothing has said to play; the front end plays only the clicks.
			string[] unused = CSession.UserArguments()
				.Where(argument => argument.StartsWith("--") && !FrontEndSwitches.Any(argument.StartsWith) && !CModSwitch.Is(argument)
					&& !CLaunchContract.Is(argument) && !argument.StartsWith(LuaSwitch))
				.ToArray();
			if (unused.Length > 0)
			{
				GD.Print($"No --country=, --load= or --replay=, so the main menu; left aside: {string.Join(" ", unused)}");
			}
			ShowFrontEnd();
		}
	}

	/// <summary>The developer's switch that runs the install's Lua on the original's interpreter: <c>--lua=5.1.4</c>.</summary>
	private const string LuaSwitch = "--lua=";

	/// <summary>
	/// The interpreter the install's Lua runs on, for the whole run: LuaJIT, or the original's Lua 5.1.4 that
	/// <c>--lua=5.1.4</c> or <c>OPENHOI3_LUA=5.1.4</c> chooses (docs/mechanics/lua.md, section 1). Before anything loads Lua.
	/// </summary>
	private static void ChooseLua()
	{
		if (CSession.UserArguments().LastOrDefault(argument => argument.StartsWith(LuaSwitch)) is { } lua
			&& !OpenHOI3.Lua.Native.CLuaInterpreter.Choose(lua[LuaSwitch.Length..]))
		{
			GD.PushError($"{lua} names no interpreter; it may be --lua=luajit or --lua=5.1.4. LuaJIT runs the scripts.");
		}
		GD.Print($"Lua: {OpenHOI3.Lua.Native.CLuaInterpreter.Description}");
	}

	/// <summary>
	/// The title theme once the first frame of ours is drawn - the loading screen, or the main menu - and not while the
	/// engine's own splash still stands, which it does until <c>_Ready</c> has made the first screen (docs/mechanics/music.md,
	/// section 3).
	/// </summary>
	private async void PlayTitleOnFirstFrame()
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		_music.PlayTitle();
	}

	/// <summary>
	/// The install to play, or null with the reason shown. The one the launcher names with <c>--install=</c>, first
	/// (<c>docs/launcher.md</c>, section 5); else, run from the editor, it is found by walking up from the project; an
	/// exported build plays the install it was placed in, the folder of its executable
	/// (<see cref="CDirectorySettings.BesideExecutable"/>).
	/// </summary>
	private CDirectorySettings? FindInstall()
	{
		if (CLaunchContract.Install(CSession.UserArguments()) is { } named)
		{
			CDirectorySettings? given = CDirectorySettings.Named(named);
			if (given is null)
			{
				ShowMessage("OpenHOI3 was told to play the Hearts of Iron 3 install at\n\n" + named
					+ "\n\nand this is not one: it has no interface, gfx and localisation folders.");
			}
			return given;
		}
		if (!OS.HasFeature("editor"))
		{
			string executable = OS.GetExecutablePath();
			CDirectorySettings? beside = CDirectorySettings.BesideExecutable(executable);
			if (beside is null)
			{
				ShowMessage("OpenHOI3 plays the Hearts of Iron 3 install it is placed in, and this is not one:\n\n" +
					System.IO.Path.GetDirectoryName(executable) +
					"\n\nStart it from the OpenHOI3 launcher, or put OpenHOI3.exe and its data folder beside hoi3_tfh.exe.");
			}
			return beside;
		}
		string projectDirectory = ProjectSettings.GlobalizePath("res://");
		CDirectorySettings? directories = CDirectorySettings.Discover(projectDirectory);
		if (directories is null)
		{
			ShowMessage("No Hearts of Iron 3 install was found. Looked in:\n\n" +
				string.Join("\n", CDirectorySettings.SearchedLocations(projectDirectory)));
		}
		return directories;
	}

	/// <summary>
	/// The install with the mods the command line selects laid over it (docs/mechanics/mods.md, section 3): the original's
	/// own reading of the engine's arguments - the original launcher starts it with <c>-mod=mod/&lt;file&gt;.mod</c>, and
	/// <c>-userdir=</c> moves the player's folder - and the developer's <c>--mod=&lt;name&gt;</c> after <c>--</c>. A mod
	/// named that is not there, or not valid, is played without, as the original plays it; here it is said.
	/// </summary>
	private static CDirectorySettings WithModsNamed(CDirectorySettings directories)
	{
		// The engine's own arguments, up to the user's after "--", joined back into the line the launcher wrote.
		string commandLine = string.Join(" ", OS.GetCmdlineArgs().TakeWhile(argument => argument != "--"));
		(List<string> values, string? userDir) = CModSwitch.Parse(commandLine);
		if (userDir is not null)
		{
			directories = directories.WithDocumentsFolder(userDir);
		}
		values.AddRange(CModSwitch.DeveloperNames(CSession.UserArguments())
			.Select(name => CModReader.VirtualPathFor(directories.GameRoot, directories.DocumentsFolder, name)));
		if (values.Count == 0)
		{
			return directories;
		}
		IReadOnlyList<CMod> mods = CModReader.Select(directories.GameRoot, directories.DocumentsFolder, values);
		if (mods.Count == 0)
		{
			CDiagnostics.Warn("No mod is selected, so the install is played as it is");
			return directories;
		}
		CDirectorySettings modded = directories.WithMods(mods);
		GD.Print($"Mods: {string.Join(", ", mods.Select(mod => mod.Name))} - {string.Join(", ", modded.Layers)}; "
			+ $"the player's folder {modded.UserFolder}");
		return modded;
	}

	/// <summary>The switches the front end plays itself, and the empty <c>--country=</c> that brought it up.</summary>
	private static readonly string[] FrontEndSwitches = { "--click=", "--screenshot=", "--country=", "--loadingshot=" };

	/// <summary>
	/// Whether an argument says what game to play: one of <see cref="GameSwitches"/> with something after it. An empty
	/// <c>--country=</c> - a launch configuration's prompt left empty - says nothing, and the main menu comes up.
	/// </summary>
	private static bool SaysWhatToPlay(string argument)
	{
		return GameSwitches.Any(name => argument.StartsWith(name) && argument[name.Length..].Trim().Length > 0);
	}

	/// <summary>A loading screen over everything, its lines written from the localisation given where one is read.</summary>
	private CLoadingScreen OpenLoadingScreen(CLocalisation? localisation = null)
	{
		var screen = new CLoadingScreen { Name = "LoadingScreen" };
		screen.Setup(_directories, firstOnly: _menuReached);
		AddChild(screen);
		screen.Localisation = localisation;
		return screen;
	}

	/// <summary>A new front end, any game ended first; called deferred, at the end of a frame.</summary>
	private void ShowFrontEnd()
	{
		Guarded(ShowFrontEndLoaded());
	}

	/// <summary>The front end read and made under the loading screen (<c>docs/mechanics/loadingscreen.md</c>).</summary>
	private async Task ShowFrontEndLoaded()
	{
		CLocalisation? known = _session?.Localisation;
		if (_session is not null)
		{
			RemoveChild(_session);
			_session.QueueFree();
			_session = null;
		}
		CLoadingScreen screen = OpenLoadingScreen(known);
		FrontEnd.CFrontEndContent content = await FrontEnd.CFrontEndContent.Load(screen, _directories, _surfaces);
		content.Gui.Sounds = _sounds;
		var frontEnd = new FrontEnd.CFrontEnd { Name = "FrontEnd", Surfaces = _surfaces };
		frontEnd.Setup(_directories, content, first: !_frontEndShown);
		_frontEndShown = true;
		frontEnd.PlayRequested += chosen => Guarded(Play(chosen));
		frontEnd.JoinRequested += (player, address) => Guarded(JoinFromMenu(frontEnd, player, address));
		frontEnd.JoinCancelled += () =>
		{
			_joinAttempt++;
			LeaveNet();
		};
		frontEnd.HostLobbyOpened += () => Guarded(HostLobby(frontEnd));
		frontEnd.NetLobbyLeft += () =>
		{
			_joinAttempt++;
			LeaveNet();
		};
		// The map made under "Creating the World...", the original stepping the pictures at the province names too - in two
		// pieces on the main thread, the map's surface and then the rest, with a frame drawn between (loadingscreen.md, section 3).
		await screen.OnMain(CLoadingSteps.World, frontEnd.PrepareMap, steps: 2);
		await screen.OnMain(CLoadingSteps.World, () =>
		{
			AddChild(frontEnd);
			MoveChild(frontEnd, 0);
		});
		_frontEnd = frontEnd;
		await screen.Settled();
		screen.QueueFree();
		_menuReached = true;
		frontEnd.Begin();
	}

	/// <summary>Whether a front end has been shown, the first alone playing the developer's clicks.</summary>
	private bool _frontEndShown;

	/// <summary>
	/// Whether the first main menu has come up: the original lets every loading picture but the first go then, so every
	/// loading screen after it shows the first (docs/mechanics/loadingscreen.md, section 1).
	/// </summary>
	private bool _menuReached;

	/// <summary>
	/// Play pressed: the loading screen up, the front end put away, the game made on the screen's thread and played; the
	/// front end freed.
	/// </summary>
	private async Task Play(CCountryTag chosen)
	{
		if (_frontEnd is not { } frontEnd)
		{
			return;
		}
		CLoadingScreen screen = OpenLoadingScreen(frontEnd.Localisation);
		await screen.Drawn();
		_startSettings = frontEnd.Settings;
		_frontEnd = null;
		RemoveChild(frontEnd);
		FrontEnd.CPreparedGame game;
		try
		{
			game = await screen.Work(CLoadingSteps.AdaptingHistory, () => frontEnd.Prepare(chosen));
		}
		finally
		{
			frontEnd.QueueFree();
		}
		if (frontEnd.HostAs is not null && game.State.Played.Count > 0)
		{
			// Host brought the lobby up (frontend.md, section 8): the game is hosted from its first frame. The machines
			// in the lobby wait to join it with the countries they chose, and are sent it at that frame
			// (docs/multiplayer.md, section 7).
			if (_net is Multiplayer.CNetHost lobby)
			{
				lobby.SetHostCountry(game.State.Played.First());
			}
			else
			{
				await Host(screen, game.State.Played.First(), Sim.Multiplayer.CLineLink.DefaultPort);
			}
		}
		await StartSession(screen, game);
	}

	/// <summary>
	/// A host's lobby opened: its port opened as soon as the play checksum is worked out, and the front end handed the
	/// host's part, through which it says how the lobby stands and hears what the others choose.
	/// </summary>
	private async Task HostLobby(FrontEnd.CFrontEnd frontEnd)
	{
		int attempt = ++_joinAttempt;
		string checksum = await Task.Run(PlayChecksum);
		if (attempt != _joinAttempt || _frontEnd != frontEnd || frontEnd.HostAs is null)
		{
			return;
		}
		var net = new Multiplayer.CNetHost(Sim.Multiplayer.CLineLink.DefaultPort, checksum, country: null);
		net.GameToLoad += (game, played) => Guarded(LoadNetGame(game, played));
		_net = net;
		frontEnd.LobbyNet = net;
		GD.Print($"Multiplayer: the lobby hosted on port {net.Port}, play checksum {checksum[..8]}.");
	}

	/// <summary>The game about to be played hosted: its port opened, and machines let in by the play checksum.</summary>
	private async Task Host(CLoadingScreen screen, CCountryTag country, int port)
	{
		string checksum = await screen.Work("", PlayChecksum);
		var net = new Multiplayer.CNetHost(port, checksum, country);
		net.GameToLoad += (game, played) => Guarded(LoadNetGame(game, played));
		_net = net;
		GD.Print($"Multiplayer: hosting on port {net.Port}, play checksum {checksum[..8]}.");
	}

	/// <summary>Counts the joins begun from the menu and cancelled, so that one cancelled knows it when its work comes back.</summary>
	private int _joinAttempt;

	/// <summary>
	/// An address given in the multiplayer menu (frontend.md, section 8): the connect log shown, up to three attempts to
	/// reach the host, then "Connecting..." - after them, as the original writes it - and the line that says how it went.
	/// A host that lets this machine in sends its game, which is loaded in the front end's place. The address may end in
	/// a colon and a port. *Ours*: the original's is the whole text, its port fixed.
	/// </summary>
	private async Task JoinFromMenu(FrontEnd.CFrontEnd frontEnd, string player, string address)
	{
		if (frontEnd.Multiplayer is not { } menu)
		{
			return;
		}
		int attempt = ++_joinAttempt;
		menu.ShowConnecting();
		// A name or an IPv4 address with a port after a colon, or an IPv6 address, bare or in brackets (CNetAddress).
		(address, int port) = Sim.Multiplayer.CNetAddress.Parse(address);
		string checksum = await Task.Run(PlayChecksum);
		Sim.Multiplayer.CLineLink? link = null;
		for (int tries = 0; tries < 3 && link is null && address.Length > 0; tries++)
		{
			try
			{
				link = await Task.Run(() => Sim.Multiplayer.CLineLink.Connect(address, port));
			}
			catch (System.Exception failed) when (failed is System.Net.Sockets.SocketException or System.IO.IOException)
			{
				// Tried again; said below where none came through.
			}
		}
		if (attempt != _joinAttempt || _frontEnd != frontEnd)
		{
			link?.Dispose();
			return;
		}
		menu.Log("CONNECTING");
		if (link is null)
		{
			menu.Log("GAME_NOT_FOUND");
			return;
		}
		var net = new Multiplayer.CNetClient(link, checksum, player, wanted: null);
		bool came = false;
		bool admitted = false;
		net.GameToLoad += (game, played) =>
		{
			if (!came && _frontEnd == frontEnd)
			{
				// A game is reached: the player's name and the last host are kept, as the original keeps them on reaching a lobby.
				frontEnd.RememberPlayer(player);
			}
			came = true;
			Guarded(LoadNetGame(game, played));
		};
		_net = net;
		GD.Print($"Multiplayer: joining {address}:{port} as {player}; play checksum {checksum[..8]}.");
		while (!came && attempt == _joinAttempt && _net == net)
		{
			if (net.Admitted && !admitted)
			{
				admitted = true;
				menu.Log("HOTJOIN_ACCEPTED");
			}
			// The host is in its lobby: this machine goes to the same lobby, and waits there for the host's Play.
			if (net.LobbyVersion > 0 && frontEnd.LobbyNet != net)
			{
				frontEnd.OpenLobbyAsClient(net, player);
			}
			if (net.Ended is not null)
			{
				string key = net.EndedKey ?? "CONNECTION_FAILED";
				if (frontEnd.LobbyNet == net)
				{
					// The lobby stays up, dead, with one line in its chat, until its Back - the original's way.
					frontEnd.SayLobbyIsDead(key == "YOUKICKED" ? key : "SERVER_LOST");
				}
				else
				{
					menu.Log(key);
				}
				LeaveNet();
				return;
			}
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}

	/// <summary>
	/// A game the developer switches say, made from them on the loading screen's thread (<see cref="CSwitchedGame"/>): the
	/// localisation first, so that the screen's lines can be written.
	/// </summary>
	private async Task StartSwitched()
	{
		CLoadingScreen screen = OpenLoadingScreen();
		CLocalisation localisation = await screen.Work("", () => CLocalisation.Load(_directories));
		screen.Localisation = localisation;
		if (CSession.Argument("--join=") is { } address)
		{
			await Join(screen, address);
			return;
		}
		CSwitchedGame made = await screen.Work(CLoadingSteps.History, () => CSwitchedGame.Make(_directories, localisation), steps: 2);
		Gui.CGuiContext gui = null!;
		await screen.OnMain(CLoadingSteps.GuiDefinitions, () => gui = Gui.CGuiContext.Load(_directories, made.Localisation));
		if (CSession.UserArguments().FirstOrDefault(argument => argument == "--host" || argument.StartsWith("--host=")) is { } host
			&& made.State.Played.Count > 0)
		{
			await Host(screen, made.State.Played.First(),
				host.Length > "--host=".Length && int.TryParse(host["--host=".Length..], out int named) ? named
					: Sim.Multiplayer.CLineLink.DefaultPort);
		}
		await StartSession(screen, new FrontEnd.CPreparedGame(made.State, made.Map, made.Localisation, gui, made.AiScripts),
			replayFile: made.ReplayFile, switched: true);
	}

	/// <summary>This machine's play checksum, in hexadecimal (docs/mechanics/checksum.md, section 8). Takes some seconds.</summary>
	private string PlayChecksum()
	{
		(byte[] hash, _) = Content.Checksum.CPlayChecksum.Compute(Content.Checksum.CChecksumSearchPath.ForPlay(_directories),
			engineVersion: "");
		return System.Convert.ToHexString(hash);
	}

	/// <summary>
	/// <c>--join=address[:port]</c>: the host reached, this machine let in by its play checksum with the name of
	/// <c>--name=</c> and the country of <c>--country=</c> asked for, and the game the host then sends waited for under
	/// the loading screen; it is loaded as every game the host sends after it (<see cref="LoadNetGame"/>).
	/// </summary>
	private async Task Join(CLoadingScreen screen, string address)
	{
		string checksum = await screen.Work("", PlayChecksum);
		// A name or an IPv4 address with a port after a colon, or an IPv6 address, bare or in brackets (CNetAddress).
		(address, int port) = Sim.Multiplayer.CNetAddress.Parse(address);
		string name = CSession.Argument("--name=") ?? System.Environment.UserName;
		var wanted = new CCountryTag(CSession.PlayerArgument() ?? CSwitchedGame.DefaultPlayer);
		Sim.Multiplayer.CLineLink link = await screen.Work("", () => Sim.Multiplayer.CLineLink.Connect(address, port));
		var net = new Multiplayer.CNetClient(link, checksum, name, wanted);
		bool came = false;
		net.GameToLoad += (game, played) =>
		{
			// The first comes under the screen that waited for it; a later one opens its own.
			Guarded(LoadNetGame(game, played, came ? null : screen));
			came = true;
		};
		_net = net;
		GD.Print($"Multiplayer: joining {address}:{port} as {name}, asking for {wanted.Value}; play checksum {checksum[..8]}.");
		// The host sends the game at its next frame that is free to; _Process takes the lines as they come.
		while (!came)
		{
			if (net.Ended is { } why)
			{
				LeaveNet();
				throw new System.InvalidOperationException(why);
			}
			await screen.Drawn();
		}
	}

	/// <summary>
	/// A game the host sent - or, on the host, the one it sent the others - loaded in place of whatever is played, with
	/// Lua states made now, and played on: what a machine joining and a machine whose game has parted both come down to
	/// (docs/multiplayer.md, sections 2 and 4.1). The save names the host's country as its player; the others played are
	/// taken up before the game is entered, so that every machine enters the same game.
	/// </summary>
	private async Task LoadNetGame(byte[] bytes, IReadOnlyList<CCountryTag> played, CLoadingScreen? screen = null)
	{
		CSession? old = _session;
		(FrontEnd.CPreparedGame Read, Debug.CSessionCarry Carry)? left = old?.LeaveForLoad();
		// Joined from the multiplayer menu, the front end is what the game takes the place of.
		FrontEnd.CFrontEnd? frontEnd = _frontEnd;
		screen ??= OpenLoadingScreen(left?.Read.Localisation ?? frontEnd?.Localisation);
		// Over the loading screen, what it is for: a game already being played is loaded again only to bring the
		// machines into step - a machine joined, or one was out of step. Ours (asked for on 2026-11-20).
		if (old is not null)
		{
			var syncing = new Label
			{
				Name = "Syncing",
				Text = "Syncing games...",
				HorizontalAlignment = HorizontalAlignment.Center,
				Position = new Vector2(0, 140),
				Size = new Vector2(1920, 60),
				MouseFilter = MouseFilterEnum.Ignore,
				ZIndex = 100,
			};
			syncing.AddThemeFontSizeOverride("font_size", 40);
			syncing.AddThemeColorOverride("font_outline_color", Colors.Black);
			syncing.AddThemeConstantOverride("outline_size", 10);
			screen.AddChild(syncing);
		}
		GD.Print($"Multiplayer: loading the game sent, {bytes.Length / 1024} KiB, played by {string.Join(", ", played)}.");
		// The frame that said so ends first: it may be the old session's own.
		await screen.Drawn();
		if (frontEnd is not null)
		{
			_startSettings = frontEnd.Settings;
			_frontEnd = null;
			RemoveChild(frontEnd);
			frontEnd.QueueFree();
		}
		if (old is not null)
		{
			RemoveChild(old);
			old.QueueFree();
			_session = null;
		}
		CLocalisation localisation = left?.Read.Localisation ?? screen.Localisation
			?? await screen.Work("", () => CLocalisation.Load(_directories));
		screen.Localisation = localisation;
		(Sim.CGameState State, Content.Map.CMap Map, OpenHOI3.Lua.CAiScripts Scripts) made = await screen.Work(CLoadingSteps.History, () =>
		{
			string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"openhoi3_net_{System.Environment.ProcessId}.hoi3");
			System.IO.File.WriteAllBytes(path, bytes);
			Content.Definitions.CCountries countries = Content.Definitions.CCountries.Load(_directories, localisation);
			Content.Map.CMap map = left?.Read.Map ?? Content.Map.CMap.Load(_directories);
			var scripts = new OpenHOI3.Lua.CAiScripts(_directories);
			(Sim.CGameState state, _) = Content.Saves.CSaveLoader.Load(_directories, map, localisation, countries, path,
				played.Count > 0 ? played[0] : null, scripts);
			foreach (CCountryTag other in played.Skip(1))
			{
				if (!state.Played.Contains(other) && state.Country(other) is { } country)
				{
					Sim.Commands.CSetPlayedCommand.Take(state, country);
				}
			}
			System.IO.File.Delete(path);
			return (state, map, scripts);
		}, steps: 2);
		Gui.CGuiContext? gui = left?.Read.Gui;
		if (gui is null)
		{
			await screen.OnMain(CLoadingSteps.GuiDefinitions, () => gui = Gui.CGuiContext.Load(_directories, localisation));
		}
		await StartSession(screen, new FrontEnd.CPreparedGame(made.State, made.Map, localisation, gui!, made.Scripts,
			left?.Read.Cache, left?.Read.Tiles), left?.Carry);
	}

	/// <summary>
	/// A game played: the session made under the loading screen - the map and the interface, then every map mode's surface -
	/// and the screen taken away.
	/// </summary>
	/// <param name="switched">Whether the developer switches made the game, rather than the front end: the switches' clicks
	/// are then the game's to play, where the front end played them itself.</param>
	private async Task StartSession(CLoadingScreen screen, FrontEnd.CPreparedGame game, Debug.CSessionCarry? carried = null,
		CReplayFile? replayFile = null, bool switched = false)
	{
		// The songs, read against the game's own names; a session made again around the same game keeps the music as it is.
		IReadOnlyList<CSong>? songs = null;
		if (carried is null)
		{
			songs = await screen.Work(CLoadingSteps.Sounds, () => CSongReader.Load(_directories, game.State, CDiagnostics.Problem));
		}
		_session?.QueueFree();
		// Nothing is heard while a game's screen is being built, as the original switches its sound off then; the files the
		// game's screens are built from are given the sounds.
		_sounds.Enabled = false;
		game.Gui.Sounds = _sounds;
		var session = new CSession { Name = "Session", Music = _music, PlaysClicks = switched, Surfaces = _surfaces, Net = _net };
		session.Setup(_directories, game, carried, replayFile);
		session.Resigned += () =>
		{
			// Left without the end-of-game screen, which plays its own track: the music stopped, as on every road out.
			_startSettings = session.Settings;
			_music.LeaveGame();
			// A game between machines is left with it: a host's leaving ends it for every machine.
			LeaveNet();
			CallDeferred(MethodName.ShowFrontEnd);
		};
		session.Replaced += (made, kept) =>
		{
			RemoveChild(session);
			session.QueueFree();
			_session = null;
			Guarded(StartSession(OpenLoadingScreen(made.Localisation), made, kept));
		};
		_session = session;
		// "Loading Graphics..." steps the pictures as the original's does on entering a game - which after the first main menu
		// has the first picture alone to show.
		await screen.OnMain(CLoadingSteps.Graphics, () =>
		{
			AddChild(session);
			MoveChild(session, 0);
		}, steps: 1);
		await session.PrepareMapModes(screen);
		screen.QueueFree();
		// The game's first frame: the title theme stopped and a song picked, in whichever country is played then.
		if (songs is not null)
		{
			_music.EnterGame(game.State, () => _session?.Player, songs);
		}
		_sounds.Enabled = true;
		session.Begin();
	}

	/// <summary>
	/// Runs a loading sequence, which nothing awaits: a failure in it is shown and logged rather than lost, since the
	/// loading screen would otherwise stand for ever.
	/// </summary>
	private async void Guarded(Task sequence)
	{
		try
		{
			await sequence;
		}
		catch (System.Exception failed)
		{
			GD.PushError(failed.ToString());
			foreach (Node child in GetChildren())
			{
				if (child is CLoadingScreen screen)
				{
					screen.QueueFree();
				}
			}
			ShowMessage("OpenHOI3 could not load:\n\n" + failed.Message);
		}
	}

	private void ShowMessage(string text)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(label);
	}
}
