using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One unit aboard a fleet or an air unit: a <c>loadedunit_entry</c> window with the picture of its kind and the mark
/// of its size, its name without its leader, what it is doing, and its organisation and strength as bars - no figure of its men. A click
/// selects it. See <c>docs/mechanics/transport.md</c>, section 12.
/// </summary>
public sealed class CLoadedUnitRow : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _owner;

	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _order;
	private readonly CGuiIcon? _size;
	private readonly CGuiIcon? _organisationBar;
	private readonly CGuiIcon? _strengthBar;

	/// <summary>Raised when the row's button is pressed.</summary>
	public event Action? Selected;

	private CLoadedUnitRow(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CUnit unit)
		: base(window)
	{
		_context = context;
		_state = state;
		_owner = owner;
		Unit = unit;

		_name = window.FindTextBox("name");
		_order = window.FindTextBox("order");
		_size = window.FindIcon("counter_size");
		_organisationBar = window.FindIcon("org_bar");
		_strengthBar = window.FindIcon("str_bar");

		string sprite = CDeployMenuText.CounterSprite(unit);
		if (sprite.Length != 0)
		{
			window.FindIcon("counter_type")?.SetSprite(sprite);
		}
		// An objective is an AI's to have, and there is none.
		Set(window.FindTextBox("objective"), "");
		if (window.FindButton("select") is { } select)
		{
			select.Pressed += () => Selected?.Invoke();
		}
		AnswerFor(name => CUnitHeaderTooltips.ChildRow(context.Localisation.Localise, owner, unit, name), "order", "objective",
			"org_bar", "str_bar");
		Update();
	}

	public CUnit Unit { get; }

	public static CLoadedUnitRow? Create(CGuiContext context, CGameState state, CCountry owner, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "loadedunit_entry");
		return window is null ? null : new CLoadedUnitRow(window, context, state, owner, unit);
	}

	/// <summary>Each frame the row finds its unit again and sets what it says.</summary>
	public override void Update()
	{
		// Its name alone: unlike a child's row, no leader is written after it.
		Set(_name, Unit.Name);
		Set(_order, CUnitPanelText.Order(_context.Localisation.Localise, _state, Unit));
		_size?.SetFrame(CDeployMenuText.SizeFrame(Unit));
		_organisationBar?.SetProgress(CUnitPanelText.OrganisationBar(_owner, Unit));
		_strengthBar?.SetProgress(CUnitPanelText.StrengthBar(Unit));
	}
}
