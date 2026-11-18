using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace OpenHOI3.Launcher;

/// <summary>The launcher's application: Avalonia's Fluent look, dark, and the one window.</summary>
public sealed class CLauncherApp : Application
{
	/// <summary>The command line the launcher was started with.</summary>
	public static string[] Arguments { get; set; } = Array.Empty<string>();

	public override void Initialize()
	{
		Styles.Add(new FluentTheme());
		RequestedThemeVariant = ThemeVariant.Dark;
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new CLauncherWindow(Arguments);
		}
		base.OnFrameworkInitializationCompleted();
	}
}
