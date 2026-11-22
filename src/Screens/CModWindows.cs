using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ImGuiGodot;
using ImGuiNET;
using OpenHOI3.Content.Windows;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The window a mod defines for an enhanced game (<c>docs/mechanics/modwindows.md</c>), drawn with Dear ImGui and shown
/// and hidden by the Insert key: one window, a tab along its top for each group of pages, the tab's pages listed down
/// its left side, and the page chosen there on the rest of it. What a page shows and how is its Lua's; this asks the
/// page shown now and then to gather what it shows - never while the game's hours run beside the frame - and has it
/// draw itself every frame, which it does from what it gathered and with no game to ask. So it is processed always,
/// and stands outside the HUD, which is not.
/// </summary>
public sealed partial class CModWindows : Node
{
	/// <summary>The localisation key of the window's title.</summary>
	public const string WindowTitle = "OPENHOI_WINDOWS_MENU";

	/// <summary>The localisation key of the tab of the pages that name no tab of their own.</summary>
	public const string UngroupedTitle = "OPENHOI_WINDOWS_UNGROUPED";

	/// <summary>How often the page shown gathers again, in seconds of real time.</summary>
	private const double CollectEvery = 2.0;

	/// <summary>How wide the list of a tab's pages is, in pixels of the canvas.</summary>
	private const float ListWidth = 190f;

	/// <summary>The typeface the window is drawn in, one of ours, and its size in pixels of the canvas.</summary>
	private const string TypefacePath = "res://fonts/LiberationSans-Regular.ttf";
	private const int TypefaceSize = 16;

	private static bool _typefaceSet;

	private Func<string, string> _localise = null!;
	private Func<IModPages?> _pages = null!;
	private Func<bool> _gameBusy = null!;
	private CSimulation _simulation = null!;
	private CCountryTag _player;
	private readonly CImGuiCanvas _canvas = new();
	private List<CModPageGroup>? _groups;

	/// <summary>The page chosen under each tab, by the tab's key: kept while another tab is looked at.</summary>
	private readonly Dictionary<string, string> _chosen = new(StringComparer.Ordinal);

	/// <summary>A tab to be brought to the front at the next frame, by its key: a page shown by its name.</summary>
	private string? _bringForward;

	/// <summary>The page that has gathered and may be drawn; another shown gathers first.</summary>
	private string? _gathered;
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
	/// Whether a text box of the window is being typed into: the keys are then its own, and neither the game's edit box
	/// that has the keyboard nor the map's arrow keys take them.
	/// </summary>
	public bool IsTyping => _shown && ImGui.GetIO().WantTextInput;

	/// <summary>The window shown or hidden; the tab and the pages chosen are kept while it is hidden.</summary>
	public void Toggle()
	{
		_shown = !_shown;
		// Shown again, what it shows is gathered afresh.
		_gathered = null;
	}

	/// <summary>
	/// The window shown on a page, by the page's name - its path below the pages' folder, or its file's name alone:
	/// for a run a script drives.
	/// </summary>
	public void Show(string pageName)
	{
		_shown = true;
		_gathered = null;
		foreach (CModPageGroup group in Groups() ?? new List<CModPageGroup>())
		{
			if (group.Pages.FirstOrDefault(page => page.Name == pageName || page.Name.EndsWith("/" + pageName, StringComparison.Ordinal)) is { } found)
			{
				_chosen[group.Key] = found.Name;
				_bringForward = group.Key;
				return;
			}
		}
	}

	/// <summary>The pages sorted into tabs, once: they do not change while a game runs.</summary>
	private List<CModPageGroup>? Groups()
	{
		if (_groups is null && _pages() is { } pages)
		{
			_groups = CModPageGroup.Of(pages.Pages);
		}
		return _groups;
	}

