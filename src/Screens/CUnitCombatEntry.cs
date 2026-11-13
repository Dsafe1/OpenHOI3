using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The row a selected unit's panel has for a battle the unit is in: the
/// <c>combat_entry</c> window, with the battle's name, a flag at each end and the same
/// bar as the battle's own window. A click on it brings that window up. See
/// <c>docs/mechanics/combatview.md</c>, section 7.
/// </summary>
public sealed class CUnitCombatEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CUnit _unit;
	private readonly CGuiTextBox? _name;
	private readonly CGuiIcon? _progress;
	private readonly CGuiIcon? _attackerFlag;
	private readonly CGuiIcon? _defenderFlag;
	private CCountryTag? _attacker;
	private CCountryTag? _defender;

	/// <summary>Raised when the row is clicked.</summary>
	public event Action? Selected;

	private CUnitCombatEntry(CGuiWindow window, CGuiContext context, CGameState state, CUnit unit, CCombat combat, CCountryTag? player)
		: base(window)
	{
		_context = context;
		_state = state;
		_unit = unit;
		Combat = combat;
		_name = window.FindTextBox("desc");
		_progress = window.FindIcon("progress");
		_attackerFlag = window.FindIcon("attacker_flag");
		_defenderFlag = window.FindIcon("defender_flag");
		AnswerFor(_ => CBattleRowTooltip.For(context.Localisation.Localise, state, combat, player), "desc");
		if (window.FindButton("select") is { } select)
		{
			select.Pressed += () =>
			{
				if (IsLive)
				{
					Selected?.Invoke();
				}
			};
		}
		Update();
	}

	public CCombat Combat { get; }

	/// <param name="player">Whose knowledge decides which land units the tooltip draws by the unknown counter.</param>
	public static CUnitCombatEntry? Create(CGuiContext context, CGameState state, CUnit unit, CCombat combat, CCountryTag? player)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "combat_entry");
		return window is null ? null : new CUnitCombatEntry(window, context, state, unit, combat, player);
	}

	/// <summary>
	/// Whether the row still stands for anything: the battle is one of the unit's and
	/// both of its sides have a country and a unit - or it is the bombing the unit is
	/// in, which is asked nothing more.
	/// </summary>
	private bool IsLive => ReferenceEquals(_unit.Bombing, Combat)
		|| (_unit.Combats.Contains(Combat)
			&& Combat.Attacker.Units.Count > 0 && Combat.Defender.Units.Count > 0
			&& Combat.Attacker.Countries.Count > 0 && Combat.Defender.Countries.Count > 0);

	public override void Update()
	{
		if (!IsLive)
		{
			return;
		}
		Set(_name, CCombatViewText.Name(_context.Localisation.Localise, _state, Combat));
		_progress?.SetProgress(CCombatViewText.Progress(CCombatViewText.Share(Combat)));
		ShowFlag(_attackerFlag, CCombatViewText.Flag(_state, Combat.Attacker), ref _attacker);
		ShowFlag(_defenderFlag, CCombatViewText.Flag(_state, Combat.Defender), ref _defender);
	}

	private void ShowFlag(CGuiIcon? icon, CCountryTag? tag, ref CCountryTag? shown)
	{
		if (icon is null || tag == shown || tag is not { } country || _state.Country(country) is not { } found)
		{
			return;
		}
		shown = tag;
		icon.SetCountry(found);
	}
}
