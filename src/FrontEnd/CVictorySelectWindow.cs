using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The lobby's window of victory conditions, <c>victory_conditions_window</c>, for a faction's first member: a row for every condition of the faction, ticked where chosen, no limit on
/// the ticks, and Accept fitting them to the number at random. See <c>docs/mechanics/endgame.md</c>, section 7.
/// </summary>
public sealed partial class CVictorySelectWindow : Control
{
	private CGuiContext _context = null!;
	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private CGameState? _state;
	private CCountry? _player;
	private readonly List<(CGuiButton Box, CVictoryCondition Condition)> _rows = new();

	public void Setup(CGuiContext context)
	{
		_context = context;
	}

	public bool IsOpen => _window is { Visible: true };

	public override void _Ready()
	{
		Name = "VictorySelect";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, CVictoryWindowText.SelectWindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_list = _window.FindListBox("conditions_list");
		if (_window.TryFindButton("accept_button") is { } accept)
		{
			accept.Pressed += Accept;
			accept.Tooltip = () => new CTooltip
			{
				Text = _state is null ? "" : CVictoryWindowText.AcceptTooltip(_context.Localisation.Localise, Ticked(), CVictoryConditions.Number(_state)),
			};
		}
		if (_window.TryFindButton("cancel_button") is { } cancel)
		{
			cancel.Pressed += () => _window.Visible = false;
		}
		_window.Visible = false;
	}

	/// <summary>Opens over a world and its player, the ticks made again from what is chosen.</summary>
	public void Open(CGameState state, CCountry player)
	{
		if (_window is null || _list is null)
		{
			return;
		}
		_state = state;
		_player = player;
		// The lobby's world has made no choice yet; what a new game would choose is what shows ticked.
		CVictoryConditions.ChooseAtStart(state);
		Func<string, string> localise = _context.Localisation.Localise;
		if (_window.FindTextBox("vclobby_label") is { } header)
		{
			header.Text = CVictoryWindowText.SelectHeader(localise, player.Faction);
		}
		_list.Clear();
		_rows.Clear();
		foreach (CVictoryCondition condition in CVictoryWindowText.AllOf(state, player.Faction))
		{
			if (CGuiWindow.CreateEntry(_context, CVictoryWindowText.SelectRowName) is not { } row || row.TryFindButton("active") is not { } box)
			{
				continue;
			}
			box.Checked = condition.Chosen;
			if (row.FindTextBox("name") is { } name)
			{
				name.Text = localise(condition.Name);
			}
			CVictoryCondition shown = condition;
			row.SetTooltips(_ => new CTooltip { Text = CVictoryWindowText.RowTooltip(new CScriptWords(localise, state, player.Tag), shown) }, "name");
			_rows.Add((box, condition));
			_list.Add(row);
		}
		_window.Visible = true;
		ShowCount();
	}

	public void Shut()
	{
		if (_window is not null)
		{
			_window.Visible = false;
		}
	}

	private int Ticked() => _rows.Count(row => row.Box.Checked);

	public override void _Process(double delta)
	{
		if (IsOpen)
		{
			ShowCount();
		}
	}

	private void ShowCount()
	{
		if (_state is not null && _window?.FindTextBox("info") is { } info)
		{
			info.Text = CVictoryWindowText.SelectInfo(Ticked(), CVictoryConditions.Number(_state));
		}
	}

	/// <summary>
	/// The ticks fitted and kept. *Ours*: on a generator of the moment as the original's is, the world taking the names that came of it.
	/// </summary>
	private void Accept()
	{
		if (_state is null || _player is null || _window is null)
		{
			return;
		}
		var random = new Random();
		IEnumerable<string> ticked = _rows.Where(row => row.Box.Checked).Select(row => row.Condition.Name);
		List<string> all = CVictoryWindowText.AllOf(_state, _player.Faction).ConvertAll(condition => condition.Name);
		CVictoryConditions.SetChosen(_state, _player.Faction, CVictoryWindowText.Fit(ticked, all, CVictoryConditions.Number(_state), random.Next));
		_window.Visible = false;
	}
}