	public override void _Process(double delta)
	{
		if (!_shown || _pages() is not { } pages || Groups() is not { } groups)
		{
			return;
		}

		bool shown = true;
		CModPage? showing = null;
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(60, 140), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(860, 620), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(_localise(WindowTitle) + "###openhoi_windows", ref shown))
		{
			if (ImGui.BeginTabBar("openhoi_groups"))
			{
				foreach (CModPageGroup group in groups)
				{
					ImGuiTabItemFlags flags = _bringForward == group.Key ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
					// The tab's key is its own, whatever its name is in the language played.
					if (BeginTab(_localise(group.Key.Length > 0 ? group.Key : UngroupedTitle) + "###tab_" + group.Key, flags))
					{
						showing = DrawGroup(pages, group);
						ImGui.EndTabItem();
					}
				}
				_bringForward = null;
				ImGui.EndTabBar();
			}
		}
		ImGui.End();
		if (!shown)
		{
			_shown = false;
		}
		// What the page ordered as it drew - a button pressed - is posted as a player's order is, which waits by itself
		// where hours run beside the frame; and the page gathers again a quarter of a second on, to show what came of it.
		IReadOnlyList<Sim.Commands.CCommand> posted = pages.TakePosted();
		foreach (Sim.Commands.CCommand command in posted)
		{
			_simulation.Post(command);
		}
		if (posted.Count > 0)
		{
			_collectedAt = Time.GetTicksMsec() - (ulong)((CollectEvery - AfterAnOrder) * 1000.0);
		}
		// A page that asked for it as it drew gathers again at once.
		if (pages.TakeRefresh())
		{
			_collectedAt = 0;
		}
		// Gathered after the frame's drawing, for the next: the page seen now, when it is new or its interval is up.
		Collect(pages, showing);
	}

	/// <summary>How soon after a page's order the page gathers again, in seconds.</summary>
	private const double AfterAnOrder = 0.25;

	/// <summary>
	/// A tab begun with flags and no close box: the binding's own call with flags asks for the box's variable, which
	/// would draw one, so the library is asked directly.
	/// </summary>
	private static unsafe bool BeginTab(string label, ImGuiTabItemFlags flags)
	{
		byte[] text = System.Text.Encoding.UTF8.GetBytes(label + "\0");
		fixed (byte* pointer = text)
		{
			return ImGuiNative.igBeginTabItem(pointer, null, flags) != 0;
		}
	}

	/// <summary>A tab's inside: its pages down the left, the one chosen on the right. Answers the page chosen.</summary>
	private CModPage? DrawGroup(IModPages pages, CModPageGroup group)
	{
		if (group.Pages.Count == 0)
		{
			return null;
		}
		CModPage chosen = group.Pages.FirstOrDefault(page => _chosen.TryGetValue(group.Key, out string? name) && page.Name == name)
			?? group.Pages[0];
		if (ImGui.BeginChild("pages_" + group.Key, new System.Numerics.Vector2(ListWidth, 0), ImGuiChildFlags.Borders))
		{
			foreach (CModPage page in group.Pages)
			{
				if (ImGui.Selectable(_localise(page.Title.Length > 0 ? page.Title : page.Name) + "###page_" + page.Name, page == chosen))
				{
					chosen = page;
				}
			}
		}
		ImGui.EndChild();
		_chosen[group.Key] = chosen.Name;

		ImGui.SameLine();
		// The page's name is the part's own, so that each page keeps its own scrolling.
		if (ImGui.BeginChild("page_" + chosen.Name, new System.Numerics.Vector2(0, 0)))
		{
			// A page not yet gathered - just chosen, or chosen while hours run - waits a frame or two, undrawn.
			if (_gathered == chosen.Name)
			{
				pages.Draw(chosen, _player, _canvas);
			}
		}
		ImGui.EndChild();
		return chosen;
	}

	/// <summary>
	/// The page shown gathers what it shows: at once where it is another than the last, and again when the interval is
	/// up - and not while the game's hours run beside the frame, when nothing may read the game.
	/// </summary>
	private void Collect(IModPages pages, CModPage? showing)
	{
		if (showing is null || _gameBusy())
		{
			return;
		}
		bool due = Time.GetTicksMsec() - _collectedAt >= CollectEvery * 1000.0;
		if (_gathered != showing.Name || due)
		{
			pages.Collect(_simulation.State, showing, _player);
			_gathered = showing.Name;
			_collectedAt = Time.GetTicksMsec();
		}
	}
}
