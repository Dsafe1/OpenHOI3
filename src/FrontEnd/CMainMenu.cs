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
	public event Action? SettingsPressed;
	public event Action? ExitPressed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context)
	{
		_context = context;
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
		// Ours where the original writes its version and four letters of its checksum (frontend.md, Ours).
		if (_window.FindTextBox("version_label") is { } version)
		{
			version.Text = "OpenHOI3";
		}
		if (_window.FindWindow(PanelName) is not { } menuPanel)
		{
			return;
		}
		menuPanel.Visible = true;
		Bind(menuPanel, "single_player_button", () => SinglePlayerPressed?.Invoke());
		Bind(menuPanel, "settings_button", () => SettingsPressed?.Invoke());
		Bind(menuPanel, "exit_button", () => ExitPressed?.Invoke());
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
		foreach (string greyed in new[] { "multi_player_button", "tutorial_button", "credits_button" })
		{
			if (menuPanel.TryFindButton(greyed) is { } button)
			{
				button.Disabled = true;
			}
		}
		// Quick Start and the DLC store are shown, greyed, saying why they are not offered (docs/compatibility.md, entry 17;
		// frontend.md, Ours).
		foreach ((string name, string why) in new[]
		{
			("toggle_quick_normal", CLobbyText.QuickStartNotOffered), ("gg_micro_button", CLobbyText.StoreNotOffered),
		})
		{
			if (menuPanel.TryFindButton(name) is { } button)
			{
				button.Disabled = true;
				button.Tooltip = () => new CTooltip { Text = why };
			}
		}
	}

	/// <summary>The main menu's panel shown or hidden on its backdrop, as the original hides it for another view.</summary>
	public void ShowPanel(bool shown)
	{
		if (_window?.FindWindow(PanelName) is { } panel)
		{
			panel.Visible = shown;
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
