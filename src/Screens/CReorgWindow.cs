using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The reorganise window, <c>reorg_window</c>: the first selected unit's brigades on the left and the second's on the
/// right, each row's arrow sending its brigade across. Each frame it writes the two names, shuts where the units part or
/// the selection is no longer those two, and makes a list again where its rows no longer match the unit's brigades in
/// number, keeping the rows still in place. Shut, it disbands a unit left empty. See <c>docs/mechanics/reorganise.md</c>.
/// </summary>
public partial class CReorgWindow : Control
{
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;

	private CGuiWindow? _window;
	private CGuiListBox? _leftList;
	private CGuiListBox? _rightList;
	private readonly List<CReorgEntry> _leftRows = new();
	private readonly List<CReorgEntry> _rightRows = new();
	private CUnit? _left;
	private CUnit? _right;

	/// <summary>The units selected on the map: the window shuts when they are no longer its two.</summary>
	public Func<IReadOnlyList<CUnit>>? Selection { get; set; }

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public bool IsOpen => _window is not null;

	public override void _Ready()
	{
		Name = "reorganise";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Brings the window up for two units, the first on the left, in place of any that is up.</summary>
	public void Open(CUnit left, CUnit right)
	{
		Close();
		if (CGuiWindow.Create(_context, CReorgText.WindowName) is not { } window)
		{
			return;
		}
		_window = window;
		_left = left;
		_right = right;
		AddChild(window);
		_leftList = window.FindListBox(CReorgText.LeftList);
		_rightList = window.FindListBox(CReorgText.RightList);
		if (window.FindButton("closebutton") is { } close)
		{
			close.Pressed += Close;
		}
		Func<string, string> localise = _context.Localisation.Localise;
		window.SetTooltips(element => _left is null || _right is null
				? CTooltip.Empty
				: CReorgTooltips.Window(localise, _left, _right, element),
			CReorgText.PositioningLeft, CReorgText.PositioningRight, CReorgText.RatioLeft, CReorgText.RatioRight,
			CReorgText.LeftName, CReorgText.RightName);
		Fill();
	}

	private void ShowFleetLine(string name, bool shown, Func<string> text)
	{
		if (_window?.FindTextBox(name) is not { } box)
		{
			return;
		}
		box.Visible = shown;
		if (shown)
		{
			box.Text = text();
		}
	}

	/// <summary>Hides the window and disbands each of its units left with no brigades (reorganise.md, section 5).</summary>
	public void Close()
	{
		if (_window is null)
		{
			return;
		}
		foreach (CUnit? unit in new[] { _left, _right })
		{
			if (unit is { IsRemoved: false, SubUnits.Count: 0 } && unit.Owner == _player.Tag)
			{
				_simulation.Post(new CDisbandCommand(unit.Id));
			}
			else if (unit is { IsRemoved: false, IsAllowedEmpty: true })
			{
				_simulation.Post(new CClearAllowedEmptyCommand(unit.Owner, unit.Id));
			}
		}
		_window.QueueFree();
		_window = null;
		_leftList = null;
		_rightList = null;
		_leftRows.Clear();
		_rightRows.Clear();
		_left = null;
		_right = null;
	}

	public override void _Process(double delta)
	{
		if (_window is null || _left is null || _right is null)
		{
			return;
		}
		// Shut where the two part, or are no longer the selection (ours, for the original's change of rows).
		IReadOnlyList<CUnit> selected = Selection?.Invoke() ?? Array.Empty<CUnit>();
		if (_left.IsRemoved || _right.IsRemoved || _left.Location != _right.Location
			|| selected.Count != 2 || !selected.Contains(_left) || !selected.Contains(_right))
		{
			Close();
			return;
		}
		SetText(CReorgText.LeftName, _left.Name);
		SetText(CReorgText.RightName, _right.Name);
		if (_leftRows.Count != _left.SubUnits.Count || _rightRows.Count != _right.SubUnits.Count)
		{
			Fill();
		}
	}

	private void SetText(string name, string text)
	{
		if (_window?.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}

	/// <summary>Both lists and a fleet's four lines, as the window opens and each time a list is made again.</summary>
	private void Fill()
	{
		CUnit left = _left!, right = _right!;
		Sync(_leftList, _leftRows, left, right, CReorgText.LeftEntry);
		Sync(_rightList, _rightRows, right, left, CReorgText.RightEntry);

		// A fleet's lines; hidden where neither unit is a fleet.
		Func<string, string> localise = _context.Localisation.Localise;
		bool fleets = CReorgText.ShowsFleetLines(left, right);
		CGameState state = _simulation.State;
		ShowFleetLine(CReorgText.PositioningLeft, fleets, () => CReorgText.Positioning(localise, state, left));
		ShowFleetLine(CReorgText.PositioningRight, fleets, () => CReorgText.Positioning(localise, state, right));
		ShowFleetLine(CReorgText.RatioLeft, fleets, () => CReorgText.Ratio(localise, left));
		ShowFleetLine(CReorgText.RatioRight, fleets, () => CReorgText.Ratio(localise, right));
	}

	/// <summary>
	/// Makes one list again: a row for each of the unit's brigades in its order - the row there already, as it was, where
	/// its brigade still is - and the rows of brigades gone freed.
	/// </summary>
	private void Sync(CGuiListBox? list, List<CReorgEntry> rows, CUnit unit, CUnit target, string template)
	{
		if (list is null)
		{
			return;
		}
		List<Control> old = list.Detach();
		var kept = new List<CReorgEntry>();
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			CReorgEntry? row = rows.FirstOrDefault(entry => ReferenceEquals(entry.SubUnit, subUnit)
				&& ReferenceEquals(entry.Target, target) && !kept.Contains(entry));
			if (row is null)
			{
				row = CReorgEntry.Create(_context, _simulation.State, _player, template, subUnit, target);
				if (row is null)
				{
					continue;
				}
				row.TransferPressed += Transfer;
			}
			kept.Add(row);
			list.Add(row.Window);
		}
		foreach (Control entry in old)
		{
			if (!kept.Exists(row => ReferenceEquals(row.Window, entry)))
			{
				entry.QueueFree();
			}
		}
		rows.Clear();
		rows.AddRange(kept);
	}

	/// <summary>A row's arrow: the brigade sent to the other unit, which the move may refuse in silence (section 4).</summary>
	private void Transfer(CReorgEntry row)
	{
		var command = new CTransferSubUnitCommand(_player.Tag, row.SubUnit.Id, row.Target.Id);
		if (command.MayExecute(_simulation.State))
		{
			_simulation.Post(command);
		}
	}
}
