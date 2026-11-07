using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Messages;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Messages;

namespace OpenHOI3.Screens;

/// <summary>
/// The message log on the bottom bar: <c>messagelog_window</c> of the <c>menubar</c> window, its list
/// of rows - a <c>logtext</c> each, the newest at the bottom - its six category boxes, a ticked one
/// hiding its category, and the buttons that close it and open it again - and the button by the minimap
/// that opens the Strategic Effects window, which it hands on. The rest of the bar - the minimap, the
/// map modes' buttons, the chat of a game of several players - is not built and not shown. See
/// <c>docs/mechanics/messages.md</c>, section 5, and <c>docs/mechanics/strategiceffects.md</c>.
/// </summary>
public sealed partial class CMessageLogView : Control
{
	public const string BarName = "menubar";
	public const string RowName = "logtext";

	private static readonly (string Box, EMessageCategory Category)[] Boxes =
	{
		("messagecat_combat", EMessageCategory.Combat),
		("messagecat_diplomacy", EMessageCategory.Diplomacy),
		("messagecat_units", EMessageCategory.Units),
		("messagecat_provinces", EMessageCategory.Province),
		("messagecat_events", EMessageCategory.Events),
		("messagecat_others", EMessageCategory.Other),
	};

	private CGuiContext _context = null!;
	private CGuiWindow? _log;
	private CGuiListBox? _list;
	private CGuiButton? _open;

	public CMessageLog Log { get; private set; } = null!;

	/// <summary>Raised with a map mode button's number, from one, when it is pressed.</summary>
	public event Action<int>? ModeButtonPressed;

	/// <summary>Raised when the bar's zoom-in button is pressed, or Page Down: <c>minimap.md</c>, section 4.</summary>
	public event Action? ZoomInPressed;

	/// <summary>Raised when the bar's zoom-out button is pressed, or Page Up.</summary>
	public event Action? ZoomOutPressed;

	private readonly CGuiButton?[] _modeButtons = new CGuiButton?[Interface.Map.CMapModeButtons.Count];

	/// <summary>
	/// Shows a map mode button down - its second frame - and every other up, as the original's does by the button's own
	/// number. Nothing is ever hidden or greyed.
	/// </summary>
	public void ShowModeButton(int number)
	{
		for (int index = 0; index < _modeButtons.Length; index++)
		{
			_modeButtons[index]?.SetFrame(index + 1 == number
				? Interface.Map.CMapModeButtons.PressedFrame
				: Interface.Map.CMapModeButtons.ReleasedFrame);
		}
	}

	/// <summary>The button by the minimap that opens and shuts the Strategic Effects window (<c>strategiceffects.md</c>).</summary>
	public const string ModifiersButtonName = "menubar_modifiers_button";

	/// <summary>The bar's two zoom buttons, by the minimap.</summary>
	public const string ZoomInName = "map_zoom_in";
	public const string ZoomOutName = "map_zoom_out";

	/// <summary>Raised when that button is pressed.</summary>
	public event System.Action? ModifiersPressed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context)
	{
		_context = context;
		// A row's text is as wide as its button less 10, in the button's font.
		float width = 420f;
		CGuiFont? font = context.Fonts.Find("Arial12_alpha");
		if (context.Types.FindWindow(RowName)?.Children is { } row)
		{
			foreach (CGuiType child in row)
			{
				if (child is CGuiButtonType button && child.Name == "messagelogbutton")
				{
					width = button.Size.X - 10f;
					font = context.Fonts.Find(button.Font) ?? font;
				}
			}
		}
		Log = new CMessageLog(context.Localisation.Localise, width, text => font?.Width(text) ?? text.Length);
	}

	public override void _Ready()
	{
		Name = "MessageLog";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		if (CGuiWindow.Create(_context, BarName) is not { } bar)
		{
			return;
		}
		AddChild(bar);
		// The bar holds the chat as well, which is not built, so it is not shown; the log, the Strategic Effects button, the
		// map modes' buttons, the zoom buttons and the minimap's shadow are (minimap.md).
		foreach (Node child in bar.GetChildren())
		{
			string name = child.Name.ToString();
			if (child is Control shown && name != "messagelog_window" && name != "OPENbutton" && name != ModifiersButtonName
				&& name != "minimap_bg_shadow" && name != ZoomInName && name != ZoomOutName
				&& !name.StartsWith("mapmode_", StringComparison.Ordinal))
			{
				shown.Visible = false;
			}
		}
		for (int number = 1; number <= Interface.Map.CMapModeButtons.Count; number++)
		{
			if (bar.TryFindButton(Interface.Map.CMapModeButtons.Name(number)) is not { } button)
			{
				continue;
			}
			int pressed = number;
			button.Pressed += () => ModeButtonPressed?.Invoke(pressed);
			string tooltip = _context.Localisation.Localise(Interface.Map.CMapModeButtons.TooltipKey(number));
			button.Tooltip = () => new Interface.Tooltips.CTooltip { Text = tooltip };
			_modeButtons[number - 1] = button;
		}
		if (bar.TryFindButton(ZoomInName) is { } zoomIn)
		{
			zoomIn.Pressed += () => ZoomInPressed?.Invoke();
		}
		if (bar.TryFindButton(ZoomOutName) is { } zoomOut)
		{
			zoomOut.Pressed += () => ZoomOutPressed?.Invoke();
		}
		if (bar.TryFindButton(ModifiersButtonName) is { } modifiers)
		{
			modifiers.Pressed += () => ModifiersPressed?.Invoke();
			modifiers.Tooltip = () => new Interface.Tooltips.CTooltip
			{
				Text = _context.Localisation.Localise(Interface.Screens.CStrategicEffectsText.ButtonTooltip),
			};
		}
		_log = bar.FindWindow("messagelog_window");
		_list = _log?.FindListBox("messagelog");
		foreach ((string box, EMessageCategory category) in Boxes)
		{
			if (_log?.TryFindButton(box) is { } shown)
			{
				shown.Pressed += () =>
				{
					Log.SetHidden(category, shown.Checked);
					Refill();
				};
				// The original answers for the boxes by name, the message settings' tabs' names (ingamemenu.md, section 4).
				if (CMessageSettingsText.TabTooltip(_context.Localisation.Localise, box) is { } tooltip)
				{
					shown.Tooltip = () => new Interface.Tooltips.CTooltip { Text = tooltip };
				}
			}
		}
		if (_log?.TryFindButton("closebutton") is { } close)
		{
			close.Pressed += () => SetOpen(false);
		}
		_open = bar.TryFindButton("OPENbutton");
		if (_open is not null)
		{
			_open.Pressed += () => SetOpen(true);
		}
		SetOpen(true);
	}

	/// <summary>A message of a type that is logged.</summary>
	public void Add(CMessage message)
	{
		Log.Add(message);
		Refill();
	}

	/// <summary>Ours: the open button shows only while the log is shut, which was not read.</summary>
	private void SetOpen(bool open)
	{
		if (_log is not null)
		{
			_log.Visible = open;
		}
		if (_open is not null)
		{
			_open.Visible = !open;
		}
	}

	private void Refill()
	{
		if (_list is null)
		{
			return;
		}
		_list.Clear();
		foreach ((CMessage _, string text) in Log.Rows)
		{
			if (CGuiWindow.CreateEntry(_context, RowName) is not { } row)
			{
				return;
			}
			row.TryFindButton("messagelogbutton")?.SetText(text);
			_list.Add(row);
		}
		_list.ShowLast();
	}
}
