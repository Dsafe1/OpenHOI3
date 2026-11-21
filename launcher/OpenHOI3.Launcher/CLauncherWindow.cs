using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using OpenHOI3.Content;
using OpenHOI3.Content.Launch;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Launcher;

/// <summary>
/// The launcher's one window (<c>docs/launcher.md</c>): the install, its mods with a box to tick each, which engine the
/// choice is played on or why none can be, and Play, which starts that engine and closes the launcher as the original's
/// does.
/// </summary>
public sealed class CLauncherWindow : Window
{
	private static readonly IBrush Quiet = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
	private static readonly IBrush Warning = new SolidColorBrush(Color.FromRgb(0xE8, 0xB0, 0x50));

	private readonly string _settingsFile = CLauncherSettingsFile.DefaultPath();
	private readonly CEngine? _own;
	private readonly string? _ownProblem;
	private readonly TextBlock _install = new() { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
	private readonly StackPanel _modRows = new() { Spacing = 6 };
	private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
	private readonly Button _play = new() { Content = "Play", MinWidth = 120, HorizontalContentAlignment = HorizontalAlignment.Center };
	private readonly List<CheckBox> _boxes = new();

	private string? _gameRoot;
	private List<CModEntry> _entries = new();
	private List<CMod> _mods = new();
	private HashSet<string> _picks = new(StringComparer.Ordinal);

	public CLauncherWindow(string[] arguments)
	{
		Title = "OpenHOI3";
		Width = 640;
		Height = 560;
		MinWidth = 480;
		MinHeight = 360;
		Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://OpenHOI3.Launcher/Assets/icon.ico")));
		FontFamily = new FontFamily("avares://OpenHOI3.Launcher/Assets/Fonts#Liberation Sans");
		FontSize = 14;

		CLauncherArguments given = CLauncherArguments.Read(arguments);
		string ownFolder = given.Engine ?? CEngineFile.OwnFolder(AppContext.BaseDirectory);
		var problems = new List<string>();
		_own = CEngineFile.Read(ownFolder, problems.Add);
		_ownProblem = problems.FirstOrDefault();

		Content = Layout();
		// What the command line names comes before what is remembered: a mod's shortcut comes up on its install and mod.
		CLauncherSettings remembered = given.Over(CLauncherSettingsFile.Read(_settingsFile));
		string? found = CInstallSearch.Find(CInstallSearch.Candidates(remembered.Install, AppContext.BaseDirectory,
			CInstallSearch.SteamLibraries(CSteamFolder.Find())));
		UseInstall(found, remembered.Mods);
	}

	private Control Layout()
	{
		var choose = new Button { Content = "Choose...", VerticalAlignment = VerticalAlignment.Center };
		choose.Click += async (_, _) => await ChooseInstall();
		_play.Click += (_, _) => Play();

		var installRow = new DockPanel { LastChildFill = true };
		DockPanel.SetDock(choose, Dock.Right);
		installRow.Children.Add(choose);
		installRow.Children.Add(new StackPanel
		{
			Spacing = 2,
			Children =
			{
				new TextBlock { Text = "Hearts of Iron 3", FontWeight = FontWeight.Bold },
				_install,
			},
		});

		var top = new StackPanel
		{
			Spacing = 14,
			Children =
			{
				new TextBlock { Text = "OpenHOI3", FontSize = 26, FontWeight = FontWeight.Bold },
				installRow,
				new TextBlock { Text = "Mods", FontWeight = FontWeight.Bold },
			},
		};

		var bottom = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 12, 0, 0) };
		DockPanel.SetDock(_play, Dock.Right);
		bottom.Children.Add(_play);
		bottom.Children.Add(new Border { Padding = new Thickness(0, 0, 12, 0), Child = _status });

