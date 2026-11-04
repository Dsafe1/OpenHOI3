using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Messages;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The in-game menu: <c>menubar.gui</c>'s window <c>menu</c>, its six buttons on <c>menu_panel</c>, the dialog Quit
/// opens and the message settings, <c>menu_message_settings</c>. Opening it stops the clock where it runs and always
/// shows the six buttons; Close from one of its windows goes back to them, and leaves the clock stopped - the
/// original's. Game Options opens <see cref="CGameOptionsWindow"/>; Save Game and Resign are greyed: what they lead to is
/// not built. See
/// <c>docs/mechanics/ingamemenu.md</c>.
/// </summary>
public sealed partial class CInGameMenu : Control
{
	public const string WindowName = "menu";
	public const string RowName = "message_entry";
	private const string DialogName = "DefaultDialog";

	private CGuiContext _context = null!;
	private CDirectorySettings _directories = null!;
	private CSimulation _simulation = null!;
	private CCountry? _player;
	private CGuiWindow? _window;
	private CGuiModalBlocker? _blocker;
	private CGuiWindow? _panel;
	private CGuiWindow? _save;
	private CGuiWindow? _settings;
	private CGuiListBox? _list;
	private readonly List<(string Name, EMessageCategory? Category, CGuiButton Box)> _tabs = new();
	private CConfirmDialog? _dialog;

	/// <summary>Whether the clock was stopped when the menu last opened; the original's starts out set.</summary>
	private bool _pausedBefore = true;

	/// <summary>Raised as the menu opens: the side menus and a country view are to shut and the selection go.</summary>
	public event Action? Opening;

	public bool IsOpen => _window is { Visible: true };

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="options">The game options window Game Options opens, its Back and Apply coming back through the menu's close.</param>
	public void Setup(CGuiContext context, CDirectorySettings directories, CSimulation simulation, CCountry? player,
		CGameOptionsWindow? options)
	{
		_context = context;
		_directories = directories;
		_simulation = simulation;
		_player = player;
		_options = options;
	}

	private CGameOptionsWindow? _options;

	public override void _Ready()
	{
		Name = "InGameMenu";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		// Under the window, the whole screen: nothing behind the menu takes a click while it is up (ingamemenu.md, section 1).
		_blocker = new CGuiModalBlocker { Name = "MenuBlocker", Visible = false };
		AddChild(_blocker);
		AddChild(_window);
		_window.Visible = false;
		if (_options is not null)
		{
			// Over the menu's picture, which stays up behind it (gameoptions.md, section 1).
			AddChild(_options);
			_options.CloseRequested += Close;
		}
		_panel = _window.FindWindow("menu_panel");
		_save = _window.FindWindow("menu_save");
		_settings = _window.FindWindow("menu_message_settings");

		if (_panel is not null)
		{
			// What these two lead to is not built - saving, the screen after a game - so they are greyed where the
			// original greys neither (ingamemenu.md, Ours).
			foreach (string unbuilt in new[] { "menu_save_button", "menu_resign_button" })
			{
				if (_panel.FindButton(unbuilt) is { } button)
				{
					button.Disabled = true;
				}
			}
			Bind(_panel.FindButton("menu_options_button"), OpenOptions);
			Bind(_panel.FindButton("menu_message_button"), OpenMessageSettings);
			Bind(_panel.FindButton("menu_quit_button"), () => AskToEnd("QUIT"));
			Bind(_panel.FindButton("menu_close_button"), Close);
		}
		if (_settings is not null)
		{
			_list = _settings.FindListBox("settings");
			foreach ((string name, EMessageCategory? category) in CMessageSettingsText.Tabs)
			{
				if (_settings.TryFindButton(name) is not { } box)
				{
					continue;
				}
				_tabs.Add((name, category, box));
				box.Pressed += () => TabClicked(box);
				if (CMessageSettingsText.TabTooltip(_context.Localisation.Localise, name) is { } text)
				{
					box.Tooltip = () => new CTooltip { Text = text };
				}
			}
			Bind(_settings.FindButton("reset"), () => _context.MessageTypes.Reset(_directories, CGuiContext.MessageTypesPath));
			Bind(_settings.FindButton("close"), CloseMessageSettings);
		}
	}

	/// <summary>Escape with nothing selected, and the top bar's menu button: open where shut, and the close where open.</summary>
	public void Toggle()
	{
		if (IsOpen)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	/// <summary>
	/// The menu on its six buttons, the clock stopped where it ran - every game here being of one player - and the
	/// selection, the side menus and a country view let go (ingamemenu.md, section 1).
	/// </summary>
	public void Open()
	{
		if (_window is null)
		{
			return;
		}
		_pausedBefore = _simulation.Clock.Paused;
		if (!_pausedBefore)
		{
			_simulation.Clock.Paused = true;
		}
		_window.Visible = true;
		SetBlocking(true);
		HideWindows();
		if (_panel is not null)
		{
			_panel.Visible = true;
		}
		Opening?.Invoke();
	}

	/// <summary>
	/// Close, and what else closes the menu - a battle's window opening: from one of its windows back to the six buttons,
	/// which notes the clock as stopped, so that it stays stopped; otherwise the clock started again where the menu
	/// stopped it, and the menu shut (ingamemenu.md, section 2).
	/// </summary>
	public void Close()
	{
		if (_window is not { Visible: true })
		{
			return;
		}
		if (_save is { Visible: true } || _settings is { Visible: true } || _options is { IsShown: true })
		{
			_window.Visible = false;
			_options?.HideWindow();
			Open();
			return;
		}
		_options?.HideWindow();
		if (_simulation.Clock.Paused && !_pausedBefore)
		{
			_simulation.Clock.Paused = false;
		}
		_pausedBefore = true;
		_window.Visible = false;
		SetBlocking(false);
	}

	private void SetBlocking(bool blocking)
	{
		if (_blocker is not null)
		{
			_blocker.Visible = blocking;
		}
	}

	/// <summary>
	/// While the menu is up no key reaches what is behind it - no button's shortcut, Enter on a message popup among them,
	/// and nothing of the map's or the clock's. The menu's own buttons and its dialog have had the key first; an Escape
	/// none of them took is the original's own Escape, which closes the menu - from the message settings, whose Close
	/// has no shortcut, back to the six buttons (ingamemenu.md, sections 1 and 4).
	/// </summary>
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (!IsOpen || @event is not InputEventKey key)
		{
			return;
		}
		GetViewport().SetInputAsHandled();
		if (key is { Pressed: true, Echo: false, Keycode: Key.Escape })
		{
			Toggle();
		}
	}

