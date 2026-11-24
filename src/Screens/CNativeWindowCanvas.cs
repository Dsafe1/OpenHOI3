using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Windows;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Screens;

/// <summary>
/// A mod's native page's window as its Lua fills it: every call finds its element by name in the window, or in a row
/// of one of its lists as <c>list#3/name</c>, and does the one thing asked. A button's press and an edit box's typing
/// are remembered until they are asked for. See <c>docs/mechanics/nativepages.md</c>, section 4.
/// </summary>
public sealed class CNativeWindowCanvas : INativeWindowCanvas
{
	private readonly CGuiWindow _window;
	private readonly CGuiContext _context;
	private readonly string _page;

	/// <summary>The buttons whose presses are being remembered and the edit boxes whose typing is, by their names.</summary>
	private readonly Dictionary<string, CGuiButton> _watchedButtons = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CGuiTextBox> _watchedBoxes = new(StringComparer.Ordinal);
	private readonly HashSet<string> _pressed = new(StringComparer.Ordinal);
	private readonly HashSet<string> _edited = new(StringComparer.Ordinal);
	private readonly HashSet<string> _missing = new(StringComparer.Ordinal);

	/// <summary>The entry each list's rows were last made of.</summary>
	private readonly Dictionary<string, string> _rowsOf = new(StringComparer.Ordinal);

	public CNativeWindowCanvas(CGuiWindow window, CGuiContext context, string page)
	{
		_window = window;
		_context = context;
		_page = page;
	}

	/// <summary>Whether the page asked for its window to be shut, since this was last asked.</summary>
	public bool TakeClose()
	{
		bool asked = _closeAsked;
		_closeAsked = false;
		return asked;
	}

	private bool _closeAsked;

	public void Close() => _closeAsked = true;

	/// <summary>The element of a name, in the window or in a list's row; none, said once, where there is none.</summary>
	private CGuiObject? Find(string name)
	{
		CGuiObject? found = null;
		int mark = name.IndexOf('#');
		int slash = mark < 0 ? -1 : name.IndexOf('/', mark);
		if (mark > 0 && slash > mark && int.TryParse(name.AsSpan(mark + 1, slash - mark - 1), out int row))
		{
			if (_window.TryFindListBox(name[..mark]) is { } list && row >= 1 && row <= list.Entries.Count
				&& list.Entries[row - 1] is CGuiWindow entry)
			{
				found = entry.TryFindElement(name[(slash + 1)..]);
			}
		}
		else
		{
			found = _window.TryFindElement(name);
		}
		if (found is null && _missing.Add(name))
		{
			CDiagnostics.Problem($"the page {_page} asks its window '{_window.Name}' for '{name}', which it does not have");
		}
		return found;
	}

	/// <summary>An edit box takes typing from the first time the page touches it.</summary>
	private CGuiTextBox? Box(string name)
	{
		if (Find(name) is not CGuiTextBox box)
		{
			return null;
		}
		if (box.Editor is null)
		{
			box.MakeEditable();
			// A page's box gives the keyboard up to a press anywhere else, and the caret goes with it.
			box.GivesUpKeyboard = true;
		}
		if (box.Editor is not null && (!_watchedBoxes.TryGetValue(name, out CGuiTextBox? watched) || watched != box))
		{
			_watchedBoxes[name] = box;
			box.Typed += () => _edited.Add(name);
		}
		return box;
	}

	public void SetText(string element, string text)
	{
		switch (Find(element))
		{
			case CGuiTextBox when Box(element) is { } box:
				// What is being typed is the player's until he leaves the box.
				if (!box.HasKeyboard)
				{
					box.Text = text;
				}
				break;
			case CGuiButton button:
				button.SetText(text);
				break;
		}
	}

	public string Text(string element)
	{
		return Box(element) is { } box ? box.Editor?.Text ?? box.Text : "";
	}

	public bool Edited(string element)
	{
		Box(element);
		return _edited.Remove(element);
	}

	public bool Pressed(string element)
	{
		if (Find(element) is not CGuiButton button)
		{
			return false;
		}
		if (!_watchedButtons.TryGetValue(element, out CGuiButton? watched) || watched != button)
		{
			_watchedButtons[element] = button;
			button.Pressed += () => _pressed.Add(element);
		}
		return _pressed.Remove(element);
	}

	public bool Checked(string element) => Find(element) is CGuiButton { Checked: true };

	public void SetChecked(string element, bool ticked)
	{
		if (Find(element) is CGuiButton button && button.Checked != ticked)
		{
			button.Checked = ticked;
		}
	}

	public void SetVisible(string element, bool visible)
	{
		if (Find(element) is { } found && found.Visible != visible)
		{
			found.Visible = visible;
		}
	}

	public void SetEnabled(string element, bool enabled)
	{
		if (Find(element) is CGuiButton button && button.Disabled == enabled)
		{
			button.Disabled = !enabled;
		}
	}

	public void SetTooltip(string element, string text)
	{
		if (Find(element) is { } found)
		{
			found.Tooltip = text.Length == 0 ? null : () => new CTooltip { Text = text };
		}
	}

	public void SetFrame(string element, int frame)
	{
		switch (Find(element))
		{
			case CGuiIcon icon:
				icon.SetFrame(frame);
				break;
			case CGuiButton button:
				button.SetFrame(frame);
				break;
		}
	}

	public void SetColour(string element, float red, float green, float blue, float alpha)
	{
		if (Find(element) is { } found)
		{
			found.Modulate = new Color(red / 255f, green / 255f, blue / 255f, alpha / 255f);
		}
	}

	public void SetRows(string list, string entry, int count)
	{
		if (_window.TryFindListBox(list) is not { } box)
		{
			if (_missing.Add(list))
			{
				CDiagnostics.Problem($"the page {_page} asks its window '{_window.Name}' for the list '{list}', which it does not have");
			}
			return;
		}
		count = Math.Clamp(count, 0, MostRows);
		if (box.Entries.Count == count && _rowsOf.GetValueOrDefault(list) == entry)
		{
			return;
		}
		// Made afresh: what was watched in the old rows is gone with them.
		box.Clear();
		string prefix = list + "#";
		foreach (string name in new List<string>(_watchedButtons.Keys))
		{
			if (name.StartsWith(prefix, StringComparison.Ordinal))
			{
				_watchedButtons.Remove(name);
			}
		}
		foreach (string name in new List<string>(_watchedBoxes.Keys))
		{
			if (name.StartsWith(prefix, StringComparison.Ordinal))
			{
				_watchedBoxes.Remove(name);
			}
		}
		for (int row = 0; row < count; row++)
		{
			if (CGuiWindow.CreateEntry(_context, entry) is not { } made)
			{
				break;
			}
			box.Add(made);
		}
		_rowsOf[list] = entry;
	}

	/// <summary>The most rows a page may give a list.</summary>
	public const int MostRows = 2000;
}
