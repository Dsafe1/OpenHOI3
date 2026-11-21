using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Messages;
using OpenHOI3.Content.Saves;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The in-game menu: <c>menubar.gui</c>'s window <c>menu</c>, its six buttons on <c>menu_panel</c>, the save dialog
/// <c>menu_save</c>, the dialog Quit opens and the message settings, <c>menu_message_settings</c>. Opening it stops the
/// clock where it runs and always shows the six buttons; Close from one of its windows goes back to them, and leaves the
/// clock stopped - the original's. Game Options opens <see cref="CGameOptionsWindow"/>; Resign is greyed: what it leads
/// to is not built. See <c>docs/mechanics/ingamemenu.md</c>.
/// </summary>
public sealed partial class CInGameMenu : Control
{
	public const string WindowName = "menu";
	public const string RowName = "message_entry";
	public const string SaveRowName = "save_game_entry";
	private const string DialogName = "DefaultDialog";
	private const string ConfirmName = "DefaultConfirm";

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
	private CGuiListBox? _games;
	private CGuiTextBox? _name;
	private readonly List<(string Name, EMessageCategory? Category, CGuiButton Box)> _tabs = new();
	private CConfirmDialog? _dialog;

	/// <summary>Whether the clock was stopped when the menu last opened; the original's starts out set.</summary>
	private bool _pausedBefore = true;

	/// <summary>Raised as the menu opens: the side menus and a country view are to shut and the selection go.</summary>
	public event Action? Opening;

	public bool IsOpen => _window is { Visible: true };

	/// <summary>Resign agreed to: the game is to end, nothing saved, and the front end come back.</summary>
	public event Action? Resigned;

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
			// Resign asks as Quit does, and agreed goes back to the front end's main menu - through no end-of-game screen,
			// which is not built (ingamemenu.md, Ours; frontend.md, section 1).
			Bind(_panel.FindButton("menu_resign_button"), () => AskToEnd("RESIGN"));
			Bind(_panel.FindButton("menu_save_button"), OpenSaveDialog);
			Bind(_panel.FindButton("menu_options_button"), OpenOptions);
			Bind(_panel.FindButton("menu_message_button"), OpenMessageSettings);
			Bind(_panel.FindButton("menu_quit_button"), () => AskToEnd("QUIT"));
			Bind(_panel.FindButton("menu_close_button"), Close);
		}
		if (_save is not null)
		{
			_games = _save.FindListBox("games");
			_name = _save.FindTextBox("game_name");
			_name?.MakeEditable();
			Bind(_save.FindButton("save"), SaveTyped);
			// Back to the six buttons, not to the map (ingamemenu.md, section 6).
			Bind(_save.FindButton("cancel"), Toggle);
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
			Bind(_settings.FindButton("reset"), () => _context.MessageTypes.Reset(_directories, _context.MessageTypesPath));
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
		// Not in a game between several players, where nothing pauses by itself (docs/multiplayer.md, section 2).
		_pausedBefore = !_simulation.Pace.PauseByItself();
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
		_simulation.Pace.ResumeAfter(!_pausedBefore);
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
		else
		{
			_dialog.Agreed += () => Resigned?.Invoke();
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

	/// <summary>
	/// Save Game: the menu's other windows hidden, a row for every save in the folder, newest first, and the name offered
	/// made again from nothing - the player's country and the hour (ingamemenu.md, section 6).
	/// </summary>
	private void OpenSaveDialog()
	{
		if (_save is null)
		{
			return;
		}
		HideWindows();
		if (_games is not null)
		{
			_games.Clear();
			var boxes = new List<CGuiButton>();
			foreach (string file in CSaveFiles.List(SaveFolders()))
			{
				if (CGuiWindow.CreateEntry(_context, SaveRowName) is not { } row || row.TryFindButton("game") is not { } box)
				{
					break;
				}
				string text = CSaveFiles.RowText(file);
				box.SetText(text);
				boxes.Add(box);
				// A click puts the row's name in the box and unticks the other rows; nothing is saved or loaded by it.
				box.Pressed += () =>
				{
					if (_name is not null)
					{
						_name.Text = text;
					}
					foreach (CGuiButton other in boxes)
					{
						if (!ReferenceEquals(other, box))
						{
							other.Checked = false;
						}
					}
				};
				_games.Add(row);
			}
		}
		_save.Visible = true;
		if (_name is not null)
		{
			string offered = CSaveFiles.OfferedName(_player?.Name ?? "", _simulation.State.TotalHours);
			_name.Text = offered;
			if (_name.Editor is { } editor)
			{
				// Set again even where the box holds it already, the caret to its end.
				editor.Text = offered;
			}
			// The original's request for the keyboard, granted once the box is shown.
			_name.TakeKeyboard();
		}
	}

	/// <summary>The player's folder of saves, where a save is written, then the install's, where one is also looked for.</summary>
	private IEnumerable<string> SaveFolders()
	{
		return _directories.UserAndLayerFolders(CSaveFiles.FolderFor(_directories));
	}

	private string SaveFolder => System.IO.Path.Combine(_directories.UserFolder, CSaveFiles.FolderFor(_directories));

	/// <summary>
	/// <c>save</c>: nothing for an empty box; a file of the name in any of the folders is asked about first, and written
	/// over only on OK; otherwise saved at once. No message says it was saved; then back to the six buttons.
	/// </summary>
	private void SaveTyped()
	{
		if (_name is null || CSaveFiles.FileName(_name.Text) is not { } file)
		{
			return;
		}
		string path = System.IO.Path.Combine(SaveFolder, file);
		if (!SaveFolders().Any(folder => System.IO.File.Exists(System.IO.Path.Combine(folder, file))))
		{
			WriteSave(path);
			return;
		}
		_dialog?.Close();
		Func<string, string> localise = _context.Localisation.Localise;
		_dialog = CConfirmDialog.Open(this, _context, ConfirmName, localise("CONFIRMSAVETITLE"),
			localise("CONFIRMSAVETEXT").Replace("$NAME$", file, StringComparison.Ordinal));
		if (_dialog is not null)
		{
			_dialog.Agreed += () => WriteSave(path);
		}
	}

	/// <summary>The game written, or the failure logged as the original logs it; either way back to the six buttons.</summary>
	private void WriteSave(string path)
	{
		try
		{
			CSaveFiles.Write(_simulation.State, _context.Localisation.Localise, path);
		}
		catch (Exception failed) when (failed is System.IO.IOException or UnauthorizedAccessException or ArgumentException
			or NotSupportedException)
		{
			CDiagnostics.Warn($"Failed to write save file {path}: {failed.Message}");
		}
		Toggle();
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
		_context.MessageTypes.WriteCustom(_context.MessageTypesPath);
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
