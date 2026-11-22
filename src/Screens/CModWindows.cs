using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Windows;
using OpenHOI3.Gui;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The windows a mod defines for an enhanced game (<c>docs/mechanics/modwindows.md</c>): a menu of the pages, which the
/// Insert key shows and hides, and the page chosen from it. Everything drawn is the mod's - the menu and each page a
/// window of its <c>interface/openhoi</c>, found by name - and everything said is its Lua's: this fills the elements a
/// page's <c>Collect</c> names and hands a pressed button to its <c>OnClick</c>. It reads the game, so it stands under
/// the HUD, which is not processed while hours run beside the frame.
/// </summary>
public sealed partial class CModWindows : Control
{
	/// <summary>The menu's window, its list of pages, the button of a row and the button that closes a window.</summary>
	public const string MenuWindow = "openhoi_windows_menu";
	public const string PagesList = "pages_list";
	public const string PageButton = "page_button";
	public const string CloseButton = "close_button";

	/// <summary>How often a page shown is asked again, in seconds of real time.</summary>
	private const double RefreshEvery = 2.0;

	private CGuiContext _context = null!;
	private Func<IModWindows?> _windows = null!;
	private CSimulation _simulation = null!;
	private CCountryTag _player;

	private CGuiWindow? _menu;
	private bool _menuFilled;
	private CModWindowPage? _page;
	private CGuiWindow? _pageWindow;
	private ulong _collectedAt;

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="windows">The mod's windows, made the first time they are asked for; null where they cannot be.</param>
	public void Setup(CGuiContext context, Func<IModWindows?> windows, CSimulation simulation, CCountryTag player)
	{
		_context = context;
		_windows = windows;
		_simulation = simulation;
		_player = player;
	}

	public override void _Ready()
	{
		Name = "ModWindows";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public bool IsOpen => _menu is { Visible: true } || _pageWindow is not null;

	/// <summary>The menu shown or hidden; hiding it shuts the page with it.</summary>
	public void Toggle()
	{
		if (_menu is { Visible: true })
		{
			_menu.Visible = false;
			ClosePage();
			return;
		}
		if (_menu is null)
		{
			// With no file declaring the menu there is nothing to show, which is reported once, as any missing window is.
			_menu = CGuiWindow.Create(_context, MenuWindow);
			if (_menu is null)
			{
				return;
			}
			AddChild(_menu);
			Bind(_menu.TryFindButton(CloseButton), Toggle);
		}
		_menu.Visible = true;
		FillMenu();
	}

	/// <summary>The menu's rows, one a page, made once: the pages do not change while a game runs.</summary>
	private void FillMenu()
	{
		if (_menuFilled || _menu?.TryFindListBox(PagesList) is not { } list || _windows() is not { } windows)
		{
			return;
		}
		_menuFilled = true;
		list.Clear();
		foreach (CModWindowPage page in windows.Pages)
		{
			if (CGuiWindow.CreateEntry(_context, CModWindowData.EntryType(PagesList)) is not { } entry)
			{
				return;
			}
			CModWindowPage opened = page;
			if (entry.TryFindButton(PageButton) is { } button)
			{
				button.SetText(_context.Localisation.Localise(page.Title.Length > 0 ? page.Title : page.Name));
				button.Pressed += () => Open(opened);
			}
			list.Add(entry);
		}
	}

	/// <summary>A page's window made and filled, in the place of the one shown.</summary>
	private void Open(CModWindowPage page)
	{
		ClosePage();
		_pageWindow = CGuiWindow.Create(_context, page.Window);
		if (_pageWindow is null)
		{
			return;
		}
		_page = page;
		AddChild(_pageWindow);
		BindButtons(_pageWindow, -1);
		Refresh();
	}

	private void ClosePage()
	{
		_pageWindow?.QueueFree();
		_pageWindow = null;
		_page = null;
	}

	/// <summary>Every button of a window handed to the page's Lua by its name, but the one that closes the page.</summary>
	private void BindButtons(CGuiWindow window, int row)
	{
		foreach ((string name, CGuiButton button) in window.Buttons)
		{
			if (row < 0 && name.Equals(CloseButton, StringComparison.OrdinalIgnoreCase))
			{
				button.Pressed += ClosePage;
				continue;
			}
			string pressed = name;
			button.Pressed += () => Click(pressed, row);
		}
	}

	private void Click(string element, int row)
	{
		if (_page is not { } page || _windows() is not { } windows)
		{
			return;
		}
		windows.Click(_simulation.State, page, _player, element, row);
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (_pageWindow is not null && (Time.GetTicksMsec() - _collectedAt) >= RefreshEvery * 1000.0)
		{
			Refresh();
		}
	}

	/// <summary>The page asked what it shows now, and its window's elements set from the answer.</summary>
	private void Refresh()
	{
		_collectedAt = Time.GetTicksMsec();
		if (_page is not { } page || _pageWindow is not { } window || _windows() is not { } windows
			|| windows.Collect(_simulation.State, page, _player) is not { } data)
		{
			return;
		}
		Apply(window, data.Window);
		foreach ((string name, List<CModWindowRow> rows) in data.Lists)
		{
			if (window.TryFindListBox(name) is not { } list)
			{
				continue;
			}
			list.Clear();
			for (int i = 0; i < rows.Count; i++)
			{
				if (CGuiWindow.CreateEntry(_context, CModWindowData.EntryType(name)) is not { } entry)
				{
					break;
				}
				Apply(entry, rows[i]);
				BindButtons(entry, i);
				list.Add(entry);
			}
		}
	}

	/// <summary>A window's elements set as a row of the answer names them; a name the window has not is passed over.</summary>
	private static void Apply(CGuiWindow window, CModWindowRow row)
	{
		foreach ((string name, string text) in row.Texts)
		{
			if (window.TryFindTextBox(name) is { } textBox)
			{
				textBox.Text = text;
			}
			else if (window.TryFindButton(name) is { } button)
			{
				button.SetText(text);
			}
		}
		foreach ((string name, bool shown) in row.Shown)
		{
			CGuiObject? element = window.TryFindElement(name);
			if (element is not null)
			{
				element.Visible = shown;
			}
			else if (window.TryFindListBox(name) is { } list)
			{
				list.Visible = shown;
			}
		}
	}

	private static void Bind(CGuiButton? button, Action action)
	{
		if (button is not null)
		{
			button.Pressed += action;
		}
	}
}
