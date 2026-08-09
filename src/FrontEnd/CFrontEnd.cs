using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using OpenHOI3.Core;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The front end, corresponding to the original's <c>CFrontEnd</c>.
///
/// It owns the shared backdrop and a set of <see cref="CFrontEndView"/> screens, and
/// switches which one is active. The views handle their own buttons; nothing about a
/// particular panel belongs here.
///
/// No layout is authored in Godot. It is read from the player's own
/// <c>interface/temp_frontend.gui</c>, whose <c>country_selection_panel</c> is the
/// fullscreen front end and declares the individual panels as siblings.
/// </summary>
public partial class CFrontEnd : Control
{
	private const string FrontEndGuiFile = "interface/temp_frontend.gui";
	private const string RootWindowName = "country_selection_panel";

	private readonly Dictionary<Type, CFrontEndView> _views = new();
	private readonly Dictionary<string, CGuiFile> _guiFiles = new(StringComparer.OrdinalIgnoreCase);

	private CGuiContext? _context;
	private CDirectorySettings? _directories;
	private TWindow? _root;
	private CFrontEndView? _active;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		_directories = CDirectorySettings.Discover();
		if (_directories is null)
		{
			ReportMissingGame();
			return;
		}
		GD.Print($"Game files: {_directories.GameRoot}");

		var timer = Stopwatch.StartNew();
		CLocalisation localisation = CLocalisation.Load(_directories);
		CSpriteTypes sprites = CSpriteTypes.Load(_directories);
		CBitmapFonts fonts = CBitmapFonts.Load(_directories);

		_context = new CGuiContext
		{
			Directories = _directories,
			Localisation = localisation,
			Sprites = sprites,
			Textures = new CTextureManager(_directories),
			Fonts = fonts,
		};
		GD.Print($"Loaded {localisation.Count} strings, {sprites.Count} sprites, " +
			$"{fonts.Count} fonts in {timer.ElapsedMilliseconds} ms");

		BuildFrontEnd();
	}

	private void BuildFrontEnd()
	{
		CGuiFile? gui = LoadGui(FrontEndGuiFile);
		CWindowType? rootType = gui?.FindWindow(RootWindowName);
		if (rootType is null)
		{
			GD.PushError($"'{RootWindowName}' not found in {FrontEndGuiFile}");
			return;
		}

		_root = new TWindow();
		_root.Initialise(rootType, _context!);
		AddChild(_root);

		// The panels are siblings inside the root window and all start visible, so
		// hide them up front and let the active view reveal its own.
		foreach (Node child in _root.GetChildren())
		{
			if (child is TWindow panel)
			{
				panel.Visible = false;
			}
		}

		Register(new CFrontEndState());
		Register(new CEasymodeView());
		Register(new CTutorialScreen());
		Register(new CMultiplayerMenuView());
		Register(new CFrontendSettingsScreen());
		Register(new CCreditsScreen());

		ShowVersion();
		Show<CFrontEndState>();
	}

	private void Register(CFrontEndView view)
	{
		TWindow? window = view.GuiFile is null
			? _root!.FindChildWindow(view.WindowName)
			: BuildExternalWindow(view.GuiFile, view.WindowName);

		if (window is null)
		{
			GD.PushWarning($"No window '{view.WindowName}' for {view.GetType().Name}");
			return;
		}
		_views[view.GetType()] = view;
		view.Attach(this, window);
	}

	/// <summary>Instantiates a window declared in some other interface file.</summary>
	private TWindow? BuildExternalWindow(string guiFile, string windowName)
	{
		CWindowType? type = LoadGui(guiFile)?.FindWindow(windowName);
		if (type is null)
		{
			return null;
		}
		var window = new TWindow();
		window.Initialise(type, _context!);
		AddChild(window);
		return window;
	}

	private CGuiFile? LoadGui(string gameRelativePath)
	{
		if (_guiFiles.TryGetValue(gameRelativePath, out CGuiFile? cached))
		{
			return cached;
		}
		string path = _directories!.Resolve(gameRelativePath);
		try
		{
			CGuiFile file = CGuiFile.ParseFile(path);
			_guiFiles[gameRelativePath] = file;
			return file;
		}
		catch (Exception e)
		{
			GD.PushError($"Could not parse '{path}': {e.Message}");
			return null;
		}
	}

	/// <summary>Makes a registered view the active screen.</summary>
	public void Show<T>() where T : CFrontEndView
	{
		if (!_views.TryGetValue(typeof(T), out CFrontEndView? view))
		{
			GD.PushWarning($"{typeof(T).Name} is not registered");
			return;
		}
		if (ReferenceEquals(_active, view))
		{
			return;
		}
		_active?.SetActive(false);
		_active = view;
		view.SetActive(true);
	}

	public void Quit()
	{
		GD.Print("Exiting.");
		GetTree().Quit();
	}

	/// <summary>Fills in the label the front end reserves for the build version.</summary>
	private void ShowVersion()
	{
		if (_root?.FindElement("version_label") is CTextBox versionLabel)
		{
			versionLabel.SetText($"OpenHOI3 (Godot {Engine.GetVersionInfo()["string"]})");
		}
	}

	private void ReportMissingGame()
	{
		GD.PushError("Hearts of Iron 3 installation not found. Looked in:\n  " +
			string.Join("\n  ", CDirectorySettings.SearchedLocations()));

		var message = new Label
		{
			Text = "Hearts of Iron 3 game files not found.\n\n" +
				"Place your installation in a \"Hearts of Iron 3\" folder\n" +
				"next to the repository, as described in the readme.",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		message.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(message);
	}
}
