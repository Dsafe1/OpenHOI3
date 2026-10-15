using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Occupation;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the politics screen's occupied countries, a <c>current_policy_entry</c>: the country's flag and name, the
/// player's policy towards it, kept up to date, and the button that opens the choice of a policy; the policy's name and
/// the button say what it does. See <c>docs/mechanics/occupation.md</c>, section 4.
/// </summary>
public sealed class CPolicyEntry
{
	private readonly Func<string, string> _localise;
	private readonly CGameState _state;
	private readonly CCountry _player;
	private readonly CGuiTextBox? _which;
	private COccupationPolicy? _shown;

	private CPolicyEntry(CGuiWindow window, Func<string, string> localise, CGameState state, CCountry player, CCountry occupied)
	{
		Window = window;
		Occupied = occupied;
		_localise = localise;
		_state = state;
		_player = player;
		window.FindButton("country_icon")?.SetCountry(occupied);
		if (window.FindTextBox("country") is { } name)
		{
			name.Text = occupied.Name;
		}
		Func<CTooltip> tooltip = () => COccupationText.Tooltip(localise, COccupationPolicies.Of(state, player, occupied));
		_which = window.FindTextBox("which");
		if (_which is not null)
		{
			_which.PassiveTooltip = tooltip;
		}
		if (window.FindButton("select_button") is { } select)
		{
			select.Pressed += () => SelectPressed?.Invoke(Occupied);
			select.Tooltip = tooltip;
		}
		Refresh();
	}

	public CGuiWindow Window { get; }

	public CCountry Occupied { get; }

	public event Action<CCountry>? SelectPressed;

	/// <summary>The policy's name, written again where it has changed.</summary>
	public void Refresh()
	{
		COccupationPolicy policy = COccupationPolicies.Of(_state, _player, Occupied);
		if (_which is not null && policy != _shown)
		{
			_shown = policy;
			_which.Text = COccupationText.Name(_localise, policy);
		}
	}

	public static CPolicyEntry? Create(CGuiContext context, CGameState state, CCountry player, CCountry occupied)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, COccupationText.RowName);
		return window is null ? null : new CPolicyEntry(window, context.Localisation.Localise, state, player, occupied);
	}
}
