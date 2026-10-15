using System;
using System.Text;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the province view says of revolt risk: the figure and its tooltip. The province view is not built, so nothing
/// shows them yet. See <c>docs/mechanics/revolt.md</c>, section 7.
/// </summary>
public static class CRevoltRiskTooltip
{
	private const string Rebels = "REB";

	/// <summary>
	/// The figure, <c>text_partisans</c>: the revolt risk shown to one place, cut, and a percent sign - yellow where above
	/// nought - and "?" where the player knows the province below everything.
	/// </summary>
	public static string Readout(CGameState state, CProvince province, CCountryTag player)
	{
		if (CProvinceIntel.Level(state, province, player) < 9)
		{
			return "?";
		}
		long shown = CRevoltRisk.Shown(state, province);
		return (shown > 0 ? "§Y" : "") + CNumberText.Format(new CFixedPoint(shown), 1) + "%";
	}

	/// <summary>
	/// The figure's tooltip, a line each, trimmed: where the fog of war hides it - the player knowing the province below 2
	/// - that it cannot be told; otherwise the partisan support where the holder is at war with the owner and the
	/// accumulator is above nought, the holder's sources of <c>global_revolt_risk</c> (the owner's where the rebels hold
	/// it), the province's of <c>local_revolt_risk</c>, the suppression where there is any, and the minimum, always.
	/// Before them all, the rebel type the province would choose, where it would choose one (<c>rebels.md</c>, section 13).
	/// </summary>
	public static string Text(Func<string, string> localise, CGameState state, CProvince province, CCountryTag player)
	{
		if (CProvinceIntel.Level(state, province, player) < 2)
		{
			return localise("CANT_SEE_UNDER_FOW");
		}
		var text = new StringBuilder();
		if (Sim.Revolts.CRebelChoice.TypeFor(state, province) is { } likely)
		{
			text.Append(localise("LIKELY_REBELS")).Append(": §Y").Append(localise(likely.Name + "_title")).Append("§W\n");
		}
		// The holder an enemy of the owner: at war, or either the rebels.
		if (province.RevoltRisk.Raw > 0 && province.Owner is { } owner && province.Controller is { } holder
			&& Sim.Movement.CMovementRules.IsEnemy(state, holder, owner))
		{
			text.Append(localise("PARTISAN_SUPPORT")).Append(": §R+").Append(CNumberText.Format(province.RevoltRisk, 2)).Append("§W\n");
		}
		if (province.Controller is { } controller)
		{
			CCountryTag whose = controller.Value == Rebels && province.Owner is { } owned ? owned : controller;
			if (state.Country(whose) is { } country)
			{
				Lines(text, CModifierText.Sources(localise, country.ModifierSources, CRevoltRisk.GlobalRevoltRisk));
			}
		}
		Lines(text, CModifierText.Sources(localise, province.ModifierSources, CRevoltRisk.LocalRevoltRisk));
		if (province.Suppression.Raw != 0)
		{
			text.Append(localise("SUPPRESSION")).Append(": §G-").Append(CNumberText.Format(province.Suppression, 2)).Append("§W\n");
		}
		text.Append(localise("MINIMUM_REVOLT_RISK")).Append(": §Y")
			.Append(CNumberText.Format(province.Modifier[CRevoltRisk.MinimumRevoltRisk], 2)).Append("%§W\n");
		return text.ToString().Trim();
	}

	private static void Lines(StringBuilder text, string lines)
	{
		if (lines.Length > 0)
		{
			text.Append(lines).Append('\n');
		}
	}
}
