using System;
using System.Collections.Generic;
using Godot;
using ImGuiGodot;
using ImGuiNET;
using OpenHOI3.Content.Windows;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The windows a mod defines for an enhanced game (<c>docs/mechanics/modwindows.md</c>), drawn with Dear ImGui: a menu
/// of the pages, which the Insert key shows and hides, and every page ticked on it in a window of its own. What a page
/// shows and how is its Lua's; this opens the windows, asks an open page now and then to gather what it shows - never
/// while the game's hours run beside the frame - and has it draw itself every frame, which it does from what it
/// gathered and with no game to ask. So it is processed always, and stands outside the HUD, which is not.
/// </summary>
public sealed partial class CModWindows : Node
{
	/// <summary>The localisation key of the menu's title.</summary>
	public const string MenuTitle = "OPENHOI_WINDOWS_MENU";

	/// <summary>How often an open page gathers again, in seconds of real time.</summary>
	private const double CollectEvery = 2.0;

	/// <summary>The typeface the windows are drawn in, one of ours, and its size in pixels of the canvas.</summary>
	private const string TypefacePath = "res://fonts/LiberationSans-Regular.ttf";
	private const int TypefaceSize = 16;

	private static bool _typefaceSet;

	private Func<string, string> _localise = null!;
	private Func<IModPages?> _pages = null!;
	private Func<bool> _gameBusy = null!;
	private CSimulation _simulation = null!;
	private CCountryTag _player;
	private readonly CImGuiCanvas _canvas = new();
	private readonly HashSet<string> _open = new(StringComparer.Ordinal);
	private readonly HashSet<string> _uncollected = new(StringComparer.Ordinal);
	private bool _shown;
	private ulong _collectedAt;

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="pages">The mod's pages, made the first time they are asked for; null where they cannot be.</param>
	/// <param name="gameBusy">Whether the game's hours run beside the frame, when no page may read the game.</param>
	public void Setup(Func<string, string> localise, Func<IModPages?> pages, Func<bool> gameBusy, CSimulation simulation, CCountryTag player)
	{
		_localise = localise;
		_pages = pages;
		_gameBusy = gameBusy;
		_simulation = simulation;
		_player = player;
	}

	public override void _Ready()
	{
		Name = "ModWindows";
		ProcessMode = ProcessModeEnum.Always;
		if (!_typefaceSet)
		{
			// One of the typefaces shipped, as everything drawn is: the library's own default is a bitmap of a dozen pixels.
			_typefaceSet = true;
			if (GD.Load<FontFile>(TypefacePath) is { } typeface)
			{
				ImGuiGD.ResetFonts();
				ImGuiGD.AddFont(typeface, TypefaceSize);
				ImGuiGD.RebuildFontAtlas();
			}
		}
	}

	public bool IsShown => _shown;

	/// <summary>
	/// Whether a text box of one of the windows is being typed into: the keys are then its own, and neither the game's
	/// edit box that has the keyboard nor the map's arrow keys take them.
	/// </summary>
	public bool IsTyping => _shown && ImGui.GetIO().WantTextInput;

	/// <summary>The menu and the pages open on it shown or hidden; which pages are open is kept while they are hidden.</summary>
	public void Toggle()
	{
		_shown = !_shown;
		if (_shown)
		{
			_uncollected.UnionWith(_open);
		}
	}

	/// <summary>The menu shown with a page open on it, by the page's name: for a run a script drives.</summary>
	public void Show(string pageName)
	{
		_shown = true;
		_open.Add(pageName);
		_uncollected.UnionWith(_open);
	}

	public override void _Process(double delta)
	{
		if (!_shown || _pages() is not { } pages)
		{
			return;
		}
		Collect(pages);

		bool shown = true;
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(40, 140), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(260, 420), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(_localise(MenuTitle) + "###openhoi_windows_menu", ref shown))
		{
			foreach (CModPage page in pages.Pages)
			{
				bool open = _open.Contains(page.Name);
				if (ImGui.Checkbox(Title(page) + "###" + page.Name, ref open))
				{
					SetOpen(page, open);
				}
			}
		}
		ImGui.End();

		foreach (CModPage page in pages.Pages)
		{
			if (!_open.Contains(page.Name))
			{
				continue;
			}
			bool open = true;
			ImGui.SetNextWindowPos(new System.Numerics.Vector2(340, 140), ImGuiCond.FirstUseEver);
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(520, 560), ImGuiCond.FirstUseEver);
			// The page's name is the window's own, whatever its title is in the language played.
			if (ImGui.Begin(Title(page) + "###openhoi_page_" + page.Name, ref open))
			{
				// A page opened while hours run has gathered nothing yet, and waits a frame or two for them.
				if (!_uncollected.Contains(page.Name))
				{
					pages.Draw(page, _player, _canvas);
				}
			}
			ImGui.End();
			if (!open)
			{
				SetOpen(page, false);
			}
		}
		if (!shown)
		{
			_shown = false;
		}
	}

	private string Title(CModPage page) => _localise(page.Title.Length > 0 ? page.Title : page.Name);

	private void SetOpen(CModPage page, bool open)
	{
		if (open)
		{
			_open.Add(page.Name);
			_uncollected.Add(page.Name);
		}
		else
		{
			_open.Remove(page.Name);
			_uncollected.Remove(page.Name);
		}
	}

	/// <summary>
	/// The open pages gather what they show: a page just opened at once, every one of them again when the interval is
	/// up - and none while the game's hours run beside the frame, when nothing may read the game.
	/// </summary>
	private void Collect(IModPages pages)
	{
		if (_gameBusy())
		{
			return;
		}
		bool due = Time.GetTicksMsec() - _collectedAt >= CollectEvery * 1000.0;
		if (!due && _uncollected.Count == 0)
		{
			return;
		}
		foreach (CModPage page in pages.Pages)
		{
			if (_open.Contains(page.Name) && (due || _uncollected.Contains(page.Name)))
			{
				pages.Collect(_simulation.State, page, _player);
			}
		}
		_uncollected.Clear();
		if (due)
		{
			_collectedAt = Time.GetTicksMsec();
		}
	}
}
