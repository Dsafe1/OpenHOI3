using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the production screen's <c>ai_need_queue</c>: a theatre, what its plan still wants built and a button that orders
/// it. See <c>docs/mechanics/theatresscreen.md</c>, section 8.
/// </summary>
public sealed class CAiNeedEntry
{
	private readonly CSimulation _simulation;
	private readonly CCountry _player;
	private readonly CTheatre _theatre;
	private readonly Func<string, string> _localise;
	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _needsLine;
	private readonly CGuiButton? _build;
	private readonly Random _draw = new();
	private int[]? _needs;
	private (long Hour, int Lines) _figured = (-1, -1);

	private CAiNeedEntry(CGuiWindow window, CGuiContext context, CSimulation simulation, CCountry player, CTheatre theatre)
	{
		Window = window;
		_simulation = simulation;
		_player = player;
		_theatre = theatre;
		_localise = context.Localisation.Localise;
		_name = window.FindTextBox("name");
		_needsLine = window.FindTextBox("needs");
		_build = window.FindButton("build");
		if (_needsLine is not null)
		{
			_needsLine.PassiveTooltip = () => new CTooltip { Text = CTheatreScreenText.QueueNeedsTooltip(_localise, _simulation.State, _needsLine.Text) };
		}
		if (_build is not null)
		{
			_build.Pressed += Build;
		}
	}

	public CGuiWindow Window { get; }

	public static CAiNeedEntry? Create(CGuiContext context, CSimulation simulation, CCountry player, CTheatre theatre)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "ai_need_entry");
		return window is null ? null : new CAiNeedEntry(window, context, simulation, player, theatre);
	}

	private uint Draw() => (uint)_draw.NextInt64(uint.MaxValue);

	private void Build()
	{
		CGameState state = _simulation.State;
		if (_needs is null || CTheatres.Headquarters(state, _theatre) is not { } headquarters)
		{
			return;
		}
		foreach (CCommand command in new CTheatreBuildSteps(state, _player, headquarters.Id, _needs, Draw).Run())
		{
			_simulation.Post(command);
		}
	}

	/// <summary>Worked out again as the hour turns or the queue's length changes; a row whose headquarters has gone writes nothing.</summary>
	public void Refresh()
	{
		CGameState state = _simulation.State;
		if (CTheatres.Headquarters(state, _theatre) is not { } headquarters)
		{
			return;
		}
		(long, int) now = (state.TotalHours, _player.Constructions.Count);
		if (now == _figured)
		{
			return;
		}
		_figured = now;
		_needs = CTheatreScreenText.Needs(state, _player, headquarters, null);
		if (_name is not null)
		{
			_name.Text = headquarters.Name;
		}
		if (_needsLine is not null)
		{
			_needsLine.Text = CTheatreScreenText.QueueNeedsText(_localise, state, _needs);
		}
		if (_build is not null)
		{
			_build.Disabled = _needs is null;
		}
	}
}
