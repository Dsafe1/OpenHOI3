using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A headquarters' row for the AI that may command it: the <c>sup_objectives</c> window. Under the player's hand it shows the box
/// that hands the headquarters to an AI and how aggressive it is to be; under an AI, what the AI has been told to take and whether it
/// may reorganise the headquarters. See <c>docs/mechanics/unitpanel.md</c>, section 9.
/// </summary>
public sealed class CUnitObjectivesEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CUnit _unit;
	private readonly CGuiButton? _automate;
	private readonly CGuiButton? _reorganise;
	private readonly CGuiTextBox? _objectives;
	private readonly CGuiIcon? _bar;
	private readonly CGuiIcon? _barBack;
	private readonly CGuiButton? _own;
	private readonly CGuiScrollbar? _slider;
	private bool _wasOwn;

	private CUnitObjectivesEntry(CGuiWindow window, CGuiContext context, CGameState state, CUnit unit, Action<CCommand> post) : base(window)
	{
		_context = context;
		_state = state;
		_unit = unit;
		_automate = window.FindButton("automate_checkbox");
		_reorganise = window.FindButton("reorg_hq_checkbox");
		_objectives = window.FindTextBox("objectives");
		_bar = window.FindIcon("tac_aggro_bg");
		_barBack = window.FindIcon("tac_aggro_slider_bg");
		_own = window.FindButton("aggression_checkbox");
		_slider = window.FindScrollbar("aggression_slider");
		if (_automate is not null)
		{
			_automate.Pressed += () => post(new CAutomateUnitCommand(unit.Owner, unit.Id));
		}
		if (_reorganise is not null)
		{
			_reorganise.Pressed += () => post(new CActivatePlanCommand(unit.Owner, unit.Id, active: true, !unit.Plan.AllowHqReorg));
		}
		if (_own is not null)
		{
			_own.Pressed += () => post(new CToggleUnitAggressionCommand(unit.Owner, unit.Id, !unit.HasOwnAggression));
		}
		if (_slider is not null)
		{
			_slider.Moved += value => post(new CSetUnitAggressionCommand(unit.Owner, unit.Id, value.Raw));
		}
		_wasOwn = !unit.HasOwnAggression;
		Update();
	}

	public static CUnitObjectivesEntry? Create(CGuiContext context, CGameState state, CUnit unit, Action<CCommand> post)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_objectives");
		if (window is null)
		{
			return null;
		}
		var entry = new CUnitObjectivesEntry(window, context, state, unit, post);
		entry.AnswerFor(name => CUnitOrderTooltips.Objectives(context.Localisation.Localise, state, unit, name));
		return entry;
	}

	public override void Update()
	{
		bool underAi = CAgents.HighestAutomated(_unit) is not null;
		if (_automate is not null)
		{
			_automate.Checked = underAi;
			// A commander already under an AI holds it, and only a theatre's headquarters has an agent to hand it to here.
			_automate.Disabled = CPlanRowsText.AgentAboveItsOwn(_unit)
				|| (!underAi && (_state.Country(_unit.Owner) is not { } country || CAgents.TheatreHeadedBy(_state, country, _unit) is null));
		}
		Show(_reorganise, underAi);
		Show(_objectives, underAi);
		Show(_bar, !underAi);
		Show(_barBack, !underAi);
		Show(_own, !underAi);
		Show(_slider, !underAi);
		if (underAi)
		{
			if (_reorganise is not null)
			{
				_reorganise.Checked = _unit.Plan.AllowHqReorg;
			}
			Func<string, string> localise = _context.Localisation.Localise;
			string objectives = CTheatreScreenText.Objectives(localise, _state, _unit);
			Set(_objectives, objectives.Length > 0 ? localise("OBJECTIVES") + ": " + objectives : localise("NO_OBJECTIVES"));
			return;
		}
		if (_own is not null)
		{
			_own.Checked = _unit.HasOwnAggression;
		}
		if (_slider is null)
		{
			return;
		}
		_slider.MouseFilter = _unit.HasOwnAggression ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		// A free slider is left to the hand, but for when it is freed.
		if (!_unit.HasOwnAggression || !_wasOwn)
		{
			_slider.Value = new CFixedPoint(_unit.HasOwnAggression ? _unit.Aggression.Raw : CUnitAggression.Of(_unit));
		}
		_wasOwn = _unit.HasOwnAggression;
	}
}
