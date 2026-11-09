using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>A headquarters' two bars, its power beside the enemy's: <c>sup_power_ratios</c>. See <c>docs/mechanics/unitpanel.md</c>, section 9.</summary>
public sealed class CPowerRatioEntry : CUnitPanelEntry
{
	private readonly CUnit _unit;
	private readonly CGuiIcon? _ours;
	private readonly CGuiIcon? _theirs;

	private CPowerRatioEntry(CGuiWindow window, CUnit unit) : base(window)
	{
		_unit = unit;
		_ours = window.FindIcon("our_power_value");
		_theirs = window.FindIcon("enemy_power_value");
		Update();
	}

	public static CPowerRatioEntry? Create(CGuiContext context, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_power_ratios");
		return window is null ? null : new CPowerRatioEntry(window, unit);
	}

	public override void Update()
	{
		(int ours, int theirs) = CPlanRowsText.PowerBars(_unit.Plan.OurPower, _unit.Plan.TheirPower);
		_ours?.SetProgress(ours);
		_theirs?.SetProgress(theirs);
	}
}
