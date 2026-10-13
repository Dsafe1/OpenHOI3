using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Screens;

/// <summary>
/// The dialog Declare War, Declare Limited War and Add war goal open, <c>DeclareWarDialog</c>: the goals
/// the player may set up, then the country or the region a goal asks for, each in its own pane in one place;
/// the goal's description, made again every frame; and "I do", which may be pressed while the action with
/// that goal would do something, and posts it accepted. It shows no cost and no warning. See
/// <c>docs/mechanics/war.md</c>, section 10.
/// </summary>
public sealed partial class CWarGoalDialog : Control
{
	public const string WindowName = "DeclareWarDialog";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private COffer _offer = null!;
	private CWarGoal _goal = null!;
	private CGuiButton? _agree;
	private CGuiTextBox? _description;
	private CGuiWindow? _setup;
	private CGuiWindow? _states;
	private CGuiWindow? _countries;

	public CGuiWindow? Window { get; private set; }

	/// <summary>Raised when the dialog is closed.</summary>
	public event Action? Closed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, COffer offer)
	{
		_context = context;
		_simulation = simulation;
		_offer = offer;
		_goal = new CWarGoal(CCasusBelliType.Null, offer.Actor, offer.Recipient);
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
		if (state.Country(_offer.Actor) is not { } actor || state.Country(_offer.Recipient) is not { } recipient)
		{
			return;
		}
		Window.TryFindIcon("LeftShield")?.SetCountry(actor);
		Window.TryFindIcon("RightShield")?.SetCountry(recipient);
		if (Window.FindTextBox("Title") is { } title)
		{
			title.Text = CDiplomacyActions.Title(localise, _offer);
		}
		_description = Window.FindTextBox("Description");
		_setup = Window.FindWindow("wargoal_setup");
		_states = Window.FindWindow("wargoal_state_select");
		_countries = Window.FindWindow("wargoal_country_select");
		foreach (CGuiWindow? pane in new[] { _states, _countries })
		{
			if (pane?.TryFindButton("cancel_select") is { } cancel)
			{
				cancel.Pressed += Cancel;
			}
		}
		if (_setup?.FindListBox("wargoal_list") is { } goals)
		{
			foreach (CCasusBelliType type in CWarGoals.Offered(state, actor, recipient))
			{
				if (CGuiWindow.CreateEntry(_context, "wargoal_item") is not { } row)
				{
					break;
				}
				row.TryFindIcon("wargoal_icon")?.SetFrame(type.SpriteIndex);
				if (row.TryFindButton("select_goal") is { } button)
				{
					button.SetText(localise(type.Name));
					button.Pressed += () => ChooseGoal(type);
				}
				goals.Add(row);
			}
		}
		_agree = Window.FindButton("AgreeButton");
		if (_agree is not null)
		{
			_agree.SetText(localise("PW_I_DO"));
			_agree.Pressed += Agree;
		}
		if (Window.FindButton("DeclineButton") is { } decline)
		{
			decline.SetText(localise("PW_RUN_AWAY"));
			decline.Pressed += Close;
		}
		ShowPane(_setup);
	}

	/// <summary>The description and the Agree button, every frame.</summary>
	public override void _Process(double delta)
	{
		CGameState state = _simulation.State;
		if (_description is not null)
		{
			// Its box's font is black, and §W is turned to it, as every dialog of that font does (COfferAskText).
			_description.Text = CWarText.Setup(_context.Localisation.Localise, state, _goal).Replace("§W", "§b");
		}
		if (_agree is not null)
		{
			_agree.Disabled = !COfferRules.IsValid(state, Posted());
		}
	}

	/// <summary>A goal chosen: the country list where it asks for a country, else the region list where it asks for one.</summary>
	private void ChooseGoal(CCasusBelliType type)
	{
		_goal = new CWarGoal(type, _offer.Actor, _offer.Recipient);
		CGameState state = _simulation.State;
		if (CWarGoals.HasConditions(type.AllowedCountries) && state.Country(_offer.Actor) is { } actor
			&& state.Country(_offer.Recipient) is { } recipient)
		{
			Fill(_countries, "country_list", "wargoal_country_item", "select_country",
				CWarGoals.CountriesFor(state, type, actor, recipient), c => c.Name, ChooseCountry);
			ShowPane(_countries);
			return;
		}
		NextAfterCountry();
	}

	private void ChooseCountry(CCountry country)
	{
		_goal = _goal with { Country = country.Tag };
		NextAfterCountry();
	}

	private void NextAfterCountry()
	{
		if (CWarGoals.HasConditions(_goal.Type.AllowedRegions))
		{
			Fill(_states, "state_list", "wargoal_state_item", "select_state", CWarGoals.RegionsFor(_simulation.State, _goal),
				r => _context.Localisation.Localise(r.Name), ChooseRegion);
			ShowPane(_states);
			return;
		}
		ShowPane(_setup);
	}

	private void ChooseRegion(CRegion region)
	{
		_goal = _goal with { Region = region };
		ShowPane(_setup);
	}

	/// <summary>Cancel in either list: the null type, and back to the goals.</summary>
	private void Cancel()
	{
		_goal = new CWarGoal(CCasusBelliType.Null, _offer.Actor, _offer.Recipient);
		ShowPane(_setup);
	}

	private void Fill<T>(CGuiWindow? pane, string listName, string entryName, string buttonName, List<T> items,
		Func<T, string> text, Action<T> chosen)
	{
		if (pane?.FindListBox(listName) is not { } list)
		{
			return;
		}
		list.Clear();
		foreach (T item in items)
		{
			if (CGuiWindow.CreateEntry(_context, entryName) is not { } row)
			{
				break;
			}
			if (row.TryFindButton(buttonName) is { } button)
			{
				button.SetText(text(item));
				button.Pressed += () => chosen(item);
			}
			list.Add(row);
		}
	}

	private void ShowPane(CGuiWindow? shown)
	{
		foreach (CGuiWindow? pane in new[] { _setup, _states, _countries })
		{
			if (pane is not null)
			{
				pane.Visible = pane == shown;
			}
		}
	}

	/// <summary>The action with the goal set up, accepted - as the original posts it, with no date stamped.</summary>
	private COffer Posted() => _offer with { Stage = EOfferStage.Accepted, WarGoal = _goal };

	private void Agree()
	{
		COffer posted = Posted();
		if (!COfferRules.IsValid(_simulation.State, posted))
		{
			return;
		}
		_simulation.Post(new COfferCommand(posted));
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
