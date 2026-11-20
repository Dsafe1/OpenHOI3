using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Chat;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The chat in play of a game between machines, from <c>chat.gui</c>: the inbox button <c>chat_inbox_window</c> while
/// it is closed, and <c>chat_window</c> with its channels, its lines, its users and its box - opened by the button or
/// Tab, closed by its close button, Tab or Escape. Made only in a game between machines, as the original's is. It
/// reads nothing of the game: who plays what and the player's channel are handed it. See
/// <c>docs/mechanics/chat.md</c>; <c>reversing/findings/FINDINGS-ingamechat.md</c>.
/// </summary>
public sealed partial class CChatView : Control
{
	private const string WindowName = "chat_window";
	private const string InboxName = "chat_inbox_window";
	private const string LineName = "chat_item";
	private const string ChannelName = "channel_window";
	private const string UserName = "user_item";

	private CGuiContext _context = null!;
	private CChatLog _log = null!;
	private CGuiWindow? _window;
	private CGuiWindow? _inbox;
	private CGuiButton? _open;
	private CGuiTextBox? _input;
	private int _shown = -1;
	private int _usersShown = -1;
	private ulong _closedFrame;
	private IReadOnlyList<(string Name, CCountryTag? Country)> _users = Array.Empty<(string, CCountryTag?)>();
	private int _usersVersion;

	/// <summary>A line typed for the current channel: the channel and the text.</summary>
	public event Action<int, string>? Said;

	/// <summary>A line typed for one player: his name and the text.</summary>
	public event Action<string, string>? Whispered;

	/// <summary>A country by its tag, for a user's flag.</summary>
	public Func<CCountryTag, CCountry?>? CountryOf { get; set; }

