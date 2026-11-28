using OpenHOI3.Gui;
using OpenHOI3.Interface.Events;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Screens;

/// <summary>
/// One row of the decision list, a <c>diplomatic_decision_entry</c>: the decision's title, the button
/// that takes it - disabled where it may not be taken, and never enabled again while the screen is up
/// - and the icon; and in an enhanced game whose mod declares it, the tick that says whether the decision
/// counts for the "decisions available" alert. See <c>docs/mechanics/events.md</c>, section 12.
/// </summary>
public sealed class CDecisionEntry
{
	public const string WindowName = "diplomatic_decision_entry";

	private readonly CSimulation _simulation;
	private readonly CDecision _decision;
	private readonly CCountryTag _country;
	private readonly CGuiButton? _select;
	private readonly CGuiButton? _alert;

	/// <summary>What the tick was just set to and the game has not yet taken up; null once it has.</summary>
	private bool? _asked;

	private CDecisionEntry(CGuiWindow window, CGuiContext context, CSimulation simulation, CDecision decision,
		CCountryTag country, bool mayTake)
	{
		Window = window;
		_simulation = simulation;
		_decision = decision;
		_country = country;

		CGameState state = simulation.State;
		CEventScope scope = CDecisions.ScopeOf(country);
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = CEventText.Localise(context.Localisation.Localise, state, decision.Name + "_title", scope).Trim();
			name.Tooltip = () => new CTooltip
			{
				Text = CEventText.Localise(context.Localisation.Localise, state, decision.Name + "_desc", CDecisions.ScopeOf(_country)).Trim(),
			};
		}
		var words = new CScriptWords(context.Localisation.Localise, state, country)
		{
			ShowAll = () => Godot.Input.IsKeyPressed(Godot.Key.Alt),
		};
		_select = window.FindButton("select_button");
		if (_select is not null)
		{
			_select.Disabled = !mayTake;
			_select.Pressed += Take;
			// What taking it does, in a scope set back to its seed first.
			_select.Tooltip = () => new CTooltip { Text = CEffectText.For(words, _decision.Effects, CDecisions.ScopeOf(_country)) };
		}
		// What it needs: its allow block's account of itself.
		if (window.FindIcon("info_icon") is { } info)
		{
			info.Tooltip = () => new CTooltip { Text = CConditionText.For(words, _decision.Allow, CDecisions.ScopeOf(_country)) };
		}
		// An enhanced game's tick for the alert: a window of the mod's own put into the row where the mod declares one
		// (CDecisionAlertText) - never in the row's own declaration, which the original reads too.
		if (state.Rules.Enhanced && context.Types.FindWindow(CDecisionAlertText.WindowName) is not null
			&& CGuiWindow.Create(context, CDecisionAlertText.WindowName) is { } tick)
		{
			window.AddChild(tick);
			_alert = tick.TryFindButton(CDecisionAlertText.ButtonName);
			if (_alert is not null)
			{
				_alert.Pressed += () => AlertPressed?.Invoke(this, !Counts, Godot.Input.IsKeyPressed(Godot.Key.Ctrl));
				_alert.Tooltip = () => CDecisionAlertText.Tooltip(context.Localisation.Localise, Counts);
				_alert.SetText(CDecisionAlertText.Mark(Counts));
			}
		}
	}

	public CGuiWindow Window { get; }

	/// <summary>The decision's name, as the files and a save have it.</summary>
	public string DecisionName => _decision.Name;

	/// <summary>
	/// The tick pressed: the row, whether its decision is now to count for the alert, and whether Ctrl was held - which
	/// asks the same of every row the list shows.
	/// </summary>
	public event System.Action<CDecisionEntry, bool, bool>? AlertPressed;

	/// <summary>Whether the decision counts for the alert: as just asked, until the game is seen to have it; else as the country has it.</summary>
	private bool Counts => _asked ?? !(_simulation.State.Country(_country)?.QuietDecisions.Contains(_decision.Name) ?? false);

	/// <summary>Shows the tick as asked at once; the order that makes it so runs with the next orders.</summary>
	public void AskAlert(bool counts)
	{
		_asked = counts;
		_alert?.SetText(CDecisionAlertText.Mark(counts));
	}

	/// <summary>Makes the row, or answers null where no file declares its window.</summary>
	public static CDecisionEntry? Create(CGuiContext context, CSimulation simulation, CDecision decision, CCountryTag country,
		bool mayTake)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CDecisionEntry(window, context, simulation, decision, country, mayTake);
	}

	/// <summary>
	/// While the screen is up: <c>allow</c>, then <c>potential</c>, asked again, the button disabled
	/// where either fails - and nothing done where both hold.
	/// </summary>
	public void Refresh()
	{
		if (_alert is not null)
		{
			// What was asked stands until the game has it; then the tick follows the game - a fellow player's change too.
			bool has = !(_simulation.State.Country(_country)?.QuietDecisions.Contains(_decision.Name) ?? false);
			if (_asked == has)
			{
				_asked = null;
			}
			_alert.SetText(CDecisionAlertText.Mark(Counts));
		}
		if (_select is null || _select.Disabled)
		{
			return;
		}
		CGameState state = _simulation.State;
		CEventScope scope = CDecisions.ScopeOf(_country);
		if (!CConditions.Holds(state, _decision.Allow, scope) || !CConditions.Holds(state, _decision.Potential, scope))
		{
			_select.Disabled = true;
		}
	}

	/// <summary>The button does not ask <c>allow</c>; the command does when it runs.</summary>
	private void Take()
	{
		_simulation.Post(new CExecuteDecisionCommand(_decision.Name, CDecisions.ScopeOf(_country)));
	}
}
