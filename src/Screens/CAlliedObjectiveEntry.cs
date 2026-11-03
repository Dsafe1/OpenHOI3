using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// An ally in the province view's allied-objectives panel, an <c>allied_coop_item</c>: the box <c>objective</c>, ticked
/// where the player has asked the ally to take the province, its flag and its name. Ticking or clearing the box raises
/// <see cref="Changed"/>. See <c>docs/mechanics/provinceview.md</c>, section 6.
/// </summary>
public sealed class CAlliedObjectiveEntry
{
	private readonly CGuiButton? _box;

	private CAlliedObjectiveEntry(CGuiWindow window, Func<string, string> localise)
	{
		Window = window;
		_box = window.FindButton("objective");
		if (_box is not null)
		{
			_box.Pressed += () =>
			{
				if (Ally is not null)
				{
					Changed?.Invoke(Ally, _box.Checked);
				}
			};
		}
		if (window.TryFindIcon("flag") is { } flag)
		{
			flag.Tooltip = () => Ally is null ? CTooltip.Empty : CProvinceViewTooltips.Flag(localise, Ally, "flag");
		}
	}

	public CGuiWindow Window { get; }

	public CCountry? Ally { get; private set; }

	/// <summary>The ally, and whether its box is now ticked.</summary>
	public event Action<CCountry, bool>? Changed;

	public static CAlliedObjectiveEntry? Create(CGuiContext context)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "allied_coop_item");
		return window is null ? null : new CAlliedObjectiveEntry(window, context.Localisation.Localise);
	}

	/// <summary>The ally and whether it is asked; its box may not be pressed where the row is not enabled - a replay.</summary>
	public void Show(CCountry ally, bool asked, bool enabled = true)
	{
		if (_box is not null)
		{
			_box.Disabled = !enabled;
		}
		Ally = ally;
		Window.TryFindIcon("flag")?.SetCountry(ally);
		if (Window.FindTextBox("country_name") is { } name)
		{
			name.Text = ally.Name;
		}
		if (_box is not null)
		{
			_box.Checked = asked;
		}
	}
}
