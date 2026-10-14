using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the politics screen's laws, a <c>current_law_entry</c>: the group's name, the country's law in it, and the
/// button that opens the choice of a law; the law's name and the button say what the law does. See
/// <c>docs/mechanics/politics.md</c>, section 11.
/// </summary>
public sealed class CLawEntry
{
	public const string WindowName = "current_law_entry";

	private CLawEntry(CGuiWindow window, Func<string, string> localise, CCountry country, CLawGroup group, CLaw law)
	{
		Window = window;
		Group = group;
		Law = law;
		if (window.FindTextBox("category") is { } category)
		{
			category.Text = localise(group.Name);
		}
		Func<CTooltip> effect = () => new CTooltip { Text = CModifierText.AllValues(localise, law.Modifier) };
		if (window.FindTextBox("which") is { } which)
		{
			which.Text = CPoliticsScreenText.LawName(localise, law, country);
			which.PassiveTooltip = effect;
		}
		if (window.FindButton("select_button") is { } select)
		{
			select.Pressed += () => SelectPressed?.Invoke(Group);
			select.Tooltip = effect;
		}
	}

	public CGuiWindow Window { get; }

	public CLawGroup Group { get; }

	/// <summary>The law the row was made for: when the country's law of the group is another, the list is made again.</summary>
	public CLaw Law { get; }

	public event Action<CLawGroup>? SelectPressed;

	public static CLawEntry? Create(CGuiContext context, CCountry country, CLawGroup group, CLaw law)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CLawEntry(window, context.Localisation.Localise, country, group, law);
	}
}
