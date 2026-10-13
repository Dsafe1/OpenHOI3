using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;

namespace OpenHOI3.Screens;

/// <summary>
/// The dialog an offer to the player opens from its icon: <c>eu3dialog.gui</c>'s <c>DefaultDialog</c>,
/// centred where its file puts it, the proposer's flag on the left and the player's on the right, the
/// kind's title and the offer's text, and Accept and Decline, each posting the answer. Accept's Enter is
/// taken away; Decline keeps Escape. Every frame it declines once its time is up. Any number may be open,
/// one over another. See <c>docs/mechanics/agreements.md</c>, section 10.
/// </summary>
public sealed partial class COfferAskDialog : Control
{
	public const string WindowName = "DefaultDialog";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private COffer _offer = null!;

	public CGuiWindow? Window { get; private set; }

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, COffer offer)
	{
		_context = context;
		_simulation = simulation;
		_offer = offer;
	}

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Window = CGuiWindow.Create(_context, WindowName);
		if (Window is null)
		{
			return;
		}
		AddChild(Window);
		Func<string, string> localise = _context.Localisation.Localise;
		CGameState state = _simulation.State;
		if (state.Country(_offer.Actor) is { } proposer)
		{
			Window.TryFindIcon("LeftShield")?.SetCountry(proposer);
		}
		if (state.Country(_offer.Recipient) is { } player)
		{
			Window.TryFindIcon("RightShield")?.SetCountry(player);
		}
		if (Window.FindTextBox("Title") is { } title)
		{
			title.Text = COfferAskText.Title(localise, _offer);
		}
		if (Window.FindTextBox("Description") is { } description)
		{
			description.Text = COfferAskText.Description(localise, state, _offer);
		}
		if (Window.FindButton("AgreeButton") is { } agree)
		{
			agree.SetText(localise("ACCEPT"));
			agree.ClearShortcut();
			agree.Pressed += () => Answer(EOfferStage.Accepted);
		}
		if (Window.FindButton("DeclineButton") is { } decline)
		{
			decline.SetText(localise("DECLINE"));
			decline.Pressed += () => Answer(EOfferStage.Declined);
		}
	}

	public override void _Process(double delta)
	{
		if (!COfferAskText.StaysOpen(_simulation.State, _offer))
		{
			Answer(EOfferStage.Declined);
		}
	}

	/// <summary>The answer posted, nothing tested, and the dialog gone.</summary>
	private void Answer(EOfferStage stage)
	{
		if (IsQueuedForDeletion())
		{
			return;
		}
		_simulation.Post(new COfferCommand(_offer with { Stage = stage }));
		QueueFree();
	}
}
