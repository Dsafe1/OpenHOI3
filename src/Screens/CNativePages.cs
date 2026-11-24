using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Windows;
using OpenHOI3.Gui;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// A mod's native pages on screen, in an enhanced game: each a window the interface files declare, filled by the
/// page's Lua. A page with a host screen has an opener put up while that screen is open, and its window goes when the
/// screen shuts. The page gathers as a page of the mod's own windows does - as its window opens, every two seconds and
/// when it asks, never while hours run - and what it orders is posted as a player's order. See
/// <c>docs/mechanics/nativepages.md</c>.
/// </summary>
public partial class CNativePages : Control
{
	/// <summary>How often an open page gathers again, in seconds, and how soon after an order of its own.</summary>
	private const double CollectEvery = 2.0;
	private const double AfterAnOrder = 0.25;

	/// <summary>The button in a page's opener window that opens and shuts the page's window.</summary>
	public const string OpenerButton = "open";

	/// <summary>The button in a page's window, where it has one, that shuts it.</summary>
	public const string CloseButton = "close";

	private sealed class CShown
	{
		public required CModPage Page { get; init; }
		public CGuiWindow? Opener { get; set; }
		public CGuiWindow? Window { get; set; }
		public CNativeWindowCanvas? Canvas { get; set; }
		public bool Gathered { get; set; }
		public ulong CollectedAt { get; set; }
	}

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private Func<IModPages?> _pages = () => null;
	private Func<bool> _busy = () => false;
	private Func<string, bool> _hostOpen = _ => false;
	private CCountryTag _player;
	private List<CShown>? _shown;

	public void Setup(CGuiContext context, CSimulation simulation, Func<IModPages?> pages, Func<bool> busy, Func<string, bool> hostOpen,
		CCountryTag player)
	{
		_context = context;
		_simulation = simulation;
		_pages = pages;
		_busy = busy;
		_hostOpen = hostOpen;
		_player = player;
	}

	public override void _Ready()
	{
		Name = "NativePages";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Opens a page's window by the page's name, or shuts it where it is up. Answers whether there is such a page.</summary>
	public bool Toggle(string name)
	{
		if (Pages()?.FirstOrDefault(shown => shown.Page.Name == name) is not { } page)
		{
			return false;
		}
		if (page.Window is null)
		{
			Open(page);
		}
		else
		{
			Shut(page);
		}
		return true;
	}

	private List<CShown>? Pages()
	{
		if (_shown is null && _pages() is { } pages)
		{
			_shown = pages.Pages.Where(page => page.IsNative).Select(page => new CShown { Page = page }).ToList();
		}
		return _shown;
	}

	private void Open(CShown page)
	{
		if (page.Window is not null || CGuiWindow.Create(_context, page.Page.Window) is not { } window)
		{
			return;
		}
		page.Window = window;
		page.Canvas = new CNativeWindowCanvas(window, _context, page.Page.Name);
		page.Gathered = false;
		page.CollectedAt = 0;
		AddChild(window);
		// Hidden until it is gathered and filled once: no frame of the bare layout.
		window.Visible = false;
		if (window.TryFindButton(CloseButton) is { } close)
		{
			close.Pressed += () => Shut(page);
		}
	}

	private static void Shut(CShown page)
	{
		page.Window?.QueueFree();
		page.Window = null;
		page.Canvas = null;
	}

	public override void _Process(double delta)
	{
		if (Pages() is not { } shown || _pages() is not { } pages)
		{
			return;
		}
		foreach (CShown page in shown)
		{
			bool hosted = page.Page.Host.Length > 0;
			bool hostOpen = hosted && _hostOpen(page.Page.Host);
			// The opener stands while the host screen is open, and the window goes with the screen.
			if (hosted && page.Page.Opener.Length > 0)
			{
				if (hostOpen && page.Opener is null && CGuiWindow.Create(_context, page.Page.Opener) is { } opener)
				{
					page.Opener = opener;
					AddChild(opener);
					// Under every page's window.
					MoveChild(opener, 0);
					CShown mine = page;
					if (opener.TryFindButton(OpenerButton) is { } button)
					{
						button.Pressed += () => Toggle(mine.Page.Name);
					}
				}
				else if (!hostOpen && page.Opener is not null)
				{
					page.Opener.QueueFree();
					page.Opener = null;
				}
			}
			if (hosted && !hostOpen && page.Window is not null)
			{
				Shut(page);
			}
			if (page.Window is null || page.Canvas is null)
			{
				continue;
			}

			if (page.Gathered)
			{
				pages.Fill(page.Page, _player, page.Canvas);
				page.Window.Visible = true;
				if (page.Canvas.TakeClose())
				{
					Shut(page);
					continue;
				}
			}
			// What it ordered as it filled is posted as a player's order, and it gathers again soon after.
			IReadOnlyList<Sim.Commands.CCommand> posted = pages.TakePosted();
			foreach (Sim.Commands.CCommand command in posted)
			{
				_simulation.Post(command);
			}
			if (posted.Count > 0)
			{
				page.CollectedAt = Time.GetTicksMsec() - (ulong)((CollectEvery - AfterAnOrder) * 1000.0);
			}
			if (pages.TakeRefresh())
			{
				page.CollectedAt = 0;
			}
			// Gathered after the frame's filling, for the next - and not while the game's hours run beside the frame.
			if (!_busy() && (!page.Gathered || Time.GetTicksMsec() - page.CollectedAt >= CollectEvery * 1000.0))
			{
				pages.Collect(_simulation.State, page.Page, _player);
				page.Gathered = true;
				page.CollectedAt = Time.GetTicksMsec();
			}
		}
	}
}
