using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// What a selected unit is doing: the <c>sup_current_orders</c> window, with a line of
/// text and the button that calls the order off. See <c>docs/mechanics/unitpanel.md</c>,
/// section 5.
/// </summary>
public sealed class CUnitOrdersEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CUnit _unit;
	private readonly CGuiTextBox? _orders;

	/// <summary>Raised when the button that cancels the order is pressed.</summary>
	public event Action? CancelPressed;

	private CUnitOrdersEntry(CGuiWindow window, CGuiContext context, CGameState state, CUnit unit, bool mayCancel) : base(window)
	{
		_context = context;
		_state = state;
		_unit = unit;
		_orders = window.FindTextBox("orders");
		if (window.FindButton("cancel_order_button") is { } cancel)
		{
			cancel.Disabled = !mayCancel;
			cancel.Pressed += () => CancelPressed?.Invoke();
		}
		AnswerFor(name => CUnitOrderTooltips.Orders(context.Localisation.Localise, unit, name), "orders");
		Update();
	}

	public static CUnitOrdersEntry? Create(CGuiContext context, CGameState state, CUnit unit, bool mayCancel = true)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_current_orders");
		return window is null ? null : new CUnitOrdersEntry(window, context, state, unit, mayCancel);
	}

	public override void Update()
	{
		Set(_orders, CUnitPanelText.Order(_context.Localisation.Localise, _state, _unit));
	}
}