	/// <summary>This machine's player's name, who is not whispered to by his own row.</summary>
	public string OwnName { get; set; } = "";

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CChatLog log)
	{
		_context = context;
		_log = log;
	}

	private Func<string, string> Localise => _context.Localisation.Localise;

	public bool IsOpen => _window is { Visible: true };

	public override void _Ready()
	{
		Name = "Chat";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		_inbox = CGuiWindow.Create(_context, InboxName);
		if (_window is null || _inbox is null)
		{
			return;
		}
		AddChild(_inbox);
		AddChild(_window);
		_open = _inbox.TryFindButton("open");
		if (_open is not null)
		{
			_open.Pressed += Open;
		}
		if (_window.TryFindButton("close") is { } close)
		{
			// Its shortcut, TAB in the original's file, is this view's to take: Tab also opens, and with Ctrl changes channel.
			close.ClearShortcut();
			close.Pressed += Close;
		}
		_input = _window.FindTextBox("chat_input");
		if (_input is not null)
		{
			_input.MakeEditable();
			if (_input.Editor is { } editor)
			{
				editor.Text = "";
			}
			_input.Entered += Enter;
		}
		// As made it is closed, or as it was left before the game was loaded again.
		_window.Visible = _log.IsOpen;
		_inbox.Visible = !_log.IsOpen;
		if (_log.IsOpen)
		{
			_input?.TakeKeyboard();
		}
	}

	/// <summary>The players of the game, the host first, where the list has changed.</summary>
	public void SetUsers(IReadOnlyList<(string Name, CCountryTag? Country)> users, int version)
	{
		if (version != _usersVersion)
		{
			_usersVersion = version;
			_users = users;
			_usersShown = -1;
		}
	}

	public void Open()
	{
		if (_window is null || _inbox is null || IsOpen)
		{
			return;
		}
		_window.Visible = true;
		_inbox.Visible = false;
		_log.Open();
		_input?.TakeKeyboard();
	}

	public void Close()
	{
		if (_window is null || _inbox is null || !IsOpen)
		{
			return;
		}
		_window.Visible = false;
		_inbox.Visible = true;
		_log.Close();
		_closedFrame = Engine.GetProcessFrames();
		if (_input is not null && _context.KeyboardFocus == _input)
		{
			_context.KeyboardFocus = null;
		}
	}

	/// <summary>
	/// Tab opens the chat where it is closed and no box is being typed in; open, Tab closes it, Ctrl and Tab goes to
	/// the next channel the player is in and with Shift to the one before, Escape closes it, and Up and Down bring
	/// back the lines sent.
	/// </summary>
	public override void _Input(InputEvent input)
	{
		if (_window is null || input is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}
		if (!IsOpen)
		{
			if (key.Keycode == Key.Tab && !key.CtrlPressed && !key.AltPressed && _context.KeyboardFocus is not { HasKeyboard: true }
				&& _closedFrame != Engine.GetProcessFrames())
			{
				Open();
				GetViewport().SetInputAsHandled();
			}
			return;
		}
		switch (key.Keycode)
		{
			case Key.Tab when key.CtrlPressed:
				_log.Next(back: key.ShiftPressed);
				break;
			case Key.Tab:
			case Key.Escape:
				Close();
				break;
			case Key.Up or Key.Down when _input?.Editor is { } editor && _log.Recall(key.Keycode == Key.Up) is { } recalled:
				editor.Text = recalled;
				_input.QueueRedraw();
				break;
			default:
				return;
		}
		GetViewport().SetInputAsHandled();
	}

	/// <summary>Enter in the box: the line read, sent or answered with its error, the box emptied and the window left open.</summary>
	private void Enter()
	{
		if (_input?.Editor is not { } editor)
		{
			return;
		}
		(EChatTyped kind, string target, string text) = _log.Read(editor.Text, _users.Select(user => user.Name));
		editor.Text = "";
		_input.QueueRedraw();
		switch (kind)
		{
			case EChatTyped.Line:
				Said?.Invoke(_log.Current, text);
				break;
			case EChatTyped.Whisper:
				Whispered?.Invoke(target, text);
				break;
			case EChatTyped.Error:
				// Said in the current channel on this machine alone.
				_log.Add(new CChatLine(EChatLineKind.System, _log.Current, target, text, DateTime.Now.ToString("HH:mm")));
				break;
		}
	}

	public override void _Process(double delta)
	{
		if (_window is null || _open is null)
		{
			return;
		}
		// The inbox button: its second frame while a channel the player is in holds a line not read.
		_open.SetFrame(_log.AnyUnread ? 2 : 1);
		if (!IsOpen)
		{
			return;
		}
		if (_log.Version != _shown)
		{
			_shown = _log.Version;
			ShowChannels();
			ShowLines();
		}
		if (_usersShown != _usersVersion)
		{
			_usersShown = _usersVersion;
			ShowUsers();
		}
	}

	/// <summary>A tab for each channel the player is in, the current one's name in yellow and one with unread lines in green.</summary>
	private void ShowChannels()
	{
		if (_window?.FindBox("channels") is not { } box)
		{
			return;
		}
		box.Clear();
		foreach (int channel in _log.Joined)
		{
			if (CGuiWindow.CreateEntry(_context, ChannelName) is not { } tab)
			{
				return;
			}
			if (tab.FindTextBox("name") is { } name)
			{
				string colour = channel == _log.Current ? "§Y" : _log.Unread(channel) ? "§G" : "";
				name.Text = colour + CChatText.Channel(Localise, channel);
			}
			if (tab.TryFindButton("select") is { } select)
			{
				int chosen = channel;
				select.Pressed += () => _log.Select(chosen);
			}
			// A channel of the game's own is not left: the original's close is for the channels players make, which are not built.
			if (tab.TryFindButton("close") is { } close)
			{
				close.Visible = false;
			}
			box.Add(tab);
		}
	}

	/// <summary>The current channel's lines, each wrapped to the row's text box, the list at its end.</summary>
	private void ShowLines()
	{
		if (_window?.FindListBox("chat") is not { } list || _context.Types.FindWindow(LineName) is not { } rowType)
		{
			return;
		}
		list.Clear();
		CGuiTextBoxType? boxType = rowType.Children.OfType<CGuiTextBoxType>().FirstOrDefault(child => child.Name == "text");
		Gfx.CGuiFont? font = boxType is null ? null : _context.Fonts.Find(boxType.Font);
		foreach (CChatLine line in _log.Lines(_log.Current))
		{
			string text = CChatText.Row(Localise, line);
			IEnumerable<string> pieces = font is null || boxType is null ? new[] { text }
				: Interface.Text.CTextLayoutLineBreak.Break(text, boxType.MaxWidth,
					run => Interface.Text.CTextIcons.Measure(run, font.Width));
			foreach (string piece in pieces)
			{
				if (CGuiWindow.CreateEntry(_context, LineName) is not { } row)
				{
					return;
				}
				if (row.FindTextBox("text") is { } box)
				{
					box.Text = piece;
				}
				list.Add(row);
			}
		}
		list.ShowLast();
	}

	/// <summary>A row for each player: the name, the country's flag, and the whisper button, which begins a line to him.</summary>
	private void ShowUsers()
	{
		if (_window?.FindListBox("users") is not { } list)
		{
			return;
		}
		list.Clear();
		foreach ((string name, CCountryTag? tag) in _users)
		{
			if (CGuiWindow.CreateEntry(_context, UserName) is not { } row)
			{
				return;
			}
			if (row.FindTextBox("name") is { } box)
			{
				box.Text = name;
			}
			if (row.TryFindButton("country_icon") is { } flag)
			{
				CCountry? country = tag is { } held ? CountryOf?.Invoke(held) : null;
				flag.Visible = country is not null;
				if (country is not null)
				{
					flag.SetCountry(country);
				}
			}
			if (row.TryFindButton("whisper") is { } whisper)
			{
				string to = name;
				whisper.Pressed += () =>
				{
					if (to != OwnName && _input?.Editor is { } editor)
					{
						editor.Text = "/whisper " + to + " ";
						_input.TakeKeyboard();
						_input.QueueRedraw();
					}
				};
			}
			// Kicking a player from a channel is the made channels', which are not built.
			if (row.TryFindButton("kick") is { } kick)
			{
				kick.Visible = false;
			}
			list.Add(row);
		}
	}
}
