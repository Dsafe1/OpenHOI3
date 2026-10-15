using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Occupation;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the choice of an occupation policy, a <c>possible_policy_entry</c>: the policy's name, what it does, which of
/// the two indicators it wears - the current policy's or another's - and the button, which may be pressed where the
/// policy's <c>allow</c> holds, the current one's included, and says what that asks. See
/// <c>docs/mechanics/occupation.md</c>, section 4.
/// </summary>
public sealed class CPossiblePolicyEntry
{
	private CPossiblePolicyEntry(CGuiWindow window, Func<string, string> localise, CGameState state, CCountry player,
		CCountry occupied, COccupationPolicy policy)
	{
		Window = window;
		Policy = policy;
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = COccupationText.Name(localise, policy);
		}
		if (window.FindTextBox("effect") is { } effect)
		{
			effect.Text = CModifierText.AllValues(localise, policy.Modifier);
		}
		bool current = COccupationText.IsCurrent(state, player, occupied, policy);
		if (window.TryFindIcon(current ? "possible_indicator" : "current_indicator") is { } other)
		{
			other.Visible = false;
		}
		if (window.FindButton("select_button") is { } select)
		{
			select.Disabled = !COccupationText.MayChoose(state, player, occupied, policy);
			select.Pressed += () => SelectPressed?.Invoke(Policy);
			var words = new CScriptWords(localise, state, player.Tag);
			select.Tooltip = () => new CTooltip { Text = COccupationText.ChoiceTooltip(words, player, occupied, policy) };
		}
	}

	public CGuiWindow Window { get; }

	public COccupationPolicy Policy { get; }

	public event Action<COccupationPolicy>? SelectPressed;

	public static CPossiblePolicyEntry? Create(CGuiContext context, CGameState state, CCountry player, CCountry occupied,
		COccupationPolicy policy)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, COccupationText.ChoiceRowName);
		return window is null ? null : new CPossiblePolicyEntry(window, context.Localisation.Localise, state, player, occupied, policy);
	}
}
