using System;
using Godot;
using OpenHOI3.Gui;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The front end's multiplayer menu (<c>docs/mechanics/frontend.md</c>, section 8;
/// <c>reversing/findings/FINDINGS-multiplayermenu.md</c>): <c>temp_frontend.gui</c>'s <c>multiplayer_menu</c> and
/// <c>multiplayer_connect_menu</c>, which are panels of the main menu's window, and <c>naming_window</c>, a window of
/// its own that asks for the game's name on Host and for the address on Join Internet Game - one window, its title,
/// its button's text and what the button does swapped each time it is opened.
/// </summary>
public sealed class CMultiplayerMenu
{
	private readonly CGuiContext _context;
	private readonly CGuiWindow? _menu;
	private readonly CGuiWindow? _connect;
	private readonly CGuiWindow? _naming;
	private readonly CGuiButton? _scan;
	private bool _scanning;

	/// <summary>Whether the naming window was opened for an address, and not for a game's name.</summary>
	private bool _namingAnAddress;

	/// <param name="backdrop">The main menu's window, <c>country_selection_panel</c>, which holds the two panels.</param>
	/// <param name="parent">What the naming window is put under.</param>
	public CMultiplayerMenu(CGuiContext context, CGuiWindow backdrop, Node parent)
	{
		_context = context;
		_menu = backdrop.FindWindow("multiplayer_menu");
		_connect = backdrop.FindWindow("multiplayer_connect_menu");
		// Made by the view itself, and hidden at once; no child of the menu, which hiding the menu leaves alone.
		_naming = CGuiWindow.Create(context, "naming_window");
		if (_naming is not null)
		{
			parent.AddChild(_naming);
			_naming.Visible = false;
		}
		if (_menu is null)
		{
			return;
		}
		Bind(_menu, "host", OnHost);
		Bind(_menu, "join", () => { });
		Bind(_menu, "join_direct", OnJoinDirect);
		Bind(_menu, "scan", OnScan);
		// The file's button is Back and the original asks for back.
		if ((_menu.TryFindButton("back") ?? _menu.TryFindButton("Back")) is { } back)
		{
			back.Pressed += OnBack;
		}
		if (_connect is not null)
		{
			Bind(_connect, "back", OnConnectBack);
		}
		if (_naming is not null)
		{
			Bind(_naming, "ok_host_button", OnNamingOk);
			Bind(_naming, "back_button", OnNamingBack);
			_naming.FindTextBox("edit")?.MakeEditable();
		}
		_menu.FindTextBox("playername")?.MakeEditable();
		// The file's "scan" is never seen: the text is written over as the menu is made.
		_scan = _menu.TryFindButton("scan");
		_scan?.SetText(Localise("SCAN"));
		// Join Lan Game is greyed while no found game is selected, and nothing is ever found: there is no scan of the
		// network (docs/compatibility.md). The metaserver is not offered. Ours.
		if (_menu.TryFindButton("join") is { } join)
		{
			join.Disabled = true;
		}
		if (_menu.TryFindButton("metaserver") is { } metaserver)
		{
			metaserver.Disabled = true;
		}
		ShowNoneFound();
	}

	/// <summary>Whether the install's files declare the menu at all.</summary>
	public bool Exists => _menu is not null;

	/// <summary>Host's name given: the player's name and the game's, as typed.</summary>
	public event Action<string, string>? HostRequested;

	/// <summary>An address given to join: the player's name, and the address with the white space off its ends.</summary>
	public event Action<string, string>? JoinRequested;

	/// <summary>The connect log's back: a connection under way is dropped.</summary>
	public event Action? ConnectCancelled;

	/// <summary>The menu's Back: the main menu again.</summary>
	public event Action? BackPressed;

	/// <summary>The menu shown and the connect log hidden; the name box filled from the settings' last player where that is not empty.</summary>
	public void Show(string lastPlayer)
	{
		if (_menu is null)
		{
			return;
		}
		_menu.Visible = true;
		if (_connect is not null)
		{
			_connect.Visible = false;
		}
		if (lastPlayer.Length > 0 && _menu.FindTextBox("playername")?.Editor is { } name)
		{
			name.Text = lastPlayer;
		}
	}

