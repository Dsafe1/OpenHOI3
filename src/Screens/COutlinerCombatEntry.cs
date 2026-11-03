using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// A battle's or a bombing's row in the outliner: an <c>outliner_combat_entry</c> window, the province in
/// <c>entry_text</c>, the figure in <c>info_text</c>, the whole row the <c>outliner_standard</c> button. Which side the
/// player is on is fixed when it is made. See <c>docs/mechanics/outliner.md</c>, section 6.
/// </summary>
public sealed class COutlinerCombatEntry
{
	private readonly CCombat _combat;
	private readonly bool _playerAttacks;
	private readonly CGuiTextBox? _figure;

	/// <summary>Raised when the row is pressed.</summary>
	public event Action? Chosen;

	private COutlinerCombatEntry(CGuiContext context, CGuiWindow window, CGameState state, CCombat combat, CCountryTag player)
	{
		Window = window;
		_combat = combat;
		_playerAttacks = COutlinerText.PlayerAttacks(combat, player);
		if (window.FindTextBox("entry_text") is { } place)
		{
			place.Text = state.Province(combat.Province).Name;
		}
		_figure = window.FindTextBox("info_text");
		Update();
		window.SetTooltips(_ => COutlinerTooltips.Combat(context.Localisation.Localise, state, combat, _playerAttacks),
			"entry_text", "info_text");
		if (window.FindButton("outliner_standard") is { } row)
		{
			row.Pressed += () => Chosen?.Invoke();
		}
	}

	public CGuiWindow Window { get; }

	public CCombat Combat => _combat;

	public static COutlinerCombatEntry? Create(CGuiContext context, CGameState state, CCombat combat, CCountryTag player)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "outliner_combat_entry");
		return window is null ? null : new COutlinerCombatEntry(context, window, state, combat, player);
	}

	/// <summary>Each frame: the figure as the battle stands.</summary>
	public void Update()
	{
		if (_figure is not null)
		{
			_figure.Text = COutlinerText.CombatFigure(_combat, _playerAttacks);
		}
	}
}