	private void HideWindows()
	{
		foreach (CGuiWindow? window in new[] { _panel, _save, _settings })
		{
			if (window is not null)
			{
				window.Visible = false;
			}
		}
	}

	/// <summary>
	/// Resign and Quit: <c>DefaultDialog</c> with the key's title and text, both shields the player's, over the menu.
	/// OK on Quit ends the program, saving nothing (ingamemenu.md, section 3).
	/// </summary>
	private void AskToEnd(string key)
	{
		_dialog?.Close();
		Func<string, string> localise = _context.Localisation.Localise;
		_dialog = CConfirmDialog.Open(this, _context, DialogName, localise(key + "_TITLE"), localise(key + "_DESC"));
		if (_dialog is null)
		{
			return;
		}
		if (_player is not null)
		{
			_dialog.Window.TryFindIcon("LeftShield")?.SetCountry(_player);
			_dialog.Window.TryFindIcon("RightShield")?.SetCountry(_player);
		}
		if (key == "QUIT")
		{
			_dialog.Agreed += () => GetTree().Quit();
		}
	}

	/// <summary>
	/// Message Settings: the menu's other windows hidden and a row for every type, whatever tab is ticked - the original's
	/// fill ignores the tabs (ingamemenu.md, section 4).
	/// </summary>
	private void OpenMessageSettings()
	{
		if (_settings is null)
		{
			return;
		}
		HideWindows();
		List(null);
		_settings.Visible = true;
	}

	/// <summary>Game Options: the options window shown and the six buttons hidden, the menu's picture left up.</summary>
	private void OpenOptions()
	{
		if (_options is null)
		{
			return;
		}
		_options.Open();
		HideWindows();
	}

	/// <summary>The window's Close: the player's file written, and back to the six buttons.</summary>
	private void CloseMessageSettings()
	{
		_context.MessageTypes.WriteCustom(CGuiContext.MessageTypesPath);
		Toggle();
	}

	/// <summary>
	/// A tab clicked: ticked, the other six unticked and its types listed; unticked with none left ticked, every type.
	/// </summary>
	private void TabClicked(CGuiButton clicked)
	{
		if (clicked.Checked)
		{
			EMessageCategory? listed = null;
			foreach ((string _, EMessageCategory? category, CGuiButton box) in _tabs)
			{
				if (ReferenceEquals(box, clicked))
				{
					listed = category;
				}
				else
				{
					box.Checked = false;
				}
			}
			List(listed);
			return;
		}
		foreach ((string _, EMessageCategory? _, CGuiButton box) in _tabs)
		{
			if (box.Checked)
			{
				return;
			}
		}
		List(null);
	}

	private void List(EMessageCategory? category)
	{
		if (_list is null)
		{
			return;
		}
		_list.Clear();
		Func<string, string> localise = _context.Localisation.Localise;
		foreach (CMessageType type in CMessageSettingsText.Listed(_context.MessageTypes, category))
		{
			if (CGuiWindow.CreateEntry(_context, RowName) is not { } row)
			{
				return;
			}
			if (row.FindTextBox("text") is { } text)
			{
				text.Text = CMessageSettingsText.RowText(localise, type);
			}
			// The row's own button, under the type's name, is attached to nothing in the original: a click does nothing.
			var boxes = new List<CGuiButton>();
			foreach (string name in CMessageSettingsText.Boxes)
			{
				if (row.TryFindButton(name) is not { } box)
				{
					continue;
				}
				box.Checked = CMessageSettingsText.Ticked(type, name);
				box.Tooltip = () => new CTooltip { Text = CMessageSettingsText.BoxTooltip(localise, type, name) ?? "" };
				boxes.Add(box);
			}
			foreach (CGuiButton box in boxes)
			{
				box.Pressed += () => CMessageSettingsText.Apply(type, Ticked(row, type, CMessageSettingsText.PauseBox),
					Ticked(row, type, CMessageSettingsText.LogBox), Ticked(row, type, CMessageSettingsText.DialogBox),
					Ticked(row, type, CMessageSettingsText.MapBox));
			}
			_list.Add(row);
		}
	}

	/// <summary>A box of a row as it stands; one a file left out leaves its setting as the type has it.</summary>
	private static bool Ticked(CGuiWindow row, CMessageType type, string name)
	{
		return row.TryFindButton(name) is { } box ? box.Checked : CMessageSettingsText.Ticked(type, name);
	}

	private static void Bind(CGuiButton? button, Action action)
	{
		if (button is not null)
		{
			button.Pressed += action;
		}
	}
}
