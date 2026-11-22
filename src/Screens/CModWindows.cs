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
		_canvas.Setting = Setting;
		_canvas.SetSetting = SetSetting;
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

	/// <summary>
	/// Whether the window has the keyboard - it or a part of it was the last thing clicked: the arrow keys then go
	/// through its lists and do not scroll the map.
	/// </summary>
	public bool HasKeyboard => _shown && ImGui.IsWindowFocused(ImGuiFocusedFlags.AnyWindow);

	/// <summary>A picture by its path below the install, for a page that draws one.</summary>
	public Func<string, Texture2D?>? Texture
	{
		set => _canvas.Texture = value;
	}

	/// <summary>The key that shows and hides the window: Insert until the player chooses another.</summary>
	public Key ToggleKey { get; private set; } = Key.Insert;

	/// <summary>The window's look, one of <see cref="Content.Settings.CEnhancedSettings.WindowThemes"/>.</summary>
	public string Theme { get; private set; } = Content.Settings.CEnhancedSettings.DefaultWindowTheme;

	/// <summary>Raised when the player has chosen another key or look, for whoever keeps the settings: the key's name, the look.</summary>
	public event Action<string, string>? SettingsChanged;

	/// <summary>Whether the next key pressed is to become the key that shows and hides the window.</summary>
	private bool _capturing;

	/// <summary>Whether the window is to be put back where and as large as it first stood, at its next drawing.</summary>
	private bool _resetLayout;

	/// <summary>The key and the look as the player's settings have them, at the start of a game.</summary>
	public void Apply(string key, string theme)
	{
		Key found = OS.FindKeycodeFromString(key);
		ToggleKey = found == Key.None ? Key.Insert : found;
		SetTheme(theme);
	}

	private void SetTheme(string theme)
	{
		Theme = Array.IndexOf(Content.Settings.CEnhancedSettings.WindowThemes, theme) >= 0
			? theme
			: Content.Settings.CEnhancedSettings.DefaultWindowTheme;
		switch (Theme)
		{
			case "light":
				ImGui.StyleColorsLight();
				break;
			case "classic":
				ImGui.StyleColorsClassic();
				break;
			case "army_green":
				ArmyGreen();
				break;
			default:
				ImGui.StyleColorsDark();
				break;
		}
	}

	/// <summary>
	/// Olive panels and amber headings: the dark look with its blue taken out. What holds something is olive, darker
	/// the further back it stands; what is chosen, held or marks a place is amber; text is a pale khaki.
	/// </summary>
	private static void ArmyGreen()
	{
		ImGui.StyleColorsDark();
		ImGuiStylePtr style = ImGui.GetStyle();
		static System.Numerics.Vector4 Rgb(int red, int green, int blue, float alpha = 1f) =>
			new(red / 255f, green / 255f, blue / 255f, alpha);
		var text = Rgb(226, 222, 196);
		var dim = Rgb(140, 142, 112);
		var back = Rgb(30, 34, 22, 0.96f);
		var panel = Rgb(38, 44, 28);
		var frame = Rgb(58, 66, 40);
		var frameHover = Rgb(76, 86, 52);
		var olive = Rgb(88, 100, 58);
		var oliveHover = Rgb(108, 122, 70);
		var amber = Rgb(226, 162, 38);
		var amberDeep = Rgb(168, 118, 26);
		var border = Rgb(96, 104, 66, 0.6f);

		style.Colors[(int)ImGuiCol.Text] = text;
		style.Colors[(int)ImGuiCol.TextDisabled] = dim;
		style.Colors[(int)ImGuiCol.WindowBg] = back;
		style.Colors[(int)ImGuiCol.ChildBg] = Rgb(0, 0, 0, 0f);
		style.Colors[(int)ImGuiCol.PopupBg] = Rgb(34, 38, 24, 0.98f);
		style.Colors[(int)ImGuiCol.Border] = border;
		style.Colors[(int)ImGuiCol.FrameBg] = frame;
		style.Colors[(int)ImGuiCol.FrameBgHovered] = frameHover;
		style.Colors[(int)ImGuiCol.FrameBgActive] = olive;
		style.Colors[(int)ImGuiCol.TitleBg] = panel;
		style.Colors[(int)ImGuiCol.TitleBgActive] = amberDeep;
		style.Colors[(int)ImGuiCol.TitleBgCollapsed] = panel;
		style.Colors[(int)ImGuiCol.MenuBarBg] = panel;
		style.Colors[(int)ImGuiCol.ScrollbarBg] = Rgb(24, 28, 18, 0.6f);
		style.Colors[(int)ImGuiCol.ScrollbarGrab] = olive;
		style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = oliveHover;
		style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = amber;
		style.Colors[(int)ImGuiCol.CheckMark] = amber;
		style.Colors[(int)ImGuiCol.SliderGrab] = amberDeep;
		style.Colors[(int)ImGuiCol.SliderGrabActive] = amber;
		style.Colors[(int)ImGuiCol.Button] = olive;
		style.Colors[(int)ImGuiCol.ButtonHovered] = oliveHover;
		style.Colors[(int)ImGuiCol.ButtonActive] = amberDeep;
		style.Colors[(int)ImGuiCol.Header] = Rgb(168, 118, 26, 0.55f);
		style.Colors[(int)ImGuiCol.HeaderHovered] = Rgb(226, 162, 38, 0.45f);
		style.Colors[(int)ImGuiCol.HeaderActive] = amberDeep;
		style.Colors[(int)ImGuiCol.Separator] = Rgb(226, 162, 38, 0.6f);
		style.Colors[(int)ImGuiCol.SeparatorHovered] = amber;
		style.Colors[(int)ImGuiCol.SeparatorActive] = amber;
		style.Colors[(int)ImGuiCol.ResizeGrip] = Rgb(168, 118, 26, 0.4f);
		style.Colors[(int)ImGuiCol.ResizeGripHovered] = amberDeep;
		style.Colors[(int)ImGuiCol.ResizeGripActive] = amber;
		style.Colors[(int)ImGuiCol.Tab] = frame;
		style.Colors[(int)ImGuiCol.TabHovered] = amberDeep;
		style.Colors[(int)ImGuiCol.TabSelected] = amberDeep;
		style.Colors[(int)ImGuiCol.TabDimmed] = panel;
		style.Colors[(int)ImGuiCol.TabDimmedSelected] = frame;
		style.Colors[(int)ImGuiCol.PlotLines] = amber;
		style.Colors[(int)ImGuiCol.PlotLinesHovered] = text;
		style.Colors[(int)ImGuiCol.PlotHistogram] = amberDeep;
		style.Colors[(int)ImGuiCol.TableHeaderBg] = Rgb(58, 66, 40);
		style.Colors[(int)ImGuiCol.TableBorderStrong] = border;
		style.Colors[(int)ImGuiCol.TableBorderLight] = Rgb(96, 104, 66, 0.35f);
		style.Colors[(int)ImGuiCol.TableRowBgAlt] = Rgb(255, 255, 255, 0.03f);
		style.Colors[(int)ImGuiCol.TextSelectedBg] = Rgb(226, 162, 38, 0.35f);
		style.Colors[(int)ImGuiCol.NavCursor] = amber;
	}

	/// <summary>A setting of the window's as a page reads it (<c>IModWindowCanvas.WindowSetting</c>).</summary>
	private string Setting(string name) => name switch
	{
		"scale" => ImGui.GetIO().FontGlobalScale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
		"theme" => Theme,
		"themes" => string.Join(' ', Content.Settings.CEnhancedSettings.WindowThemes),
		"key" => OS.GetKeycodeString(ToggleKey),
		"capturing" => _capturing ? "yes" : "no",
		_ => "",
	};

	/// <summary>A setting of the window's as a page sets it (<c>IModWindowCanvas.SetWindowSetting</c>).</summary>
	private void SetSetting(string name, string value)
	{
		switch (name)
		{
			case "scale" when float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale):
				// The text's size alone, and not kept: it is the library's own factor, as the overlay's was.
				ImGui.GetIO().FontGlobalScale = Mathf.Clamp(scale, 0.6f, 2.0f);
				break;
			case "theme":
				SetTheme(value);
				SettingsChanged?.Invoke(OS.GetKeycodeString(ToggleKey), Theme);
				break;
			case "key":
				_capturing = value == "capture";
				break;
			case "layout" when value == "reset":
				_resetLayout = true;
				break;
		}
	}

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

	/// <summary>
	/// The mouse over the window, or held on it, is the window's alone. The library's own layer takes its input after
	/// every node of the game's that asks for raw input - a moveable window of the game's, which is dragged by the
	/// button held on it, a list's wheel - so a press on this window reached the event window beneath it as well. This
	/// node is kept the last of the session's, which is the first asked; while the library wants the mouse it hands a
	/// mouse event to the library itself and lets nobody else have it.
	///
	/// The arrow keys go the same way while the window has the keyboard, for another reason: the program's root stops
	/// every arrow key before Godot's own interface answers it (<c>Main._Input</c>), which is before the library's
	/// layer too - so a list of a page never heard one, and a box of a page could not move its caret by one.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!_shown)
		{
			return;
		}
		// Choosing the key that shows the window: the next key pressed is it, and nobody else hears that key. Escape
		// gives the choosing up.
		if (_capturing && @event is InputEventKey { Pressed: true, Echo: false } pressed)
		{
			_capturing = false;
			if (pressed.Keycode != Key.Escape && pressed.Keycode != Key.None)
			{
				ToggleKey = pressed.Keycode;
				SettingsChanged?.Invoke(OS.GetKeycodeString(ToggleKey), Theme);
			}
			GetViewport().SetInputAsHandled();
			return;
		}
		bool mouse = @event is InputEventMouse && ImGui.GetIO().WantCaptureMouse;
		bool arrow = @event is InputEventKey { Keycode: Key.Left or Key.Right or Key.Up or Key.Down } && HasKeyboard;
		if (mouse || arrow)
		{
			ImGuiController.WindowInputCallback(@event);
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		// The last child is the first asked for input: nodes added since - a popup, an event's window - come before it.
		if (GetParent() is { } parent && GetIndex() != parent.GetChildCount() - 1)
		{
			parent.MoveChild(this, -1);
		}
		if (!_shown || _pages() is not { } pages || Groups() is not { } groups)
		{
			return;
		}

		bool shown = true;
		CModPage? showing = null;
		// Where and as large as it first stands; afterwards as the player left it, which the library keeps - until a
		// page asks for the layout to be put back.
		ImGuiCond when = _resetLayout ? ImGuiCond.Always : ImGuiCond.FirstUseEver;
		_resetLayout = false;
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(60, 140), when);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(860, 620), when);
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
