using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The prompt a lend-lease row's cancel opens, <c>DefaultConfirm</c> with no title: <c>CANCEL_LL_PROMPT</c>
/// naming the receiver, and Ok, which ends the giving, and Cancel. See <c>docs/mechanics/aid.md</c>,
/// section 6.
/// </summary>
public sealed partial class CLendLeasePrompt : Control
{
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountryTag _giver;
	private CCountry _receiver = null!;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountryTag giver, CCountry receiver)
	{
		_context = context;
		_simulation = simulation;
		_giver = giver;
		_receiver = receiver;
	}

	public override void _Ready()
	{
		Name = "CancelLendLease";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		if (CGuiWindow.Create(_context, CTradeCancelDialog.WindowName) is not { } window)
		{
			return;
		}
		AddChild(window);
		if (window.FindTextBox("Description") is { } description)
		{
			description.Text = CLendLeaseText.CancelPrompt(_context.Localisation.Localise, _receiver);
		}
		if (window.FindButton("AgreeButton") is { } agree)
		{
			agree.Pressed += () =>
			{
				_simulation.Post(new CRemoveLendLeaseCommand(_giver, _receiver.Tag));
				QueueFree();
			};
		}
		if (window.FindButton("DeclineButton") is { } decline)
		{
			decline.Pressed += QueueFree;
		}
	}
}
