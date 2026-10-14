using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the choice of a law, a <c>possible_law_entry</c>: the law's name with its price, what it does, which of the
/// two indicators it wears - the current law's or another's - and the button, which may be pressed only where the law is
/// allowed, the money is there and it is not the law in force, and says why not. See
/// <c>docs/mechanics/politics.md</c>, section 11.
/// </summary>
public sealed class CPossibleLawEntry
{
	public const string WindowName = "possible_law_entry";

	private CPossibleLawEntry(CGuiWindow window, Func<string, string> localise, CGameState state, CCountry country, CLaw law,
		Func<CLaw, string> allowText)
	{
		Window = window;
		Law = law;
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = CPoliticsScreenText.PossibleLawName(localise, state, country, law);
		}
		if (window.FindTextBox("effect") is { } effect)
		{
			effect.Text = CModifierText.AllValues(localise, law.Modifier);
		}
		bool current = CPoliticsScreenText.IsCurrent(country, law);
		if (window.TryFindIcon(current ? "possible_indicator" : "current_indicator") is { } other)
		{
			other.Visible = false;
		}
		if (window.FindButton("select_button") is { } select)
		{
			select.Disabled = !CPoliticsScreenText.MayChoose(state, country, law);
			select.Pressed += () => SelectPressed?.Invoke(Law);
			select.Tooltip = () => new CTooltip { Text = CPoliticsTooltips.LawChoice(localise, state, country, law, allowText) };
		}
	}

	public CGuiWindow Window { get; }

	public CLaw Law { get; }

	public event Action<CLaw>? SelectPressed;

	public static CPossibleLawEntry? Create(CGuiContext context, CGameState state, CCountry country, CLaw law,
		Func<CLaw, string> allowText)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CPossibleLawEntry(window, context.Localisation.Localise, state, country, law, allowText);
	}
}
