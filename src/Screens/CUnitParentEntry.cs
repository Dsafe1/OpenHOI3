using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A unit above the selected one, at the top of its panel: a
/// <c>sup_parent_unit_entry</c> window whose one button carries the unit's name and
/// selects it. See <c>docs/mechanics/unitpanel.md</c>, section 2.
/// </summary>
public sealed class CUnitParentEntry : CUnitPanelEntry
{
	/// <summary>Raised when the button is pressed.</summary>
	public event Action? Selected;

	private CUnitParentEntry(CGuiWindow window, CGuiContext context, CUnit unit) : base(window)
	{
		AnswerFor(_ => CUnitOrderTooltips.Parent(context.Localisation.Localise));
		if (window.FindButton("unit") is { } button)
		{
			// Set once: the original's has no update either.
			button.SetText(unit.Name);
			button.Pressed += () => Selected?.Invoke();
		}
	}

	public static CUnitParentEntry? Create(CGuiContext context, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_parent_unit_entry");
		return window is null ? null : new CUnitParentEntry(window, context, unit);
	}
}
