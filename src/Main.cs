using Godot;
using OpenHOI3.Content;

namespace OpenHOI3;

/// <summary>
/// Entry point, and the boot scene.
///
/// Today it finds the install and starts a game straight away. It is its own step
/// rather than part of the game so that a menu has somewhere to arrive: a game is a
/// <see cref="CSession"/> this node creates, and can free and create again.
/// </summary>
public partial class Main : Control
{
	private CSession? _session;

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

		string projectDirectory = ProjectSettings.GlobalizePath("res://");
		CDirectorySettings? directories = CDirectorySettings.Discover(projectDirectory);
		if (directories is null)
		{
			ShowMessage("No Hearts of Iron 3 install was found. Looked in:\n\n" +
				string.Join("\n", CDirectorySettings.SearchedLocations(projectDirectory)));
			return;
		}

		GD.Print($"Install: {directories.GameRoot}");
		StartSession(directories);
	}

	private void StartSession(CDirectorySettings directories)
	{
		_session?.QueueFree();
		_session = new CSession { Name = "Session" };
		_session.Setup(directories);
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
