using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One unit directly under a selected one: a <c>childunit_entry</c> window with its
/// name, the picture of its kind and the mark of its size, its men, what it is doing,
/// and its organisation and strength as bars. A click selects it. See
/// <c>docs/mechanics/unitpanel.md</c>, section 8.
/// </summary>
public sealed class CUnitChildRow : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _owner;

	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _men;
	private readonly CGuiTextBox? _order;
	private readonly CGuiIcon? _size;
	private readonly CGuiIcon? _organisationBar;
	private readonly CGuiIcon? _strengthBar;

	/// <summary>Raised when the row's button is pressed.</summary>
	public event Action? Selected;

	private CUnitChildRow(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CUnit unit)
		: base(window)
	{
		_context = context;
		_state = state;
		_owner = owner;
		Unit = unit;

		_name = window.FindTextBox("name");
		_men = window.FindTextBox("str");
		_order = window.FindTextBox("order");
		_size = window.FindIcon("counter_size");
		_organisationBar = window.FindIcon("org_bar");
		_strengthBar = window.FindIcon("str_bar");

		string sprite = CDeployMenuText.CounterSprite(unit);
		if (sprite.Length != 0)
		{
			window.FindIcon("counter_type")?.SetSprite(sprite);
		}
		if (unit.SubUnits.Count > 0 && unit.SubUnits[0].IsPride)
		{
			window.FindIcon("unit_pride_star")?.SetFrame(3);
		}
		// An objective is an AI's to have, and there is none.
		Set(window.FindTextBox("objective"), "");
		if (window.FindButton("select") is { } select)
		{
			select.Pressed += () => Selected?.Invoke();
		}
		Update();
	}

	public CUnit Unit { get; }

	public static CUnitChildRow? Create(CGuiContext context, CGameState state, CCountry owner, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "childunit_entry");
		return window is null ? null : new CUnitChildRow(window, context, state, owner, unit);
	}

	public override void Update()
	{
		// The original adds the leader's name in brackets; no unit has a leader yet.
		Set(_name, Unit.Name);
		Set(_men, CUnitPanelText.ChildMen(Unit));
		Set(_order, CUnitPanelText.Order(_context.Localisation.Localise, _state, Unit));
		_size?.SetFrame(CDeployMenuText.SizeFrame(Unit));
		_organisationBar?.SetProgress(CUnitPanelText.OrganisationBar(_owner, Unit));
		_strengthBar?.SetProgress(CUnitPanelText.StrengthBar(Unit));
	}
}
