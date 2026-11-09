using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One of a headquarters' three rows saying the stance the AI over it holds: <c>sup_unit_stance</c>. Its button opens the list of
/// stances over the row. See <c>docs/mechanics/unitpanel.md</c>, section 9.
/// </summary>
public sealed class CUnitStanceEntry : CUnitPanelEntry
{
	private const string RowName = "sup_stance_change";

	private readonly CGuiContext _context;
	private readonly CGuiWindow _changeWindow;
	private readonly Action<CCommand> _post;
	private readonly CUnit _unit;
	private readonly EPlanStanceKind _kind;
	private readonly CGuiButton? _button;

	private CUnitStanceEntry(CGuiWindow window, CGuiContext context, CGuiWindow changeWindow, CUnit unit, EPlanStanceKind kind, Action<CCommand> post)
		: base(window)
	{
		_context = context;
		_changeWindow = changeWindow;
		_post = post;
		_unit = unit;
		_kind = kind;
		_button = window.FindButton("stance");
		if (_button is not null)
		{
			_button.Pressed += OpenList;
		}
		AnswerFor(_ => new CTooltip { Text = Word("_DESC") });
		Update();
	}

	public static CUnitStanceEntry? Create(CGuiContext context, CGuiWindow changeWindow, CUnit unit, EPlanStanceKind kind, Action<CCommand> post)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_unit_stance");
		return window is null ? null : new CUnitStanceEntry(window, context, changeWindow, unit, kind, post);
	}

	private string Word(string suffix)
	{
		string key = CPlanRowsText.StanceKey(_kind, CPlanRowsText.StanceOf(_unit.Plan, _kind));
		return key.Length == 0 ? "" : _context.Localisation.Localise(key + suffix);
	}

	public override void Update()
	{
		if (_button is not null)
		{
			_button.SetText(Word(""));
			_button.Disabled = CPlanRowsText.AgentAboveItsOwn(_unit);
		}
	}

	private void OpenList()
	{
		if (_changeWindow.FindListBox("list") is not { } list)
		{
			return;
		}
		list.Clear();
		for (int stance = 0; stance < CPlanRowsText.StanceCount(_kind); stance++)
		{
			if (CGuiWindow.CreateEntry(_context, RowName) is not { } row || row.FindButton("stance") is not { } choice)
			{
				continue;
			}
			choice.SetText(_context.Localisation.Localise(CPlanRowsText.StanceKey(_kind, stance)));
			int picked = stance;
			choice.Pressed += () => Pick(picked);
			list.Add(row);
		}
		_changeWindow.GlobalPosition = Window.GlobalPosition;
		_changeWindow.Visible = true;
	}

	private void Pick(int stance)
	{
		_post(_kind switch
		{
			EPlanStanceKind.Air => new CSetPlanCommand(_unit.Owner, _unit.Id, CSetPlanCommand.Unchanged, stance, CSetPlanCommand.UnchangedAirOrNaval),
			EPlanStanceKind.Naval => new CSetPlanCommand(_unit.Owner, _unit.Id, CSetPlanCommand.Unchanged, CSetPlanCommand.UnchangedAirOrNaval, stance),
			_ => new CSetAgentStanceCommand(_unit.Owner, _unit.Id, stance),
		});
		_changeWindow.Visible = false;
	}
}
