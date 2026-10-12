using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One unit in a list of the battle window: the <c>division_option</c> window, with the
/// picture of its kind, its name, where it is, its width and its two bars. See
/// <c>docs/mechanics/combatview.md</c>, section 5.
/// </summary>
public sealed class CCombatUnitEntry
{
	/// <summary>A unit that is not landing from the sea has its plate drawn whole.</summary>
	private const int WholePlate = 100;

	private readonly CGameState _state;
	private readonly CGuiIcon? _strength;
	private readonly CGuiIcon? _organisation;
	private readonly CGuiTextBox? _location;

	private CCombatUnitEntry(CGuiWindow window, CGameState state, CUnit unit)
	{
		Window = window;
		Unit = unit;
		_state = state;
		_strength = window.FindIcon("str_bar");
		_organisation = window.FindIcon("org_bar");
		_location = window.FindTextBox("location");

		string sprite = CCombatViewText.UnitSprite(unit);
		if (sprite.Length != 0)
		{
			window.FindIcon("counter_type")?.SetSprite(sprite);
		}
		window.FindIcon("unit_cw_strip")?.SetFrame(CCombatViewText.UnitWidthFrame(unit));
		window.FindIcon("combat_unit_bg")?.SetProgress(WholePlate);
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = unit.Name;
		}
		Update();
	}

	public CGuiWindow Window { get; }

	public CUnit Unit { get; }

	/// <param name="chance">Whether the entry says its chance of coming up - an attacker's reserve - rather than the unit's whole.</param>
	public static CCombatUnitEntry? Create(CGuiContext context, CGameState state, CCombat combat, CUnit unit, bool chance)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "division_option");
		if (window is null)
		{
			return null;
		}
		Func<string, string> localise = context.Localisation.Localise;
		window.SetTooltips(element => chance
				? CBattleUnitTooltips.Reserve(localise, state, unit)
				: CBattleUnitTooltips.Unit(localise, state, combat, unit, element),
			"name", "location", "org_bar", "str_bar");
		return new CCombatUnitEntry(window, state, unit);
	}

	/// <summary>Writes what an hour may have changed: the two bars and where the unit is.</summary>
	public void Update()
	{
		_strength?.SetProgress(CCombatViewText.StrengthBar(Unit));
		_organisation?.SetProgress(CCombatViewText.OrganisationBar(_state, Unit));
		string location = CCombatViewText.Location(_state, Unit);
		if (_location is not null && _location.Text != location)
		{
			_location.Text = location;
		}
	}
}
