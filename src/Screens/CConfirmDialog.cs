using System;
using Godot;
using OpenHOI3.Gui;

namespace OpenHOI3.Screens;

/// <summary>
/// The original's <c>DefaultConfirm</c>: a title, a text, and a button that agrees and
/// one that declines. Either takes it down; only the first does what was asked. What
/// raises the leader window's is in <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public sealed class CConfirmDialog
{
	private readonly CGuiWindow _window;

	/// <summary>Raised when the player agrees, just before the dialog goes.</summary>
	public event Action? Agreed;

	private CConfirmDialog(Node parent, CGuiWindow window, string title, string text)
	{
		_window = window;
		parent.AddChild(window);
		if (window.FindTextBox("Title") is { } titleBox)
		{
			titleBox.Text = title;
		}
		if (window.FindTextBox("Description") is { } textBox)
		{
			textBox.Text = text;
		}
		if (window.FindButton("AgreeButton") is { } agree)
		{
			agree.Pressed += () =>
			{
				Agreed?.Invoke();
				Close();
			};
		}
		if (window.FindButton("DeclineButton") is { } decline)
		{
			decline.Pressed += Close;
		}
	}

	public static CConfirmDialog? Open(Node parent, CGuiContext context, string windowName, string title, string text)
	{
		CGuiWindow? window = CGuiWindow.Create(context, windowName);
		return window is null ? null : new CConfirmDialog(parent, window, title, text);
	}

	public bool IsOpen => GodotObject.IsInstanceValid(_window) && !_window.IsQueuedForDeletion();

	/// <summary>The dialog's window, for what else its opener puts on it - the shields of a <c>DefaultDialog</c>.</summary>
	public CGuiWindow Window => _window;

	public void Close()
	{
		if (IsOpen)
		{
			_window.QueueFree();
		}
	}
}
