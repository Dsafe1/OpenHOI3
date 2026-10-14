using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the Strategic Effects window says: a row for every triggered modifier whose <c>potential</c> holds for the
/// player's country, in file order, each with its name, every value of its modifier, its strip's frame, whether the
/// country has it, and its <c>trigger</c> conditions met out of those counted; and a row's tooltip, the <c>trigger</c>
/// block's account of itself. See <c>docs/mechanics/strategiceffects.md</c>.
/// </summary>
public static class CStrategicEffectsText
{
	public const string WindowName = "modifier_window";
	public const string RowName = "modifier_entry";
	public const string ButtonTooltip = "STRATEGIC_EFFECT_BUTTON_TOOLTIP";

	/// <summary>A row's "?", the one element of it with a tooltip.</summary>
	public const string InfoName = "modifier_info";

	/// <summary>The rows, in file order: every entry whose <c>potential</c> holds for the country.</summary>
	public static List<CStrategicEffectRow> Rows(CGameState state, CCountry country)
	{
		var rows = new List<CStrategicEffectRow>();
		CEventScope scope = CEventScope.Start(country.Tag);
		foreach (CTriggeredModifier entry in state.TriggeredModifiers)
		{
			if (!CTriggeredModifiers.PotentialHolds(state, entry, country))
			{
				continue;
			}
			(int met, int counted) = CConditionTally.Of(state, entry.Trigger, scope);
			rows.Add(new CStrategicEffectRow(entry, country.TriggeredModifiers.Contains(entry), met, counted));
		}
		return rows;
	}

	/// <summary><c>modifier_name</c>: its key, localised.</summary>
	public static string Name(Func<string, string> localise, CTriggeredModifier entry) => localise(entry.Name);

	/// <summary><c>modifier_effect</c>: every value of its modifier, a line a kind.</summary>
	public static string Effect(Func<string, string> localise, CTriggeredModifier entry) =>
		CModifierText.AllValues(localise, entry.Modifier);

	/// <summary><c>modifier_status</c>: met, a space, a slash, a space, counted.</summary>
	public static string Status(CStrategicEffectRow row) =>
		row.Met.ToString(CultureInfo.InvariantCulture) + " / " + row.Counted.ToString(CultureInfo.InvariantCulture);

	/// <summary><c>modifier_progress</c>, in hundredths: met x 100 / counted, nought where nothing is counted.</summary>
	public static int Progress(CStrategicEffectRow row) => row.Counted < 1 ? 0 : row.Met * 100 / row.Counted;

	/// <summary>The strip's frame counted from one: its icon counted from nought.</summary>
	public static int Frame(CTriggeredModifier entry) => entry.Icon + 1;

	/// <summary>A row's "?": its <c>trigger</c> block written out in the country's scope.</summary>
	public static string Tooltip(CScriptWords words, CTriggeredModifier entry, CCountry country) =>
		CConditionText.For(words, entry.Trigger, CEventScope.Start(country.Tag));
}
