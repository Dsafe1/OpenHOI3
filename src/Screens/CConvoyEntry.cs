using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>A row of the production screen's <c>convoy_queue</c>, a <c>convoy_entry</c>. See <c>docs/mechanics/convoywindows.md</c>, section 2.</summary>
public sealed class CConvoyEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountryTag _viewer;

	private CConvoyEntry(CGuiWindow window, CGuiContext context, CGameState state, CConvoy convoy, CCountryTag viewer, Action<CConvoy> chosen)
	{
		Window = window;
		Convoy = convoy;
		_context = context;
		_state = state;
		_viewer = viewer;
		if (window.TryFindButton("select") is { } select)
		{
			select.Disabled = !CConvoyWindowText.MayOpen(convoy, viewer);
			select.Pressed += () => chosen(convoy);
			select.Tooltip = () => CConvoyWindowText.Tooltip(context.Localisation.Localise, state, convoy, viewer);
		}
	}

	public CGuiWindow Window { get; }

	public CConvoy Convoy { get; }

	public static CConvoyEntry? Create(CGuiContext context, CGameState state, CConvoy convoy, CCountryTag viewer, Action<CConvoy> chosen)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CConvoyWindowText.RowName);
		return window is null ? null : new CConvoyEntry(window, context, state, convoy, viewer, chosen);
	}

	/// <summary>Filled every frame the row is on the screen.</summary>
	public void Update()
	{
		if (Window.FindTextBox("convoy_name") is { } name)
		{
			name.Text = CConvoyWindowText.RowTitle(_state, Convoy);
		}
		if (Window.FindTextBox("num_convoys") is { } count)
		{
			count.Text = CConvoyWindowText.RowCount(_state, Convoy, _viewer);
		}
		foreach ((Sim.Economy.EGoods goods, string icon) in CConvoyWindowText.RowIcons)
		{
			if (Window.TryFindIcon(icon) is { } picture)
			{
				picture.Visible = Convoy.Carries[(int)goods];
			}
		}
		if (Window.FindTextBox("lend_lease") is { } lendLease)
		{
			string? line = CConvoyWindowText.LendLeaseLine(_context.Localisation.Localise, _state, Convoy, _viewer);
			lendLease.Visible = line is not null;
			lendLease.Text = line ?? "";
		}
	}
}
