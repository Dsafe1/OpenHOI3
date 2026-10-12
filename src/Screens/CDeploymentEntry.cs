using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A line of the deploy menu for a unit waiting to be placed: a
/// <c>deployment_entry</c> window with the unit's name, the picture of its kind, the
/// mark of its size, and a button that chooses it. See
/// <c>docs/mechanics/mapunits.md</c>, section 6.
/// </summary>
public sealed class CDeploymentEntry
{
	/// <summary>Raised when the line's button is pressed.</summary>
	public event Action? Selected;

	private CDeploymentEntry(CGuiWindow window, CUnit unit)
	{
		Window = window;
		Unit = unit;

		if (window.FindTextBox("name") is { } name)
		{
			name.Text = unit.Name;
		}
		Fill(window, unit);
		if (window.FindButton("select") is { } select)
		{
			select.Pressed += () => Selected?.Invoke();
		}
	}

	public CGuiWindow Window { get; }

	public CUnit Unit { get; }

	public static CDeploymentEntry? Create(CGuiContext context, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "deployment_entry");
		if (window is null)
		{
			return null;
		}
		// The line answers for all of itself, its name over its button too.
		window.SetTooltips(_ => CDeployMenuTooltips.Entry(context.Localisation.Localise, unit), "name");
		return new CDeploymentEntry(window, unit);
	}

	/// <summary>
	/// The two pictures both kinds of line carry: the unit's kind, left as the file has
	/// it where no sprite is declared for that kind, and its size.
	/// </summary>
	public static void Fill(CGuiWindow window, CUnit unit)
	{
		string sprite = CDeployMenuText.CounterSprite(unit);
		if (sprite.Length != 0)
		{
			window.FindIcon("counter_type")?.SetSprite(sprite);
		}
		window.FindIcon("counter_size")?.SetFrame(CDeployMenuText.SizeFrame(unit));
	}
}
