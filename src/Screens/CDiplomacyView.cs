using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Screens;

/// <summary>
/// The diplomacy screen, <c>country_diplomacy</c>. Of it only the list of decisions is built: made
/// again each time the screen is opened, its rows asked again each frame while it is up. See
/// <c>docs/mechanics/events.md</c>, section 12.
/// </summary>
public sealed partial class CDiplomacyView : CCountryView
{
	private const string ListName = "decisions_listbox";

	private readonly List<CDecisionEntry> _rows = new();
	private CGuiListBox? _list;

	protected override string WindowName => "country_diplomacy";
	protected override string TopColourName => "topcolor_diplomacy";

	protected override void Build(CGuiWindow window)
	{
		_list = window.FindListBox(ListName);
	}

	protected override void Opened()
	{
		_rows.Clear();
		if (_list is null)
		{
			return;
		}
		_list.Clear();
		foreach ((CDecision decision, bool mayTake) in CDecisions.Offered(Simulation.State, Player.Tag))
		{
			if (CDecisionEntry.Create(Context, Simulation, decision, Player.Tag, mayTake) is { } row)
			{
				_rows.Add(row);
				_list.Add(row.Window);
			}
		}
	}

	protected override void Refresh()
	{
		foreach (CDecisionEntry row in _rows)
		{
			row.Refresh();
		}
	}
}
