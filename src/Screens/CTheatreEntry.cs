using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the theatres screen's two lists, a <c>theatre_entry</c>: its headquarters' name and owner and a mark for how its plan's power stands, all
/// fixed when the row is made. Only the row's background answers the mouse. See <c>docs/mechanics/theatresscreen.md</c>, section 2.
/// </summary>
public sealed class CTheatreEntry
{
	private CTheatreEntry(CGuiWindow window, CGameState state, CTheatre theatre, CUnit headquarters, Action<CTheatre> chosen)
	{
		Window = window;
		Theatre = theatre;
		if (window.FindTextBox("title") is { } title)
		{
			title.Text = headquarters.Name;
		}
		if (state.Country(headquarters.Owner) is { } owner)
		{
			window.TryFindButton("country_icon")?.SetCountry(owner);
		}
		window.TryFindIcon("theatre_plupp")?.SetFrame(CTheatreScreenText.RatioFrame(state, headquarters));
		if (window.TryFindButton("background_button") is { } background)
		{
			background.Pressed += () => chosen(theatre);
		}
	}

	public CGuiWindow Window { get; }

	public CTheatre Theatre { get; }

	public static CTheatreEntry? Create(CGuiContext context, CGameState state, CTheatre theatre, CUnit headquarters, Action<CTheatre> chosen)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CTheatreScreenText.TheatreRowName);
		return window is null ? null : new CTheatreEntry(window, state, theatre, headquarters, chosen);
	}
}
