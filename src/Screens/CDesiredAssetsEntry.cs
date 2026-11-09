using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>What a headquarters' plan is short of: <c>sup_desired_assets</c>. See <c>docs/mechanics/unitpanel.md</c>, section 9.</summary>
public sealed class CDesiredAssetsEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CUnit _unit;
	private readonly CGuiTextBox? _text;

	private CDesiredAssetsEntry(CGuiWindow window, CGuiContext context, CGameState state, CUnit unit) : base(window)
	{
		_context = context;
		_state = state;
		_unit = unit;
		_text = window.FindTextBox("desired");
		AnswerFor(name => CPlanRowsText.DesiredTooltip(context.Localisation.Localise, state, unit, name), "desired");
		Update();
	}

	public static CDesiredAssetsEntry? Create(CGuiContext context, CGameState state, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_desired_assets");
		return window is null ? null : new CDesiredAssetsEntry(window, context, state, unit);
	}

	public override void Update()
	{
		Set(_text, CPlanRowsText.Desired(_context.Localisation.Localise, _state, _unit));
	}
}
