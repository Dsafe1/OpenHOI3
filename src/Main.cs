using Godot;
using System.Linq;
using OpenHOI3.Content;

namespace OpenHOI3;

/// <summary>
/// Entry point, and the boot scene.
///
/// It finds the install and brings up the front end - the main menu and the lobby - which hands over the game Play makes;
/// a game resigned brings the front end back. A developer switch that says what to play starts that game at once. A game
/// is a <see cref="CSession"/> this node creates, frees and creates again (<c>docs/mechanics/frontend.md</c>, section 1).
/// </summary>
public partial class Main : Control
{
	private CSession? _session;
	private FrontEnd.CFrontEnd? _frontEnd;
	private CDirectorySettings _directories = null!;

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

		string projectDirectory = ProjectSettings.GlobalizePath("res://");
		CDirectorySettings? directories = CDirectorySettings.Discover(projectDirectory);
		if (directories is null)
		{
			ShowMessage("No Hearts of Iron 3 install was found. Looked in:\n\n" +
				string.Join("\n", CDirectorySettings.SearchedLocations(projectDirectory)));
			return;
		}

		GD.Print($"Install: {directories.GameRoot}");
		_directories = directories;
		if (CSession.UserArguments().Any(SaysWhatToPlay))
		{
			StartSession(null);
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

	/// <summary>The switches the front end plays itself, and the empty <c>--country=</c> that brought it up.</summary>
	private static readonly string[] FrontEndSwitches = { "--click=", "--screenshot=", "--country=" };

	/// <summary>
	/// Whether an argument says what game to play: one of <see cref="GameSwitches"/> with something after it. An empty
	/// <c>--country=</c> - a launch configuration's prompt left empty - says nothing, and the main menu comes up.
	/// </summary>
	private static bool SaysWhatToPlay(string argument)
	{
		return GameSwitches.Any(name => argument.StartsWith(name) && argument[name.Length..].Trim().Length > 0);
	}

	/// <summary>A new front end, any game ended first.</summary>
	private void ShowFrontEnd()
	{
		if (_session is not null)
		{
			RemoveChild(_session);
			_session.QueueFree();
			_session = null;
		}
		_frontEnd = new FrontEnd.CFrontEnd { Name = "FrontEnd" };
		_frontEnd.Setup(_directories, first: !_frontEndShown);
		_frontEndShown = true;
		_frontEnd.Launched += game =>
		{
			_launching = game;
			CallDeferred(MethodName.Launch);
		};
		AddChild(_frontEnd);
	}

	/// <summary>Whether a front end has been shown, the first alone playing the developer's clicks.</summary>
	private bool _frontEndShown;

	/// <summary>The game Play made, waiting for the frame's end to be handed to a session.</summary>
	private FrontEnd.CPreparedGame? _launching;

	/// <summary>The game the lobby made, played; the front end gone.</summary>
	private void Launch()
	{
		if (_launching is not { } game)
		{
			return;
		}
		_launching = null;
		if (_frontEnd is not null)
		{
			RemoveChild(_frontEnd);
			_frontEnd.QueueFree();
			_frontEnd = null;
		}
		StartSession(game);
	}

	private void StartSession(FrontEnd.CPreparedGame? game)
	{
		_session?.QueueFree();
		_session = new CSession { Name = "Session" };
		_session.Setup(_directories, game);
		_session.Resigned += () => CallDeferred(MethodName.ShowFrontEnd);
		AddChild(_session);
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
