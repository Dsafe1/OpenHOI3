using System;
using Avalonia;

namespace OpenHOI3.Launcher;

/// <summary>
/// The launcher's entry point (<c>docs/launcher.md</c>). One switch of its own, for development: <c>--engine=&lt;folder&gt;</c>
/// takes the engine in that folder as the launcher's own, in place of the one in its <c>engine</c> folder.
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