		var page = new DockPanel { LastChildFill = true, Margin = new Thickness(20) };
		DockPanel.SetDock(top, Dock.Top);
		DockPanel.SetDock(bottom, Dock.Bottom);
		page.Children.Add(top);
		page.Children.Add(bottom);
		page.Children.Add(new Border
		{
			Margin = new Thickness(0, 6, 0, 0),
			Padding = new Thickness(10),
			BorderThickness = new Thickness(1),
			BorderBrush = Quiet,
			CornerRadius = new CornerRadius(4),
			Child = new ScrollViewer { Content = _modRows },
		});
		return page;
	}

	/// <summary>Takes an install - or none - lists its mods and ticks what is remembered.</summary>
	private void UseInstall(string? gameRoot, IEnumerable<string> remembered)
	{
		_gameRoot = gameRoot;
		_install.Text = gameRoot ?? "Not found. Choose the folder Hearts of Iron 3: Their Finest Hour is installed in.";
		_install.Foreground = gameRoot is null ? Warning : Quiet;
		_entries = gameRoot is null ? new() : CModList.Read(gameRoot, CDirectorySettings.DefaultDocumentsFolder());
		_mods = CModList.Playable(_entries);
		_picks = CModPicks.Restore(_mods, remembered);
		BuildRows();
		Refresh();
	}

	private void BuildRows()
	{
		_modRows.Children.Clear();
		_boxes.Clear();
		if (_gameRoot is not null && _entries.Count == 0)
		{
			_modRows.Children.Add(new TextBlock { Text = "This install has no mods.", Foreground = Quiet });
		}
		foreach (CModEntry entry in _entries)
		{
			var lines = new StackPanel { Spacing = 1 };
			lines.Children.Add(new TextBlock { Text = entry.Mod?.Name ?? Path.GetFileNameWithoutExtension(entry.File), FontWeight = FontWeight.Bold });
			var details = new List<string> { entry.VirtualPath };
			if (entry.Mod?.Engine is { } engine)
			{
				details.Add($"made for engine {engine}");
			}
			if (entry.Mod is { Dependencies.Count: > 0 } withDependencies)
			{
				details.Add("needs " + string.Join(", ", withDependencies.Dependencies));
			}
			lines.Children.Add(new TextBlock { Text = string.Join(" - ", details), Foreground = Quiet, FontSize = 12, TextWrapping = TextWrapping.Wrap });
			var warnings = entry.Problems.ToList();
			if (entry.Refusal is { } refusal)
			{
				warnings.Add(refusal);
			}
			if (entry.Mod is { } mod)
			{
				warnings.AddRange(CModPicks.Missing(_mods, mod).Select(name => $"Needs \"{name}\", which this install does not have"));
			}
			foreach (string warning in warnings)
			{
				lines.Children.Add(new TextBlock { Text = warning, Foreground = Warning, FontSize = 12, TextWrapping = TextWrapping.Wrap });
			}
			var box = new CheckBox { Content = lines, Tag = entry.Mod, IsEnabled = entry.Mod is not null };
			box.Click += (_, _) => Tick(box);
			_boxes.Add(box);
			_modRows.Children.Add(box);
		}
	}

	private void Tick(CheckBox box)
	{
		if (box.Tag is CMod mod)
		{
			_picks = CModPicks.Set(_mods, _picks, mod, box.IsChecked == true);
		}
		Refresh();
	}

	/// <summary>The boxes ticked as the picks are, and the engine the choice is played on, or why it cannot be.</summary>
	private void Refresh()
	{
		foreach (CheckBox box in _boxes)
		{
			box.IsChecked = box.Tag is CMod mod && _picks.Contains(mod.VirtualPath);
		}
		CEngineChoiceResult choice = Choice();
		_play.IsEnabled = _gameRoot is not null && choice.Engine is not null;
		if (_gameRoot is null)
		{
			_status.Text = "";
			return;
		}
		List<CMod> chosen = CModPicks.Chosen(_mods, _picks);
		if (choice.Engine is not { } engine)
		{
			// Where the launcher's own engine was wanted, why it has none.
			bool ownWanted = chosen.All(mod => mod.Engine is null);
			_status.Text = ownWanted && _ownProblem is not null ? $"{choice.Problem}. {_ownProblem}" : choice.Problem;
			_status.Foreground = Warning;
			return;
		}
		string what = chosen.Count == 0 ? "Their Finest Hour" : string.Join(", ", chosen.Select(mod => mod.Name));
		string whose = engine == _own ? "the launcher's own engine" : "the engine the mod ships";
		_status.Text = $"{what}, on {whose}, {engine.Version}";
		_status.Foreground = Quiet;
	}

	private CEngineChoiceResult Choice()
	{
		return CEngineChoice.Choose(CModPicks.Chosen(_mods, _picks), _own);
	}

	private async System.Threading.Tasks.Task ChooseInstall()
	{
		IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			Title = "The folder Hearts of Iron 3: Their Finest Hour is installed in",
			AllowMultiple = false,
		});
		if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } folder)
		{
			return;
		}
		if (!CDirectorySettings.IsInstall(folder))
		{
			_status.Text = $"{folder} is not a Hearts of Iron 3 install: it has no interface, gfx and localisation folders.";
			_status.Foreground = Warning;
			return;
		}
		UseInstall(Path.GetFullPath(folder), _picks);
		Remember();
	}

	private void Play()
	{
		CEngineChoiceResult choice = Choice();
		if (_gameRoot is null || choice.Engine is not { } engine)
		{
			return;
		}
		List<CMod> chosen = CModPicks.Chosen(_mods, _picks);
		var start = new ProcessStartInfo(engine.Executable) { WorkingDirectory = engine.Folder, UseShellExecute = false };
		foreach (string argument in CLaunchContract.Arguments(_gameRoot, chosen.Select(mod => mod.VirtualPath)))
		{
			start.ArgumentList.Add(argument);
		}
		Remember();
		try
		{
			Process.Start(start);
		}
		catch (Win32Exception exception)
		{
			_status.Text = $"{engine.Executable} could not be started: {exception.Message}";
			_status.Foreground = Warning;
			return;
		}
		Close();
	}

	private void Remember()
	{
		try
		{
			CLauncherSettingsFile.Write(_settingsFile, new CLauncherSettings(_gameRoot,
				CModPicks.Chosen(_mods, _picks).Select(mod => mod.VirtualPath).ToList()));
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// Not remembering is no reason not to play.
		}
	}
}
