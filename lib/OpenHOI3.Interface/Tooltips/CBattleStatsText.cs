using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What an enhanced game adds to the end of a land unit's tooltip in a battle's window (after BiceLib's
/// <c>activateCombatUnitStats</c>; <c>docs/mechanics/combatview.md</c>, section 12): the unit's soft attack, hard
/// attack, defensiveness and toughness, each summed over its brigades and beside it what the battle's modifier makes
/// of it - which says what the two percentages above are percentages of; and, once it has dealt or taken any, the
/// damage it has dealt and taken in the battle and in the last hour it did. Every word is the localisation's, by keys
/// of ours.
/// </summary>
public static class CBattleStatsText
{
	/// <summary>The heading over the figures. The key whose being there turns the block on.</summary>
	public const string HeadKey = "OPENHOI_BATTLE_STATS_HEAD";

	public const string SoftKey = "OPENHOI_BATTLE_STATS_SOFT";
	public const string HardKey = "OPENHOI_BATTLE_STATS_HARD";
	public const string DefensivenessKey = "OPENHOI_BATTLE_STATS_DEFENSIVENESS";
	public const string ToughnessKey = "OPENHOI_BATTLE_STATS_TOUGHNESS";
	public const string DamageHeadKey = "OPENHOI_BATTLE_DAMAGE_HEAD";
	public const string StrengthKey = "OPENHOI_BATTLE_DAMAGE_STRENGTH";
	public const string OrganisationKey = "OPENHOI_BATTLE_DAMAGE_ORGANISATION";
	public const string BattleKey = "OPENHOI_BATTLE_DAMAGE_BATTLE";
	public const string HourKey = "OPENHOI_BATTLE_DAMAGE_HOUR";
	public const string DealtKey = "OPENHOI_BATTLE_DAMAGE_DEALT";
	public const string TakenKey = "OPENHOI_BATTLE_DAMAGE_TAKEN";

	private const string Indent = "  ";
	private const string Y = "\u00A7Y";
	private const string W = "\u00A7W";

	/// <param name="measure">How wide a text is in the tooltip's font, for the columns; each character one where none is given.</param>
	public static string Block(Func<string, string> localise, CGameState state, CUnit unit, Func<string, float>? measure = null)
	{
		if (!state.Rules.Enhanced || unit.SubUnits.Count == 0 || localise(HeadKey) == HeadKey)
		{
			return "";
		}
		measure ??= text => text.Length;
		// The two figures the tooltip's percentages print: a landing's second scale with the battle's own.
		long attack = unit.CombatAttack.Raw * unit.LandingAttack.Raw / CFixedPoint.Scale;
		long defend = unit.CombatDefence.Raw * unit.LandingDefence.Raw / CFixedPoint.Scale;
		var figures = new List<IReadOnlyList<string>>
		{
			Row(localise(SoftKey), CUnitTotals.Sum(unit, CUnitTotals.SoftAttack).Raw, attack),
			Row(localise(HardKey), CUnitTotals.Sum(unit, CUnitTotals.HardAttack).Raw, attack),
			Row(localise(DefensivenessKey), CUnitTotals.Sum(unit, CUnitTotals.Defensiveness).Raw, defend),
			Row(localise(ToughnessKey), CUnitTotals.Sum(unit, CSubUnitStats.Toughness).Raw, defend),
		};
		// The figures end together, and so does what the battle makes of them, the arrows in a column between.
		string text = "\n" + localise(HeadKey) + "\n" + CTextTable.Render(figures, Indent, measure, 1, 3);

		if (unit.BattleDamage is not { IsEmpty: false } damage)
		{
			return text;
		}
		int brigades = unit.SubUnits.Count;
		string dealt = W + localise(DealtKey), taken = W + localise(TakenKey);
		var rows = new List<IReadOnlyList<string>>
		{
			new[] { "", "", W + localise(StrengthKey), W + localise(OrganisationKey) },
			new[] { W + localise(BattleKey), dealt, Figure(damage.StrengthDealt / CBattleDamage.Fine), Figure(damage.OrganisationDealt / CBattleDamage.Fine) },
			new[] { "", taken, Figure(damage.StrengthTaken), Figure(damage.OrganisationTaken / brigades) },
			new[] { W + localise(HourKey), dealt, Figure(damage.HourStrengthDealt / CBattleDamage.Fine), Figure(damage.HourOrganisationDealt / CBattleDamage.Fine) },
			new[] { "", taken, Figure(damage.HourStrengthTaken), Figure(damage.HourOrganisationTaken / brigades) },
		};
		return text + "\n" + localise(DamageHeadKey) + "\n" + CTextTable.Render(rows, Indent, measure, 2, 3);
	}

	/// <summary>
	/// A figure's row: its name and the figure - and, where the modifier is not one, an arrow and what the modifier
	/// makes of it, each a cell of its own so that they stand in columns.
	/// </summary>
	private static IReadOnlyList<string> Row(string caption, long figure, long modifier)
	{
		return modifier == CFixedPoint.Scale
			? new[] { W + caption, Figure(figure) }
			: new[] { W + caption, Figure(figure), W + Arrow, Figure(figure * modifier / CFixedPoint.Scale) };
	}

	/// <summary>Between a figure and what the battle makes of it: punctuation, as the heading's own is.</summary>
	private const string Arrow = "->";

	private static string Figure(long thousandths) => Y + CNumberText.Format(new CFixedPoint(thousandths), 1) + W;
}
