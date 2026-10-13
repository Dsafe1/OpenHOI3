using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;

namespace OpenHOI3.Screens;

/// <summary>
/// The request dialog a row of the action list opens, <c>DefaultDiplomacyDialog</c>: the two flags, the
/// action's title and description, the word for the AI's likely answer where it answers the kind, and
/// Accept, which dates the offer now and posts it, and Decline. It closes by itself when the screen is
/// hidden or the offer stops being valid. See <c>docs/mechanics/agreements.md</c>, section 7.
/// </summary>
public sealed partial class CDiplomacyRequestDialog : Control
{
	public const string WindowName = "DefaultDiplomacyDialog";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;

	public CGuiWindow? Window { get; private set; }

	/// <summary>The action asked about.</summary>
	public COffer Offer { get; private set; } = null!;

	/// <summary>Raised when the dialog is closed, by either button or by itself.</summary>
	public event Action? Closed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, COffer offer)
	{
		_context = context;
		_simulation = simulation;
		Offer = offer;
	}

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		// Where the file puts it: the original moves it to diplo_request_pos, whose meaning was not read.
		Window = CGuiWindow.Create(_context, WindowName);
		if (Window is null)
		{
			return;
		}
		AddChild(Window);
		Func<string, string> localise = _context.Localisation.Localise;
		CGameState state = _simulation.State;

		if (state.Country(Offer.Actor) is { } actor)
		{
			Window.TryFindIcon("LeftShield")?.SetCountry(actor);
		}
		if (state.Country(Offer.Recipient) is { } recipient)
		{
			Window.TryFindIcon("RightShield")?.SetCountry(recipient);
		}
		if (Window.FindTextBox("Title") is { } title)
		{
			title.Text = CDiplomacyActions.Title(localise, Offer);
		}
		if (Window.FindTextBox("Description") is { } description)
		{
			description.Text = CDiplomacyActions.Description(localise, state, Offer);
		}
		string chance = CDiplomacyActions.Chance(localise, state, Offer);
		if (Window.FindTextBox("chance_string") is { } chanceText)
		{
			chanceText.Text = chance;
			chanceText.Visible = chance.Length > 0;
		}
		if (Window.TryFindIcon("diplo_popup_chance_bg") is { } chanceBackground)
		{
			chanceBackground.Visible = chance.Length > 0;
		}
		if (Window.FindButton("AgreeButton") is { } agree)
		{
			agree.SetText(localise("ACCEPT"));
			agree.Pressed += Agree;
		}
		if (Window.FindButton("DeclineButton") is { } decline)
		{
			decline.SetText(localise("DECLINE"));
			decline.Pressed += Close;
		}
	}

	/// <summary>Every frame: closed once the offer would do nothing.</summary>
	public override void _Process(double delta)
	{
		if (!COfferRules.IsValid(_simulation.State, Offer))
		{
			Close();
		}
	}

	/// <summary>The offer dated now and posted; the influence is charged when it runs.</summary>
	private void Agree()
	{
		_simulation.Post(new COfferCommand(Offer with { Date = _simulation.State.TotalHours }));
		Close();
	}

	public void Close()
	{
		if (IsQueuedForDeletion())
		{
			return;
		}
		Closed?.Invoke();
		QueueFree();
	}
}