	/// <summary>The menu and the connect log hidden; the naming window is left as it is.</summary>
	public void Hide()
	{
		if (_menu is not null)
		{
			_menu.Visible = false;
		}
		if (_connect is not null)
		{
			_connect.Visible = false;
		}
	}

	/// <summary>The last host's address, which Join Internet Game puts in the box: set by whoever holds the settings.</summary>
	public Func<string> LastHost { get; set; } = () => "";

	/// <summary>The name in the box as it stands: never trimmed and never tested.</summary>
	public string PlayerName => _menu?.FindTextBox("playername")?.Editor?.Text ?? "";

	/// <summary>The connect log shown in the menu's place, as a connection is begun.</summary>
	public void ShowConnecting()
	{
		if (_menu is not null)
		{
			_menu.Visible = false;
		}
		if (_connect is not null)
		{
			_connect.Visible = true;
		}
	}

	/// <summary>A line added at the end of the connect log, by its key. Nothing ever clears the log.</summary>
	public void Log(string key)
	{
		if (_connect?.FindListBox("info_list") is not { } list || CGuiWindow.CreateEntry(_context, "connection_log_text") is not { } row)
		{
			return;
		}
		if (row.FindTextBox("text") is { } text)
		{
			text.Text = Localise(key);
		}
		list.Add(row);
		list.ShowLast();
	}

	private void OnHost()
	{
		OpenNaming("HOST_TITLE", "HOST_BUTTON", address: false);
	}

	private void OnJoinDirect()
	{
		// The box is set to the last host's address, empty or not; Host leaves in it whatever it last held.
		if (_naming?.FindTextBox("edit")?.Editor is { } edit)
		{
			edit.Text = LastHost();
		}
		OpenNaming("JOIN_IP_TITLE", "JOIN_BUTTON", address: true);
	}

	private void OpenNaming(string title, string button, bool address)
	{
		if (_naming is null || _menu is null)
		{
			return;
		}
		if (_naming.FindTextBox("Title") is { } box)
		{
			box.Text = Localise(title);
		}
		_namingAnAddress = address;
		_naming.TryFindButton("ok_host_button")?.SetText(Localise(button));
		_naming.FindTextBox("edit")?.TakeKeyboard();
		_naming.Visible = true;
		_menu.Visible = false;
	}

	private void OnNamingOk()
	{
		if (_naming is null)
		{
			return;
		}
		string typed = _naming.FindTextBox("edit")?.Editor?.Text ?? "";
		_naming.Visible = false;
		if (_namingAnAddress)
		{
			// The white space off both ends and nothing else tested: an empty address goes on to a failed connect.
			JoinRequested?.Invoke(PlayerName, typed.Trim());
		}
		else
		{
			// Exactly as typed, an empty name allowed.
			HostRequested?.Invoke(PlayerName, typed);
		}
	}

	private void OnNamingBack()
	{
		if (_naming is not null)
		{
			_naming.Visible = false;
		}
		if (_menu is not null)
		{
			_menu.Visible = true;
		}
	}

	private void OnConnectBack()
	{
		if (_menu is not null)
		{
			_menu.Visible = true;
		}
		if (_connect is not null)
		{
			_connect.Visible = false;
		}
		ConnectCancelled?.Invoke();
	}

	private void OnScan()
	{
		// A toggle. Starting empties the list; with nothing found the None row is back at once.
		_scanning = !_scanning;
		_scan?.SetText(Localise(_scanning ? "STOP_SCAN" : "SCAN"));
		if (_scanning)
		{
			_menu?.FindListBox("games")?.Clear();
			ShowNoneFound();
		}
	}

	private void OnBack()
	{
		if (_scanning)
		{
			OnScan();
		}
		Hide();
		BackPressed?.Invoke();
	}

	/// <summary>With no game known and no row in the list, one row saying None.</summary>
	private void ShowNoneFound()
	{
		if (_menu?.FindListBox("games") is not { Count: 0 } games || CGuiWindow.CreateEntry(_context, "local_game_entry") is not { } row)
		{
			return;
		}
		row.TryFindButton("game")?.SetText(Localise("FE_NONE_FOUND"));
		games.Add(row);
	}

	private string Localise(string key) => _context.Localisation.Localise(key);

	private static void Bind(CGuiWindow window, string name, Action pressed)
	{
		if (window.TryFindButton(name) is { } button)
		{
			button.Pressed += pressed;
		}
	}
}
