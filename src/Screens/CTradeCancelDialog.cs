using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Screens;

/// <summary>
/// The dialog a line of the trade list's cancel button opens, <c>DefaultConfirm</c>: "Cancel Trade
/// Agreement", what the route moves, and Ok, which posts the cancellation - the player the actor, the
/// partner the recipient - and Cancel, which only closes. Nothing is tested at either press, and each
/// press of the line's button opens another. See <c>docs/mechanics/trade.md</c>, section 13.
/// </summary>
public sealed partial class CTradeCancelDialog : Control
{
	public const string WindowName = "DefaultConfirm";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CTradeRoute _route = null!;
	private Sim.Countries.CCountryTag _player;

	public CGuiWindow? Window { get; private set; }

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CTradeRoute route, Sim.Countries.CCountryTag player)
	{
		_context = context;
		_simulation = simulation;
		_route = route;
		_player = player;
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
		if (Window.FindTextBox("Title") is { } title)
		{
			title.Text = CTradeListText.CancelTitle(localise);
		}
		if (Window.FindTextBox("Description") is { } description)
		{
			description.Text = CTradeListText.CancelDescription(localise, _simulation.State, _route, _player);
		}
		// The buttons keep their file's words, Ok and Cancel.
		if (Window.FindButton("AgreeButton") is { } agree)
		{
			agree.Pressed += Agree;
		}
		if (Window.FindButton("DeclineButton") is { } decline)
		{
			decline.Pressed += Close;
		}
	}

	/// <summary>The cancellation posted: a trade offer that ends, for the route as it is.</summary>
	private void Agree()
	{
		CTradeRoute route = _route;
		_simulation.Post(new COfferCommand(new COffer(EOfferKind.Trade, _player, CTradeListText.Partner(route, _player),
			EOfferStage.Proposed, false, _simulation.State.TotalHours)
		{
			Deal = new CTradeDeal(route.From, route.To, route.Terms) { Start = route.Start },
		}));
		Close();
	}

	public void Close()
	{
		if (IsQueuedForDeletion())
		{
			return;
		}
		QueueFree();
	}
}
