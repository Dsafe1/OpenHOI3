using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// A headquarters' row for the AI that may command it: the <c>sup_objectives</c> window.
/// With no AI in command it shows the box that hands the headquarters to one, and how
/// aggressive it is to be - a slider from one end of a red and blue bar to the other, and
/// the box that makes the setting the headquarters' own. There is no AI yet, so the row is
/// shown as the original shows it for a headquarters under the player's hand, and none of
/// it can be pressed. See <c>docs/mechanics/unitpanel.md</c>, section 9.
/// </summary>
public sealed class CUnitObjectivesEntry : CUnitPanelEntry
{
	private CUnitObjectivesEntry(CGuiWindow window) : base(window)
	{
		// Under the player's hand: the box is clear, and what an AI in command would show
		// in its place - its objectives, and the box that lets it reorganise - is hidden.
		if (window.FindButton("automate_checkbox") is { } automate)
		{
			automate.Checked = false;
			automate.Disabled = true;
		}
		Show(window.FindButton("reorg_hq_checkbox"), false);
		Show(window.FindTextBox("objectives"), false);

		Show(window.FindIcon("tac_aggro_bg"), true);
		Show(window.FindIcon("tac_aggro_slider_bg"), true);
		// A headquarters with no setting of its own shows the one it is under, and its
		// slider cannot be moved until the box beside it is ticked. Nothing holds a
		// setting yet: the slider stands in the middle.
		if (window.FindButton("aggression_checkbox") is { } own)
		{
			own.Checked = false;
			own.Disabled = true;
		}
		if (window.FindScrollbar("aggression_slider") is { } slider)
		{
			slider.Value = CFixedPoint.Zero;
			slider.MouseFilter = Control.MouseFilterEnum.Ignore;
		}
	}

	public static CUnitObjectivesEntry? Create(CGuiContext context)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_objectives");
		return window is null ? null : new CUnitObjectivesEntry(window);
	}
}
