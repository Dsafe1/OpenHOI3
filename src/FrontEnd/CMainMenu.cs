using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The main menu: <c>temp_frontend.gui</c>'s <c>country_selection_panel</c> - its backdrop over the whole of the map, the
/// engine's logo and the version - with <c>mainmenu_panel</c> on it. Single Player opens the lobby, Settings the front end's
/// settings window, Exit ends the program at once; what is not built is greyed and what is not offered hidden. See
/// <c>docs/mechanics/frontend.md</c>, section 2.
/// </summary>
public partial class CMainMenu : Control
{
	public const string WindowName = "country_selection_panel";
	public const string PanelName = "mainmenu_panel";

	/// <summary>The panels of the window other than the main menu, which the original shows as other views.</summary>
	private static readonly string[] OtherPanels =
	{
		"easymode_panel", "tutorial_panel", "multiplayer_menu", "multiplayer_connect_menu", "settings",
	};

	private CGuiContext _context = null!;
	private CGuiWindow? _window;

	public event Action? SinglePlayerPressed;

	/// <summary>Multi Player pressed, the main menu's panel hidden: the multiplayer menu is to be shown.</summary>
	public event Action? MultiplayerPressed;

	/// <summary>The multiplayer menu; made as the node enters the tree.</summary>
	public CMultiplayerMenu? Multiplayer { get; private set; }
	public event Action? SettingsPressed;
	public event Action? ExitPressed;

	private string _versionLabel = Interface.Screens.CVersionText.Name;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, string versionLabel)
	{
		_context = context;
		_versionLabel = versionLabel;
	}

	public override void _Ready()
	{
		Name = "MainMenu";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		foreach (string name in OtherPanels)
		{
			if (_window.FindWindow(name) is { } panel)
			{
				panel.Visible = false;
			}
		}
		// Our name where the original writes its version, then the four letters of its file checksum (checksum.md, section 5).
		if (_window.FindTextBox("version_label") is { } version)
		{
			version.Text = _versionLabel;
		}
		if (_window.FindWindow(PanelName) is not { } menuPanel)
		{
			return;
		}
		// The multiplayer menu, whose panels are this window's and already hidden (frontend.md, section 8).
		Multiplayer = new CMultiplayerMenu(_context, _window, this);
		Multiplayer.BackPressed += () =>
		{
			// Back clears simple and comes to the main menu, as closing the settings window does.
			ShowMainMenu();
			ShowPanel(true);
		};
		Bind(menuPanel, "multi_player_button", () =>
		{
			ShowPanel(false);
			MultiplayerPressed?.Invoke();
		});
		Bind(menuPanel, "single_player_button", () => SinglePlayerPressed?.Invoke());
		Bind(menuPanel, "settings_button", () => SettingsPressed?.Invoke());
		Bind(menuPanel, "exit_button", () => ExitPressed?.Invoke());
		Bind(menuPanel, QuickToggleName, () => Switch(quick: true));
		BindQuickStart();
		ShowPanel(true);
		// Hidden as the original always hides it; then what is not built greyed, and what is not offered hidden.
		foreach (string hidden in new[]
		{
			"nudge_button", "homepage_button", "forum_button", "facebook_button", "twitter_button",
		})
		{
			if (menuPanel.TryFindButton(hidden) is { } button)
			{
				button.Visible = false;
			}
		}
		foreach (string greyed in Multiplayer.Exists ? new[] { "tutorial_button", "credits_button" }
			: new[] { "multi_player_button", "tutorial_button", "credits_button" })
		{
			if (menuPanel.TryFindButton(greyed) is { } button)
			{
				button.Disabled = true;
			}
		}
		// The DLC store is shown, greyed, saying why it is not offered (docs/compatibility.md, entry 17; frontend.md, Ours).
		if (menuPanel.TryFindButton("gg_micro_button") is { } store)
		{
			store.Disabled = true;
			store.Tooltip = () => new CTooltip { Text = CLobbyText.StoreNotOffered };
		}
	}

	/// <summary>
	/// The Quick Start panel, <c>easymode_panel</c>: each of its four buttons starts its game with no lobby, Custom Start
	/// goes back to the main menu, Exit ends the program, and the tutorial is greyed as on the main menu
	/// (<c>docs/mechanics/bookmarks.md</c>, section 4).
	/// </summary>
	private void BindQuickStart()
	{
		if (_window?.FindWindow(QuickPanelName) is not { } quickPanel)
		{
			return;
		}
		foreach (CQuickStart start in CQuickStart.All)
		{
			CQuickStart pressed = start;
			Bind(quickPanel, start.ButtonName, () => QuickStartPressed?.Invoke(pressed));
		}
		Bind(quickPanel, QuickToggleName, () => Switch(quick: false));
		Bind(quickPanel, "exit_button", () => ExitPressed?.Invoke());
		if (quickPanel.TryFindButton("tutorial_button") is { } tutorial)
		{
			tutorial.Disabled = true;
		}
	}

	private const string QuickPanelName = "easymode_panel";
	private const string QuickToggleName = "toggle_quick_normal";

	/// <summary>
	/// Whether the Quick Start panel is the one shown, in place of the main menu: the settings' <c>simple</c> at the start, and
	/// kept in step with it as the toggle moves between the two (<c>reversing/findings/FINDINGS-frontend.md</c>, A.1).
	/// </summary>
	public bool QuickShown { get; private set; }

	/// <summary>A Quick Start button pressed, with the start it names.</summary>
	public event Action<CQuickStart>? QuickStartPressed;

	/// <summary>The toggle moved the menu to the Quick Start panel or back: the settings' <c>simple</c> follows it.</summary>
	public event Action<bool>? Switched;

	/// <summary>The panel the front end opens on, by the settings' <c>simple</c>; has to be set before the node enters the tree.</summary>
	public void Begin(bool simple)
	{
		QuickShown = simple;
	}

	/// <summary>The main menu, not the Quick Start panel, next time a panel is shown: what closing the settings window does.</summary>
	public void ShowMainMenu()
	{
		if (QuickShown)
		{
			QuickShown = false;
			Switched?.Invoke(false);
		}
	}

	private void Switch(bool quick)
	{
		QuickShown = quick;
		Switched?.Invoke(quick);
		ShowPanel(true);
	}

	/// <summary>
	/// The panel the menu is on - the main menu's or the Quick Start's - shown or hidden on its backdrop, as the original
	/// hides it for another view; the other of the two hidden either way.
	/// </summary>
	public void ShowPanel(bool shown)
	{
		if (_window?.FindWindow(PanelName) is { } panel)
		{
			panel.Visible = shown && !QuickShown;
		}
		if (_window?.FindWindow(QuickPanelName) is { } quickPanel)
		{
			quickPanel.Visible = shown && QuickShown;
		}
	}

	private static void Bind(CGuiWindow window, string name, Action pressed)
	{
		if (window.TryFindButton(name) is { } button)
		{
			button.Pressed += pressed;
		}
	}
}
