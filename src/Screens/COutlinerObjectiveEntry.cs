using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// An allied objective's row in the outliner: an <c>outliner_allied_objective_entry</c> window with the province in
/// <c>entry_text</c>, red where it was asked of the player and green where the player asked it. See
/// <c>docs/mechanics/outliner.md</c>, section 7.
/// </summary>
public sealed class COutlinerObjectiveEntry
{
	/// <summary>Raised when the row is pressed.</summary>
	public event Action? Chosen;

	private COutlinerObjectiveEntry(CGuiContext context, CGuiWindow window, CGameState state, CCountryTag player, int province,
		bool askedOfPlayer)
	{
		Window = window;
		if (window.FindTextBox("entry_text") is { } text)
		{
			text.Text = COutlinerText.ObjectiveText(state, province, askedOfPlayer);
		}
		window.SetTooltips(name => name == "entry_text"
			? COutlinerTooltips.Objective(context.Localisation.Localise, state, player, province, askedOfPlayer)
			: Interface.Tooltips.CTooltip.Empty, "entry_text");
		if (window.FindButton("outliner_standard") is { } row)
		{
			row.Pressed += () => Chosen?.Invoke();
		}
	}

	public CGuiWindow Window { get; }

	public static COutlinerObjectiveEntry? Create(CGuiContext context, CGameState state, CCountryTag player, int province,
		bool askedOfPlayer)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "outliner_allied_objective_entry");
		return window is null ? null : new COutlinerObjectiveEntry(context, window, state, player, province, askedOfPlayer);
	}
}
