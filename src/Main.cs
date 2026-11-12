using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OpenHOI3.Content;
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

	/// <summary>The player's settings as the program started, which the music goes by before a screen has its own.</summary>
	private Content.Settings.CUserSettings _startSettings = Content.Settings.CUserSettings.Default;

	/// <summary>The switches that say what game to play, which start it without the front end.</summary>
	private static readonly string[] GameSwitches = { "--country=", "--load=", "--replay=" };

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

		GD.Print("OpenHOI3 starting.");
		MouseFilter = MouseFilterEnum.Ignore;
		if (CSession.UserArguments().FirstOrDefault(a => a.StartsWith(Debug.CShaderCompileCheck.Switch)) is { } shaderCheck)
		{
			Debug.CShaderCompileCheck.Run(shaderCheck[Debug.CShaderCompileCheck.Switch.Length..], GetTree());
			return;
		}

		CDirectorySettings? directories = FindInstall();
		if (directories is null)
		{
			return;
		}

		GD.Print($"Install: {directories.GameRoot}");
		_directories = directories;
		// The title theme from the load's first moment, as the original starts it before its first loading frame
		// (docs/mechanics/music.md, section 3). The settings it is played by are the screen's that has them, else the file's.
		_startSettings = Content.Settings.CUserSettingsReader.Read(Content.Settings.CUserSettingsReader.DefaultPath());
		_music = new Audio.CMusic { Name = "Music" };
		_music.Setup(directories, () => _session?.Settings ?? _frontEnd?.Settings ?? _startSettings);
		AddChild(_music);
		_music.PlayTitle();
		if (CSession.UserArguments().Any(SaysWhatToPlay))
		{
			Guarded(StartSwitched());
		}
		else
		{
			// The game's switches want a game, which nothing has said to play; the front end plays only the clicks.
			string[] unused = CSession.UserArguments()
				.Where(argument => argument.StartsWith("--") && !FrontEndSwitches.Any(argument.StartsWith)).ToArray();
			if (unused.Length > 0)
			{
				GD.Print($"No --country=, --load= or --replay=, so the main menu; left aside: {string.Join(" ", unused)}");
			}
			ShowFrontEnd();
		}
	}

	/// <summary>
	/// The install to play, or null with the reason shown. Run from the editor, it is found by walking up from the
	/// project; an exported build plays the install it was placed in, the folder of its executable
	/// (<see cref="CDirectorySettings.BesideExecutable"/>).
	/// </summary>
	private CDirectorySettings? FindInstall()
	{
		if (!OS.HasFeature("editor"))
		{
			string executable = OS.GetExecutablePath();
			CDirectorySettings? beside = CDirectorySettings.BesideExecutable(executable);
			if (beside is null)
			{
				ShowMessage("OpenHOI3 plays the Hearts of Iron 3 install it is placed in, and this is not one:\n\n" +
					System.IO.Path.GetDirectoryName(executable) +
					"\n\nPut OpenHOI3.exe and its data folder beside hoi3_tfh.exe.");
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
		FrontEnd.CFrontEndContent content = await FrontEnd.CFrontEndContent.Load(screen, _directories);
		var frontEnd = new FrontEnd.CFrontEnd { Name = "FrontEnd" };
		frontEnd.Setup(_directories, content, first: !_frontEndShown);
		_frontEndShown = true;
		frontEnd.PlayRequested += chosen => Guarded(Play(chosen));
		// The map made under "Creating the World...", the original stepping the pictures at the province names too.
		await screen.OnMain(CLoadingSteps.World, () =>
		{
			AddChild(frontEnd);
			MoveChild(frontEnd, 0);
		}, steps: 2);
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
		await StartSession(screen, game);
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
		CSwitchedGame made = await screen.Work(CLoadingSteps.History, () => CSwitchedGame.Make(_directories, localisation), steps: 2);
		Gui.CGuiContext gui = null!;
		await screen.OnMain(CLoadingSteps.GuiDefinitions, () => gui = Gui.CGuiContext.Load(_directories, made.Localisation));
		await StartSession(screen, new FrontEnd.CPreparedGame(made.State, made.Map, made.Localisation, gui, made.AiScripts),
			replayFile: made.ReplayFile);
	}

	/// <summary>
	/// A game played: the session made under the loading screen - the map and the interface, then every map mode's surface -
	/// and the screen taken away.
	/// </summary>
	private async Task StartSession(CLoadingScreen screen, FrontEnd.CPreparedGame game, Debug.CSessionCarry? carried = null,
		CReplayFile? replayFile = null)
	{
		// The songs, read against the game's own names; a session made again around the same game keeps the music as it is.
		IReadOnlyList<CSong>? songs = null;
		if (carried is null)
		{
			songs = await screen.Work(CLoadingSteps.Sounds, () => CSongReader.Load(_directories, game.State, CDiagnostics.Warn));
		}
		_session?.QueueFree();
		var session = new CSession { Name = "Session", Music = _music };
		session.Setup(_directories, game, carried, replayFile);
		session.Resigned += () =>
		{
			// Left without the end-of-game screen, which plays its own track: the music stopped, as on every road out.
			_music.LeaveGame();
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
