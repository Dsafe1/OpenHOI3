using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The window the attach button brings up, <c>attachment_selection_panel</c>: every unit of the country that may take the
/// selected one, and a click on a row puts it there. It goes with the panel it came from. See
/// <c>docs/mechanics/attaching.md</c>, section 2.
/// </summary>
public sealed class CAttachmentPanel
{
	private readonly CGuiWindow _window;

	private CAttachmentPanel(Node parent, CGuiWindow window, CGuiContext context, CSimulation simulation, CCountry owner, CUnit unit)
	{
		_window = window;
		parent.AddChild(window);
		if (window.FindButton("back_button") is { } back)
		{
			back.Pressed += Hide;
		}
		if (window.FindListBox("list") is not { } list)
		{
			return;
		}
		CGameState state = simulation.State;
		foreach (CUnit candidate in CAttachText.Candidates(state, owner, unit))
		{
			if (CGuiWindow.CreateEntry(context, CAttachText.RowName) is not { } row)
			{
				continue;
			}
			Fill(row, context, state, unit, candidate);
			if (row.TryFindButton("assign") is { } assign)
			{
				assign.Pressed += () =>
				{
					simulation.Post(new CAttachUnitCommand(owner.Tag, unit.Id, candidate.Id));
					Hide();
				};
			}
			list.Add(row);
		}
	}

	private static void Fill(CGuiWindow row, CGuiContext context, CGameState state, CUnit unit, CUnit candidate)
	{
		if (row.FindTextBox("name") is { } name)
		{
			name.Text = candidate.Name;
		}
		if (row.FindTextBox("location") is { } location)
		{
			location.Text = CAttachText.Location(state, unit, candidate);
		}
		row.FindIcon("counter_size")?.SetFrame(COobBrowserText.SizeFrame(candidate));
		if (candidate.SubUnits.Count == 0)
		{
			return;
		}
		string sprite = CUnitPanelText.BrigadeSprite(candidate.SubUnits[0]);
		if (context.Sprites.Find(sprite) is not null)
		{
			row.FindIcon("counter_type")?.SetSprite(sprite);
		}
		if (candidate.SubUnits[0].IsPride)
		{
			row.TryFindIcon("unit_pride_star")?.SetFrame(3);
		}
	}

	public static CAttachmentPanel? Open(Node parent, CGuiContext context, CSimulation simulation, CCountry owner, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.Create(context, CAttachText.WindowName);
		return window is null ? null : new CAttachmentPanel(parent, window, context, simulation, owner, unit);
	}

	private void Hide()
	{
		_window.Visible = false;
	}

	/// <summary>Taken away for good, with the strip of buttons it came from or for a new one.</summary>
	public void Close()
	{
		_window.QueueFree();
	}
}
