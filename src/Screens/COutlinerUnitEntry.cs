using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A unit's row in the outliner: an <c>outliner_unit_entry_bg</c> window, whose <c>outliner_standard</c> is the whole
/// row, and in it an <c>outliner_top_unit_entry</c> window with the expand button, the name, the figure and the arrow of
/// a unit on the move. A land row is indented by <c>outliner_browser_offset</c> a level. See
/// <c>docs/mechanics/outliner.md</c>, section 5.
/// </summary>
public sealed class COutlinerUnitEntry
{
	private readonly CGameState _state;
	private readonly CCountry _player;
	private readonly CUnit _unit;
	private readonly bool _land;
	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _figure;
	private readonly CGuiIcon? _moving;
	private readonly CGuiButton? _expand;
	private string _figureFor;

	/// <summary>Raised when the row is pressed.</summary>
	public event Action? Chosen;

	/// <summary>Raised when the expand button is pressed.</summary>
	public event Action? ExpandPressed;

	private COutlinerUnitEntry(CGuiContext context, CGuiWindow window, CGuiWindow unitWindow, CGameState state, CCountry player,
		COutlinerRow row, bool expanded)
	{
		Window = window;
		_state = state;
		_player = player;
		_unit = row.Unit!;
		_land = row.Section == EOutlinerSection.LandUnits;
		window.AddChild(unitWindow);
		// What lies over the row's button takes none of its clicks, the expand button aside.
		unitWindow.MouseFilter = Control.MouseFilterEnum.Ignore;
		if (_land && context.Types.FindPosition("outliner_browser_offset") is { } offset)
		{
			unitWindow.Position = new Vector2(offset.Position.X * row.Depth, unitWindow.Position.Y);
		}
		_name = unitWindow.FindTextBox("entry_text");
		_figure = unitWindow.FindTextBox("info_text");
		_moving = unitWindow.FindIcon("movment");
		_expand = unitWindow.FindButton("expand_button");
		_expand?.SetFrame(expanded ? 2 : 1);
		if (_expand is not null)
		{
			_expand.Pressed += () => ExpandPressed?.Invoke();
		}
		if (_figure is not null)
		{
			_figure.Text = COutlinerText.UnitFigure(state, _unit, _land, made: true);
		}
		_figureFor = Figure();
		if (_moving is not null)
		{
			_moving.Visible = _unit.IsMoving;
		}
		Update();

		Func<string, CTooltip> tooltip = name => COutlinerTooltips.Unit(context.Localisation.Localise, state, _unit, name);
		window.SetTooltips(tooltip);
		unitWindow.SetTooltips(tooltip, "entry_text", "info_text", "movment");
		if (window.FindButton("outliner_standard") is { } background)
		{
			background.Pressed += () => Chosen?.Invoke();
		}
	}

	public CGuiWindow Window { get; }

	public CUnit Unit => _unit;

	public static COutlinerUnitEntry? Create(CGuiContext context, CGameState state, CCountry player, COutlinerRow row,
		bool expanded)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "outliner_unit_entry_bg");
		CGuiWindow? unitWindow = CGuiWindow.CreateEntry(context, "outliner_top_unit_entry");
		if (window is null || unitWindow is null || row.Unit is null)
		{
			window?.QueueFree();
			unitWindow?.QueueFree();
			return null;
		}
		return new COutlinerUnitEntry(context, window, unitWindow, state, player, row, expanded);
	}

	/// <summary>
	/// Each frame: the name, the figure where what it counts has changed - written plainly, the shatter's red gone - a
	/// land unit's arrow, and the expand button enabled only with subordinates to show.
	/// </summary>
	public void Update()
	{
		if (_name is not null)
		{
			_name.Text = COutlinerText.UnitName(_player, _unit);
		}
		string figure = Figure();
		if (_figure is not null && figure != _figureFor)
		{
			_figureFor = figure;
			_figure.Text = figure;
		}
		if (_land && _moving is not null)
		{
			_moving.Visible = _unit.IsMoving;
		}
		if (_expand is not null)
		{
			_expand.Disabled = _unit.Children.Count == 0;
		}
	}

	private string Figure() => COutlinerText.UnitFigure(_state, _unit, _land, made: false);
}
