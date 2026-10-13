using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Screens;

/// <summary>
/// The trade window a row "Offer Trade Agreement" opens, <c>TradeDiplomacyDialog</c>: the two flags, a
/// slider for each good but money - left sells, right buys - with its amount a day and a button putting it
/// back, the offer's text, the AI's likely answer, and Accept, which may be pressed only where the terms
/// are good and their convoy could be made. Each frame a moved slider sets the terms again. See
/// <c>docs/mechanics/trade.md</c>, section 7.
/// </summary>
public sealed partial class CTradeRequestDialog : Control
{
	public const string WindowName = "TradeDiplomacyDialog";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private readonly long[] _places = new long[CTradeWindowText.Sliders.Count];
	private readonly long[] _starts = new long[CTradeWindowText.Sliders.Count];
	private readonly CGuiScrollbar?[] _sliders = new CGuiScrollbar?[CTradeWindowText.Sliders.Count];
	private CGuiButton? _agree;

	public CGuiWindow? Window { get; private set; }

	/// <summary>The offer as its sliders stand.</summary>
	public COffer Offer { get; private set; } = null!;

	/// <summary>Raised when the window is closed, by either button or by itself.</summary>
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
		for (int i = 0; i < CTradeWindowText.Sliders.Count; i++)
		{
			(EGoods _, string slider, string _, string reset) = CTradeWindowText.Sliders[i];
			_sliders[i] = Window.FindScrollbar(slider);
			_starts[i] = _sliders[i]?.Value.Raw ?? 500;
			// Unlike any place, so the first frame sets every good.
			_places[i] = -1000;
			int index = i;
			if (Window.TryFindButton(reset) is { } button)
			{
				button.Pressed += () =>
				{
					if (_sliders[index] is { } toReset)
					{
						toReset.Value = new CFixedPoint(_starts[index]);
					}
				};
			}
		}
		// Debt is used for the deal only where the actor may run one with the recipient and is at war.
		if (Window.TryFindElement("debt_desc") is { } debt && state.Country(Offer.Actor) is { } buyer
			&& state.Country(Offer.Recipient) is { } seller)
		{
			debt.Visible = CGameState.Status(buyer, seller).AllowDebts && state.IsAtWar(buyer.Tag);
		}
		_agree = Window.FindButton("AgreeButton");
		if (_agree is not null)
		{
			_agree.SetText(localise("ACCEPT"));
			_agree.Pressed += Agree;
			_agree.Tooltip = () => new CTooltip { Text = CTradeWindowText.AcceptTooltip(localise, _simulation.State, Offer) };
		}
		if (Window.FindButton("DeclineButton") is { } decline)
		{
			decline.SetText(localise("DECLINE"));
			decline.Pressed += Close;
		}
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (!COfferRules.IsValid(_simulation.State, Offer))
		{
			Close();
			return;
		}
		Refresh();
	}

	/// <summary>Where a slider has moved, every good's amount set again and the texts with it; Accept enabled or not.</summary>
	private void Refresh()
	{
		if (Window is null)
		{
			return;
		}
		Func<string, string> localise = _context.Localisation.Localise;
		CGameState state = _simulation.State;
		bool moved = false;
		for (int i = 0; i < _sliders.Length; i++)
		{
			long place = _sliders[i]?.Value.Raw ?? _starts[i];
			moved |= place != _places[i];
			_places[i] = place;
		}
		if (moved && Offer.Deal is { } deal && state.Country(Offer.Actor) is { } actor && state.Country(Offer.Recipient) is { } recipient)
		{
			CTradeTerms terms = deal.Terms;
			for (int i = 0; i < _sliders.Length; i++)
			{
				(EGoods goods, string _, string value, string _) = CTradeWindowText.Sliders[i];
				long amount = CTradeWindowText.Amount(state, _places[i]);
				terms = CTradeRules.SetTrading(state, terms, actor, recipient, amount, goods);
				if (Window.FindTextBox(value) is { } text)
				{
					text.Text = CTradeWindowText.Value(localise, amount);
				}
			}
			Offer = Offer with { Deal = deal with { Terms = terms } };
			if (Window.FindTextBox("Description") is { } description)
			{
				description.Text = CTradeWindowText.Description(localise, terms, recipient);
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
		}
		if (_agree is not null && Offer.Deal is { } current)
		{
			_agree.Disabled = !CTradeWindowText.MayAccept(state, current);
		}
	}

	/// <summary>The offer dated now and posted.</summary>
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
