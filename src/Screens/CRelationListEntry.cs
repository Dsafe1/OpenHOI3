using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// One row of the diplomacy screen's relation list, a <c>domesticpanel_relation_entry</c>: the other
/// country's flag and name, and the chosen country's relation with it in a colour from red to green.
/// Its tooltip is the year's change, the breakdown delayed. See
/// <c>docs/mechanics/diplomacyscreen.md</c>, section 5.3.
/// </summary>
public sealed class CRelationListEntry
{
	public const string WindowName = "domesticpanel_relation_entry";

	private CRelationListEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry chosen, CCountry other,
		long value)
	{
		Window = window;
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = CDiplomacyScreenText.RelationName(other);
		}
		if (window.FindTextBox("value") is { } box)
		{
			box.Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
			box.Colour = CDiplomacyScreenText.RelationColour(value).ToGodot();
		}
		window.SetTooltips(_ => CDiplomacyTooltips.Relation(context.Localisation.Localise, state, chosen, other), "name", "value");
	}

	public CGuiWindow Window { get; }

	public static CRelationListEntry? Create(CGuiContext context, CGameState state, CCountry chosen, CCountry other, long value)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CRelationListEntry(window, context, state, chosen, other, value);
	}
}
