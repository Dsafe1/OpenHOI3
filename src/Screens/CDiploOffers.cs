using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The bar of offers made to the player, under the top bar: <c>diplomessageicons_window</c> at
/// <c>diplomessageicon_start</c>, an icon for each offer - the kind's frame and the proposer's flag -
/// oldest on the left, 40 apart, as many as the bar's width holds; one more waits unseen. An icon blinks
/// from its 120th hour and declines its offer at its 168th, shown or not. A left click opens the offer's
/// dialog and takes the icon away; a right click declines the offer at once. See
/// <c>docs/mechanics/agreements.md</c>, section 10.
/// </summary>
public sealed partial class CDiploOffers : Control
{
	private const string BarName = "diplomessageicons_window";
	private const string IconWindowName = "diplomessageicon_entry_window";
	private const string StartName = "diplomessageicon_start";
	private const string OffsetName = "diplomessageicon_offset";

	private sealed class COfferIcon
	{
		public required COffer Offer { get; init; }
		public required long ShownAt { get; init; }
		public required CGuiWindow Window { get; init; }
		public CGuiButton? Button { get; init; }
	}

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountryTag _player;
	private CGuiWindow? _bar;
	private float _step = 40f;
	private double _clock;
	private readonly List<COfferIcon> _icons = new();

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountryTag player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public override void _Ready()
	{
		Name = "DiploOffers";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_bar = CGuiWindow.Create(_context, BarName);
		if (_bar is null)
		{
			return;
		}
		AddChild(_bar);
		CGuiPoint start = _context.Types.FindPosition(StartName)?.Position ?? default;
		_bar.MoveTo(new Vector2(start.X, start.Y));
		CGuiPoint offset = _context.Types.FindPosition(OffsetName)?.Position ?? default;
		var iconType = _context.Types.FindWindow(IconWindowName);
		_step = (iconType?.Size.X ?? 20) + offset.X;
		_simulation.State.OfferAsked += Asked;
	}

	public override void _ExitTree()
	{
		_simulation.State.OfferAsked -= Asked;
	}

	/// <summary>An offer made to the player: a new icon at the end of the bar.</summary>
	private void Asked(COffer offer)
	{
		if (_bar is null || offer.Recipient != _player || COfferAskText.Kind(offer) == 0
			|| CGuiWindow.CreateEntry(_context, IconWindowName) is not { } window)
		{
			return;
		}
		Func<string, string> localise = _context.Localisation.Localise;
		long shownAt = _simulation.State.TotalHours;
		var icon = new COfferIcon { Offer = offer, ShownAt = shownAt, Window = window, Button = window.FindButton("diplomessageicon_button") };
		CTooltip Tooltip()
		{
			(string text, string delayed) = COfferAskText.IconTooltip(localise, _simulation.State, offer, shownAt);
			return new CTooltip { Text = text, Delayed = delayed };
		}
		if (icon.Button is { } button)
		{
			button.SetFrame(COfferAskText.Kind(offer));
			button.Tooltip = Tooltip;
			button.Pressed += () => Open(icon);
			button.RightPressed += () => Decline(icon);
		}
		if (window.FindTextBox("flag") is { } flag)
		{
			flag.Text = "@" + offer.Actor.Value;
			// The flag answers by itself, as the button does: it hangs below the button, over nothing.
			flag.Tooltip = Tooltip;
			// Taking the mouse for that, it hands the button the clicks that fall on the button.
			flag.GuiInput += input =>
			{
				if (input is InputEventMouseButton { Pressed: true } press && icon.Button is { } under
					&& under.GetGlobalRect().HasPoint(flag.GetGlobalTransform() * press.Position))
				{
					if (press.ButtonIndex == MouseButton.Left)
					{
						Open(icon);
						flag.AcceptEvent();
					}
					else if (press.ButtonIndex == MouseButton.Right)
					{
						Decline(icon);
						flag.AcceptEvent();
					}
				}
			};
		}
		// The ring drawn over the flag says nothing and takes no mouse: the flag under it answers.
		if (window.TryFindIcon("messageicon_bg_overlay") is { } overlay)
		{
			overlay.MouseFilter = MouseFilterEnum.Ignore;
		}
		_bar.AddChild(window);
		_icons.Add(icon);
		Layout();
	}

	/// <summary>Oldest on the left, a step apart, every icon whose place starts within the bar's width.</summary>
	private void Layout()
	{
		if (_bar is null)
		{
			return;
		}
		float x = 0f;
		float width = _bar.Size.X;
		foreach (COfferIcon icon in _icons)
		{
			icon.Window.Visible = x <= width;
			icon.Window.MoveTo(new Vector2(x, 0f));
			x += _step;
		}
	}

	/// <summary>Every frame: an icon blinking from its 120th hour, and one declining its offer at its 168th.</summary>
	public override void _Process(double delta)
	{
		_clock += delta;
		long now = _simulation.State.TotalHours;
		foreach (COfferIcon icon in _icons.ToArray())
		{
			if (COfferAskText.Lapsed(icon.ShownAt, now))
			{
				Decline(icon);
				continue;
			}
			if (icon.Button is { } button)
			{
				float alpha = COfferAskText.Blinks(icon.ShownAt, now) ? COfferAskText.BlinkAlpha(_clock) : 1f;
				button.Modulate = new Color(1f, 1f, 1f, alpha);
			}
		}
	}

	private void Open(COfferIcon icon)
	{
		Remove(icon);
		var dialog = new COfferAskDialog();
		dialog.Setup(_context, _simulation, icon.Offer);
		AddChild(dialog);
	}

	private void Decline(COfferIcon icon)
	{
		Remove(icon);
		_simulation.Post(new COfferCommand(icon.Offer with { Stage = EOfferStage.Declined }));
	}

	private void Remove(COfferIcon icon)
	{
		if (!_icons.Remove(icon))
		{
			return;
		}
		icon.Window.QueueFree();
		Layout();
	}
}
