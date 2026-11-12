using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The outliner: the window <c>outliner</c> down the right of the in-game screen, a section each for the player's
/// battles, units, allied objectives and bombings under the nine switches at its top. Each frame it gathers what the
/// sections hold and makes its rows again only where that has changed; otherwise each row brings itself up to date. See
/// <c>docs/mechanics/outliner.md</c>.
/// </summary>
public partial class COutliner : Control
{
	private const string WindowName = "outliner";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;
	private IReadOnlyList<int> _settings = Array.Empty<int>();

	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private CGuiIcon? _bottom;
	private readonly Dictionary<EOutlinerSection, CGuiButton> _switches = new();
	private readonly HashSet<CUnit> _expanded = new();
	/// <summary>The rows shown; none until the first frame after it opens, which always lays the list out - an empty one too.</summary>
	private List<COutlinerRow>? _shown;
	private readonly List<Action> _updates = new();
	private bool _covered;
	private bool _openWhenUncovered;

	/// <summary>A unit's row was pressed.</summary>
	public event Action<CUnit>? UnitChosen;

	/// <summary>A unit's row went down: its unit may be dragged into the order of battle browser.</summary>
	public event Action<CUnit>? UnitDragStarted;

	/// <summary>A battle's or a bombing's row was pressed.</summary>
	public event Action<CCombat>? CombatChosen;

	/// <summary>An allied objective's row was pressed: the map is to centre on the province.</summary>
	public event Action<int>? ProvinceChosen;

	/// <summary>Raised when it opens or shuts, for the top bar's button.</summary>
	public event Action? Toggled;

	/// <summary>Has to be called before the node enters the tree: the player's settings say what is switched on, and whether it is open.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player, IReadOnlyList<int> settings)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
		_settings = settings;
	}

	public bool IsOpen => Visible;

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			Visible = false;
			return;
		}
		AddChild(_window);
		_window.MouseFilter = MouseFilterEnum.Ignore;
		_list = _window.FindListBox("outliner_list");
		if (_context.Types.Find("outliner_bottom") is CGuiIconType bottom)
		{
			_bottom = new CGuiIcon();
			_bottom.Initialise(bottom, _context);
			_window.AddChild(_bottom);
		}
		foreach (EOutlinerSection section in Enum.GetValues<EOutlinerSection>())
		{
			if (_window.FindButton(COutlinerText.SwitchName(section)) is not { } checkbox)
			{
				continue;
			}
			checkbox.Checked = _settings.Contains((int)section);
			EOutlinerSection named = section;
			checkbox.Tooltip = () => new CTooltip { Text = COutlinerText.Title(_context.Localisation.Localise, named) };
			_switches[section] = checkbox;
		}
		// Ten in the settings is the outliner open (outliner.md, section 3).
		Visible = _settings.Contains(10);
	}

	/// <summary>The top bar's button: open it where it is shut, shut it where it is open.</summary>
	public void Toggle()
	{
		if (_covered)
		{
			return;
		}
		Visible = !Visible;
		if (Visible)
		{
			_shown = null;
		}
		Toggled?.Invoke();
	}

	/// <summary>
	/// Whether a country screen or the ledger is up: the outliner is shut while one is, and opens again after where it
	/// was shut so (outliner.md, section 1).
	/// </summary>
	public void SetCovered(bool covered)
	{
		if (covered == _covered)
		{
			return;
		}
		_covered = covered;
		if (covered)
		{
			_openWhenUncovered = Visible;
			Visible = false;
		}
		else if (_openWhenUncovered)
		{
			Visible = true;
			_shown = null;
		}
		Toggled?.Invoke();
	}

	/// <summary>Whether its button is to be shown: not while a screen covers it.</summary>
	public bool IsCovered => _covered;

	public override void _Process(double delta)
	{
		if (!Visible || _list is null)
		{
			return;
		}
		var wanted = new HashSet<EOutlinerSection>();
		foreach ((EOutlinerSection section, CGuiButton checkbox) in _switches)
		{
			if (checkbox.Checked)
			{
				wanted.Add(section);
			}
		}
		List<COutlinerRow> rows = COutlinerContents.Gather(_simulation.State, _player.Tag, wanted, _expanded.Contains);
		if (_shown is null || !SameRows(rows, _shown))
		{
			Rebuild(rows);
		}
		else
		{
			foreach (Action update in _updates)
			{
				update();
			}
		}
	}

	/// <summary>The same rows, allied objectives counted and not compared, as the original compares them.</summary>
	private static bool SameRows(List<COutlinerRow> rows, List<COutlinerRow> shown)
	{
		if (rows.Count != shown.Count)
		{
			return false;
		}
		for (int i = 0; i < rows.Count; i++)
		{
			if (rows[i].Section == EOutlinerSection.AlliedObjectives && shown[i].Section == EOutlinerSection.AlliedObjectives
				&& rows[i].IsTitle == shown[i].IsTitle)
			{
				continue;
			}
			if (rows[i] != shown[i])
			{
				return false;
			}
		}
		return true;
	}

	private void Rebuild(List<COutlinerRow> rows)
	{
		_shown = rows;
		_updates.Clear();
		_list!.Clear();
		CGameState state = _simulation.State;
		float height = 0f;
		foreach (COutlinerRow row in rows)
		{
			CGuiWindow? window = null;
			if (row.IsTitle)
			{
				window = COutlinerTitleEntry.Create(_context, row.Section)?.Window;
			}
			else if (row.Unit is { } unit)
			{
				if (COutlinerUnitEntry.Create(_context, state, _player, row, _expanded.Contains(unit)) is { } entry)
				{
					entry.Chosen += () => UnitChosen?.Invoke(unit);
					entry.DragStarted += () => UnitDragStarted?.Invoke(unit);
					entry.ExpandPressed += () => Expand(unit);
					_updates.Add(entry.Update);
					window = entry.Window;
				}
			}
			else if (row.Combat is { } combat)
			{
				if (COutlinerCombatEntry.Create(_context, state, combat, _player.Tag) is { } entry)
				{
					entry.Chosen += () => CombatChosen?.Invoke(combat);
					_updates.Add(entry.Update);
					window = entry.Window;
				}
			}
			else if (COutlinerObjectiveEntry.Create(_context, state, _player.Tag, row.Province, row.AskedOfPlayer) is { } entry)
			{
				int province = row.Province;
				entry.Chosen += () => ProvinceChosen?.Invoke(province);
				window = entry.Window;
			}
			if (window is not null)
			{
				_list.Add(window);
				height += window.Size.Y;
			}
		}
		// As high as its rows, up to a part of the screen; and its foot under it (outliner.md, section 1).
		float most = Mathf.Max(0f, Mathf.Floor((GetViewportRect().Size.Y - 358f) / 16f) * 16f - 48f);
		float shown = Mathf.Min(height, most);
		_list.SetHeight(shown);
		if (_bottom is not null)
		{
			_bottom.Position = new Vector2(0f, _list.Position.Y + shown);
		}
	}

	/// <summary>A unit's expand button: its subordinates shown or hidden, which the next frame's gathering sees.</summary>
	private void Expand(CUnit unit)
	{
		if (!_expanded.Remove(unit))
		{
			_expanded.Add(unit);
		}
	}
}
