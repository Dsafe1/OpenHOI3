using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Screens;

/// <summary>
/// A way out of the province in the province view's <c>adj</c> list, an <c>adj_member</c>: the kind of way as an icon and
/// the neighbour's name, both saying the kind. See <c>docs/mechanics/provinceview.md</c>, section 2.
/// </summary>
public sealed class CProvinceWayEntry
{
	private CProvinceEdge _edge;

	private CProvinceWayEntry(CGuiWindow window, Func<string, string> localise, Func<CGameState> state)
	{
		Window = window;
		window.SetTooltips(_ => CProvinceViewTooltips.Way(localise, state(), _edge), "adj_text");
	}

	public CGuiWindow Window { get; }

	public static CProvinceWayEntry? Create(CGuiContext context, Func<CGameState> state)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "adj_member");
		return window is null ? null : new CProvinceWayEntry(window, context.Localisation.Localise, state);
	}

	public void Show(CGameState state, CProvinceEdge edge)
	{
		_edge = edge;
		Window.TryFindIcon("adj_icon")?.SetSprite(CProvinceViewText.WaySprite(state, edge));
		if (Window.FindTextBox("adj_text") is { } text)
		{
			text.Text = state.Province(edge.To).Name;
		}
	}
}
