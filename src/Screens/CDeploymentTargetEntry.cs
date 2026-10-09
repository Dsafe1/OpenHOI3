using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A line of the deploy menu for a unit in the field the one being placed may join: a
/// <c>deployment_target_entry</c> window with that unit's name, where it is, the
/// picture of its kind and the mark of its size, and a button that joins it. See
/// <c>docs/mechanics/mapunits.md</c>, section 6.
/// </summary>
public sealed class CDeploymentTargetEntry
{
	/// <summary>Raised when the line's button is pressed.</summary>
	public event Action? Selected;

	private CDeploymentTargetEntry(CGuiWindow window, CGameState state, CUnit target)
	{
		Window = window;
		Target = target;

		if (window.FindTextBox("name") is { } name)
		{
			name.Text = target.Name;
		}
		if (window.FindTextBox("location") is { } location)
		{
			location.Text = CDeployMenuText.Location(state, target);
		}
		CDeploymentEntry.Fill(window, target);
		if (window.FindButton("select") is { } select)
		{
			select.Pressed += () => Selected?.Invoke();
		}
	}

	public CGuiWindow Window { get; }

	public CUnit Target { get; }

	public static CDeploymentTargetEntry? Create(CGuiContext context, CGameState state, CUnit target)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "deployment_target_entry");
		return window is null ? null : new CDeploymentTargetEntry(window, state, target);
	}
}
