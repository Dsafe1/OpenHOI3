using System;
using Avalonia;

namespace OpenHOI3.Launcher;

/// <summary>
/// The launcher's entry point (<c>docs/launcher.md</c>). Its switches are <see cref="OpenHOI3.Content.Launch.CLauncherArguments"/>:
/// <c>--install=</c> and <c>--mod=</c>, by which a shortcut names what it comes up on, and for development
/// <c>--engine=&lt;folder&gt;</c>, the engine to take as the launcher's own.
/// </summary>
public static class CLauncherProgram
{
	[STAThread]
	public static void Main(string[] args)
	{
		CLauncherApp.Arguments = args;
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	/// <summary>The application as Avalonia's tools ask for it.</summary>
	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<CLauncherApp>().UsePlatformDetect().LogToTrace();
	}
}
