using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Screens;

/// <summary>
/// The victory conditions' window, <c>vc_window</c>, behind the bottom bar's goals button and the alert: a picture and a list of the conditions of the player's
/// faction, and each faction's count and bar. The game goes on under it. See <c>docs/mechanics/endgame.md</c>, section 5.
/// </summary>
public sealed partial class CVictoryConditionsView : Control
{
	private static readonly (string Tag, string Short)[] Bars = { ("axis", "axis"), ("allies", "allies"), ("comintern", "comintern") };

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;
	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private readonly List<(CGuiIcon? Background, CVictoryCondition Condition)> _rows = new();
	private string? _builtFor;
	private CDate? _filledOn;

	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	/// <summary>Raised when it opens, which shuts the Strategic Effects window.</summary>
	public event Action? Opened;

	public bool IsOpen => _window is { Visible: true };

	public override void _Ready()
	{
		Name = "VictoryConditions";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, CVictoryWindowText.WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_list = _window.FindListBox("vc_list");
		if (_window.TryFindButton("close_button") is { } close)
		{
			close.Pressed += () => _window.Visible = false;
		}
		_window.Visible = false;
	}

	/// <summary>Opens it where it is shut, and shuts it where it is open.</summary>
	public void Toggle()
	{
		if (_window is null)
		{
			return;
		}
		if (_window.Visible)
		{
			_window.Visible = false;
			return;
		}
		_window.Visible = true;
		_builtFor = null;
		_filledOn = null;
		Refresh();
		Opened?.Invoke();
	}

	/// <summary>Shuts it, which the Strategic Effects window's opening does.</summary>
	public void Shut()
	{
		if (_window is not null)
		{
			_window.Visible = false;
		}
	}

	public override void _Process(double delta)
	{
		if (IsOpen)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		if (_window is null)
		{
			return;
		}
		CGameState state = _simulation.State;
		if (_player.Faction != _builtFor)
		{
			Build(state);
		}
		if (_filledOn == state.Date)
		{
			return;
		}
		_filledOn = state.Date;
		foreach ((string tag, string name) in Bars)
		{
			if (_window.FindTextBox($"vc_{name}_progress_text") is { } text)
			{
				text.Text = CVictoryWindowText.ProgressText(state, tag);
			}
			_window.TryFindIcon($"vc_{name}_progress")?.SetProgress(CVictoryWindowText.Progress(state, tag));
		}
		foreach ((CGuiIcon? background, CVictoryCondition condition) in _rows)
		{
			background?.SetFrame(CVictoryWindowText.Frame(condition));
		}
	}

	/// <summary>The picture of the player's faction and its rows; with no faction of those, no picture and no list, and the file's own line shows.</summary>
	private void Build(CGameState state)
	{
		string? picture = CVictoryWindowText.Picture(_player.Faction);
		foreach (string name in new[] { "vc_bg_axis", "vc_bg_allies", "vc_bg_com" })
		{
			if (_window!.TryFindIcon(name) is { } icon)
			{
				icon.Visible = name == picture;
			}
		}
		if (_list is null)
		{
			return;
		}
		_list.Visible = picture is not null;
		if (picture is null)
		{
			return;
		}
		_builtFor = _player.Faction;
		_list.Clear();
		_rows.Clear();
		Func<string, string> localise = _context.Localisation.Localise;
		foreach (CVictoryCondition condition in CEndGameText.Conditions(state, _player.Faction))
		{
			if (CGuiWindow.CreateEntry(_context, CVictoryWindowText.RowName) is not { } row)
			{
				continue;
			}
			if (row.FindTextBox("vc_name") is { } name)
			{
				name.Text = localise(condition.Name);
			}
			CVictoryCondition shown = condition;
			row.SetTooltips(_ => new CTooltip
			{
				Text = CVictoryWindowText.RowTooltip(new CScriptWords(localise, _simulation.State, _player.Tag), shown),
			}, "vc_name");
			_rows.Add((row.TryFindIcon("vc_entry_bg"), condition));
			_list.Add(row);
		}
		_filledOn = null;
	}
}
