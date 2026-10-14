using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The Strategic Effects window, <c>modifier_window</c>: a row for every triggered modifier the player's country might
/// have, in file order, filled as it opens and again once a game day while it is open. The bottom bar's
/// <c>menubar_modifiers_button</c> opens and shuts it, its <c>close_button</c> shuts it. See
/// <c>docs/mechanics/strategiceffects.md</c>.
/// </summary>
public sealed partial class CStrategicEffectsView : Control
{
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;
	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private readonly List<CStrategicEffectsEntry> _rows = new();
	private CDate? _filledOn;

	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public bool IsOpen => _window is { Visible: true };

	public override void _Ready()
	{
		Name = "StrategicEffects";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, CStrategicEffectsText.WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_list = _window.FindListBox("modifier_list");
		if (_window.TryFindButton("close_button") is { } close)
		{
			close.Pressed += () => SetOpen(false);
		}
		_window.Visible = false;
	}

	/// <summary>Opens it where it is shut, filled at once, and shuts it where it is open.</summary>
	public void Toggle()
	{
		SetOpen(!IsOpen);
	}

	public void SetOpen(bool open)
	{
		if (_window is null)
		{
			return;
		}
		_window.Visible = open;
		if (open)
		{
			Fill();
		}
	}

	/// <summary>Once a game day while it is open.</summary>
	public override void _Process(double delta)
	{
		if (IsOpen && _filledOn != _simulation.State.Date)
		{
			Fill();
		}
	}

	private void Fill()
	{
		CGameState state = _simulation.State;
		_filledOn = state.Date;
		if (_list is null)
		{
			return;
		}
		List<CStrategicEffectRow> rows = CStrategicEffectsText.Rows(state, _player);
		// The rows are made again only where their number has changed.
		if (rows.Count != _rows.Count)
		{
			_list.Clear();
			_rows.Clear();
			for (int i = 0; i < rows.Count; i++)
			{
				if (CStrategicEffectsEntry.Create(_context, Words, _player) is not { } row)
				{
					return;
				}
				_rows.Add(row);
				_list.Add(row.Window);
			}
		}
		for (int i = 0; i < rows.Count; i++)
		{
			_rows[i].Show(rows[i]);
		}
	}

	private CScriptWords Words() => new(_context.Localisation.Localise, _simulation.State, _player.Tag);
}
