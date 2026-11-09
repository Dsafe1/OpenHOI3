using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Console;

namespace OpenHOI3.Screens;

/// <summary>
/// The console: <c>console_wnd</c>, a list of what has been answered above a line to type on, opened by <c>§</c> and
/// <c>`</c>. The up and down arrows go through what has been typed. See <c>docs/mechanics/console.md</c>.
/// </summary>
public sealed partial class CConsoleView : Control
{
	private const string WindowName = "console_wnd";
	private const string EntryName = "console_entry_wnd";

	private readonly List<string> _history = new();
	private CGuiContext _context = null!;
	private CConsoleRunner _runner = null!;
	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private CGuiTextBox? _edit;
	private int _cursor;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CConsoleRunner runner)
	{
		_context = context;
		_runner = runner;
	}

	public override void _Ready()
	{
		Name = "Console";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_list = _window.FindListBox("console_list");
		_edit = _window.FindTextBox("console_edit");
		if (_edit is not null)
		{
			_edit.MakeEditable();
			if (_edit.Editor is { } editor)
			{
				editor.AlsoRefused = "§`";
				editor.Text = "";
			}
			_edit.Entered += Submit;
		}
		Visible = false;
	}

	public bool IsOpen => Visible;

	/// <summary>Opens it where it is shut, with the keyboard, and shuts it where it is open.</summary>
	public void Toggle()
	{
		Visible = !Visible;
		if (Visible)
		{
			_edit?.TakeKeyboard();
			_cursor = _history.Count;
		}
	}

	public static bool IsConsoleKey(InputEventKey key)
	{
		return key.Unicode == 0xA7 || key.Unicode == '`' || key.Keycode == Key.Quoteleft;
	}

	public override void _Input(InputEvent input)
	{
		if (!Visible || input is not InputEventKey { Pressed: true } key)
		{
			return;
		}
		if (key.Keycode == Key.Escape)
		{
			Toggle();
			GetViewport().SetInputAsHandled();
		}
		else if (key.Keycode is Key.Up or Key.Down && _edit?.Editor is { } editor)
		{
			_cursor = Mathf.Clamp(_cursor + (key.Keycode == Key.Up ? -1 : 1), 0, _history.Count);
			editor.Text = _cursor < _history.Count ? _history[_cursor] : "";
			_edit.QueueRedraw();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Submit()
	{
		if (_edit?.Editor is not { } editor)
		{
			return;
		}
		string line = editor.Text.Trim();
		editor.Text = "";
		_edit.QueueRedraw();
		if (line.Length == 0)
		{
			return;
		}
		_history.Add(line);
		_cursor = _history.Count;
		CConsoleReply reply = _runner.Run(line);
		if (reply.Text.Length == 0)
		{
			return;
		}
		foreach (string answer in reply.Text.Split('\n'))
		{
			Add((reply.Ok ? "§W" : "§R") + answer);
		}
		_list?.ShowLast();
	}

	private void Add(string text)
	{
		if (_list is null || CGuiWindow.CreateEntry(_context, EntryName) is not { } row)
		{
			return;
		}
		if (row.FindTextBox("console_text") is { } box)
		{
			box.Text = text;
		}
		_list.Add(row);
	}
}
