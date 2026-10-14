using System;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the intelligence screen's cabinet, a <c>foreign_minister_entry</c>: the post, the minister's name, his
/// personality, his party's colour and his portrait, or <c>???</c> for an empty post - shown whatever the spies know. Its
/// colour says his party, the rest what his personality does. See <c>docs/mechanics/espionagescreen.md</c>, section 5.
/// </summary>
public sealed class CForeignMinisterEntry
{
	public const string WindowName = "foreign_minister_entry";

	private CForeignMinisterEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry country, CGovernmentPosition post)
	{
		Window = window;
		Func<string, string> localise = context.Localisation.Localise;
		CMinister? minister = country.Government.TryGetValue(post.Name, out CMinister? holder) ? holder : null;
		SetText("title", localise(post.Name));
		SetText("name", minister?.Name ?? "???");
		SetText("personality", minister is null ? "???" : CPoliticsScreenText.Personality(localise, minister, post.Name));
		CGuiIcon? legend = window.TryFindIcon("legend_color");
		if (legend is not null)
		{
			legend.Visible = minister is not null;
			if (minister is not null && state.Ideology(minister.Ideology) is { } ideology)
			{
				legend.Modulate = ideology.Colour.ToGodot();
			}
		}
		if (minister is not null)
		{
			CMinisterEntry.ShowPortrait(context, window, minister);
		}
		window.SetTooltips(element => element == "legend_color"
			? CEspionageTooltips.MinisterParty(localise, country, minister)
			: CEspionageTooltips.MinisterPersonality(localise, minister, post.Name));
	}

	public CGuiWindow Window { get; }

	public static CForeignMinisterEntry? Create(CGuiContext context, CGameState state, CCountry country, CGovernmentPosition post)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CForeignMinisterEntry(window, context, state, country, post);
	}

	private void SetText(string name, string text)
	{
		if (Window.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}
}
