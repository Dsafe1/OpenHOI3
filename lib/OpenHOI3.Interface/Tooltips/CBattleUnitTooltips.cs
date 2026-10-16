using System;
using System.Text;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What an entry of a battle's window says in a tooltip: a land unit's bars, width and the whole
/// of what it brings to the battle; an attacker's reserve its chance of coming up; a ship its
/// target, positioning, reach, attack and defence; a wing its attack and defence. See
/// <c>docs/mechanics/combatview.md</c>, section 12.
/// </summary>
public static class CBattleUnitTooltips
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>A land unit on the front, or a reserve of the defender's.</summary>
	public static CTooltip Unit(Func<string, string> localise, CGameState state, CCombat combat, CUnit unit, string element)
	{
		CCountry? owner = state.Country(unit.Owner);
		switch (element)
		{
			case "str_bar":
				return Text(Strength(localise, CUnitHeaderTooltips.StrengthShare(unit), HasStrength(unit)));
			case "org_bar":
				return Text(Organisation(localise, CUnitTotals.AverageOrganisation(unit)));
			case "unit_cw_strip":
				CFixedPoint width = owner is null ? CUnitTotals.Sum(unit, CSubUnitType.CombatWidth) : CUnitTotals.Width(owner, unit);
				return Text(localise("UNIT_COMBAT_WIDTH") + ": §Y" + CNumberText.Format(width, 1) + "§W\n");
		}

		var text = new StringBuilder();
		if (unit.Leader is { } leader)
		{
			text.Append(CTextTemplate.Fill(localise("LEADER_COMMAND_INFO"),
				("NAME", leader.Name), ("SKILL", leader.Skill.ToString(System.Globalization.CultureInfo.InvariantCulture))));
		}
		else
		{
			text.Append(localise(unit.SubUnits.Count == 1 ? "NO_LEADER_FOR_THIS_2" : "NO_LEADER_FOR_THIS_1")).Append('\n');
		}
		text.Append(Landing(localise, state, unit));
		// The second pair of multipliers is a landing's alone (transport.md, section 12): where it
		// makes a figure differ from its base, both are written, each coloured against the other.
		text.Append(Scaled(localise("BATTLE_ATTACKMOD"), unit.CombatAttack, unit.LandingAttack));
		text.Append(Scaled(localise("BATTLE_DEFENDMOD"), unit.CombatDefence, unit.LandingDefence));
		text.Append(CBattleTooltips.Modifiers(localise, unit.CombatModifiers));
		if (combat.SideOf(unit) is { Tactic: { } tactic } side)
		{
			text.Append(CBattleTooltips.TacticFigures(localise, tactic, true, side.IsAttacker, !side.IsAttacker));
		}
		text.Append(localise("AP_ATTACK")).Append(": §Y").Append(CNumberText.Trimmed(CUnitTotals.Best(unit, CUnitTotals.Piercing)))
			.Append("§!\n");
		text.Append(localise("ARMOR_VALUE")).Append(": §Y").Append(CNumberText.Trimmed(CUnitTotals.Best(unit, CUnitTotals.Armour)))
			.Append("§!\n");
		return Text(text.ToString());
	}

	/// <summary>
	/// A unit landing from the sea: while it is short of the coast that it is in the transports, a blank line, its
	/// progress - red, green from the whole - and each of its two scales that is not one, red below and green above, and a
	/// blank line. Nothing for any other unit.
	/// </summary>
	public static string Landing(Func<string, string> localise, CGameState state, CUnit unit)
	{
		if (!Sim.Transport.CLanding.IsLanding(state, unit))
		{
			return "";
		}
		long progress = Sim.Transport.CLanding.Progress(state, unit);
		var text = new StringBuilder();
		if (progress < Scale)
		{
			text.Append(localise("AMPH_INVASION_COMBAT_PROGRESS_DESC")).Append("\n\n");
		}
		text.Append(CTextTemplate.Fill(localise("AMPH_INVASION_PROGRESS"),
			("PERC", (progress >= Scale ? "§G" : "§R") + CNumberText.Format(new CFixedPoint(progress * 100), 2) + "§!")));
		foreach ((string key, CFixedPoint scale) in new[]
		{
			("AMPH_INVASION_ATTACK_SCALE", unit.LandingAttack), ("AMPH_INVASION_DEFEND_SCALE", unit.LandingDefence),
		})
		{
			if (scale != CFixedPoint.One)
			{
				text.Append('\n').Append(CTextTemplate.Fill(localise(key),
					("PERC", (scale < CFixedPoint.One ? "§R" : "§G") + Hundredfold(scale) + "§!")));
			}
		}
		return text.Append("\n\n").ToString();
	}

	/// <summary>
	/// A modifier line: the figure times its landing scale, as a percentage - yellow, or green above its base and red
	/// below - and where the two differ, " / " and the base in the other colour.
	/// </summary>
	private static string Scaled(string label, CFixedPoint figure, CFixedPoint scale)
	{
		var scaled = new CFixedPoint(figure.Raw * scale.Raw / Scale);
		if (scaled == figure)
		{
			return label + ": §Y" + Hundredfold(scaled) + "§!%\n";
		}
		bool above = scaled > figure;
		return label + ": " + (above ? "§G" : "§R") + Hundredfold(scaled) + "§!% / " + (above ? "§R" : "§G")
			+ Hundredfold(figure) + "§!%\n";
	}

	/// <summary>A reserve of the attacker's: its chance of coming up this hour, whatever the element.</summary>
	public static CTooltip Reserve(Func<string, string> localise, CGameState state, CUnit unit)
	{
		CFixedPoint chance = CFrontLine.ReinforceChance(state, unit, true);
		return Text(localise("BATTLE_REINFORCE_CHANCE") + ": §Y" + CNumberText.Format(new CFixedPoint(chance.Raw * 100), 2) + "§W%\n");
	}

	/// <summary>A ship.</summary>
	public static CTooltip Ship(Func<string, string> localise, CGameState state, CCombat combat, CSubUnit ship, string element)
	{
		CCountry? owner = ship.Unit is { } fleet ? state.Country(fleet.Owner) : null;
		switch (element)
		{
			case "str_bar":
				long most = owner is null ? ship.Stats[CSubUnitType.MaxStrength].Raw : CUnits.MaxStrength(state, owner, ship, false, false).Raw;
				return Text(Strength(localise, Share(ship.Strength.Raw, most), most != 0));
			case "org_bar":
				return Text(Organisation(localise, ship.Organisation));
		}

		var text = new StringBuilder("==>");
		if (Target(combat, ship) is { } target)
		{
			bool enemy = CMovementRules.IsEnemy(state, target.Unit?.Owner, ship.Unit?.Owner);
			text.Append(enemy ? "§G" : "§R").Append(target.Name).Append("§W");
		}
		else
		{
			text.Append("<> ");
		}
		text.Append(" (").Append(CNumberText.Format(ship.Distance, 0)).Append("km)\n");

		if (ship.Unit is { } unit)
		{
			if (unit.Combats.Count > 0)
			{
				CCombat battle = unit.Combats[0];
				CCombatSide side = battle.Attacker.Countries.Contains(unit.Owner) ? battle.Attacker : battle.Defender;
				long placed = (ship.Stats[CNavalBattle.Positioning].Raw + side.Positioning.Raw) * 100;
				placed = placed * (Scale + (owner?.Technology[CNavalTargets.TargetingChance].Raw ?? 0)) / Scale;
				text.Append(localise("EFF_POS")).Append(" : §Y").Append(CNumberText.Format(new CFixedPoint(placed), 1)).Append("%§W\n");
			}
			CProvince province = state.Province(unit.Location);
			long rain = province.Weather.Precipitation.Raw
				* state.Defines[CWeatherEffects.WeatherSection, CNavalTargets.FiringRangeModifier].Raw / Scale;
			long range = ship.Stats[CNavalBattle.FiringDistance].Raw * (Scale + rain) / Scale;
			text.Append(localise("BATTLE_FIRINGRANGE")).Append(CNumberText.Format(new CFixedPoint(range), 1)).Append("km");
			if (rain != 0)
			{
				text.Append('(').Append(rain < 0 ? "§R" : "§G").Append(CNumberText.Format(new CFixedPoint(rain * 100), 0)).Append("%§W)");
			}
			text.Append('\n');
		}
		text.Append(localise("BATTLE_ATTACKMOD")).Append(": §Y").Append(Hundredfold(ship.CombatAttack)).Append("§W%\n");
		text.Append(localise("BATTLE_DEFENDMOD")).Append(": §Y").Append(Hundredfold(ship.CombatDefence)).Append("§W%\n");
		text.Append(CBattleTooltips.Modifiers(localise, ship.CombatModifiers));
		return Text(text.ToString());
	}

	/// <summary>A wing; a row of what is bombed has nothing built for it.</summary>
	public static CTooltip Wing(Func<string, string> localise, CSubUnit wing, string element)
	{
		switch (element)
		{
			case "str_bar":
				long most = wing.Stats[CSubUnitType.MaxStrength].Raw;
				return Text(Strength(localise, Share(wing.Strength.Raw, most), most != 0));
			case "org_bar":
				return Text(Organisation(localise, wing.Organisation));
		}
		var text = new StringBuilder();
		text.Append(localise("BATTLE_ATTACKMOD")).Append(": §Y").Append(Hundredfold(wing.CombatAttack)).Append("§W%\n");
		text.Append(localise("BATTLE_DEFENDMOD")).Append(": §Y").Append(Hundredfold(wing.CombatDefence)).Append("§W%\n");
		text.Append(CBattleTooltips.Modifiers(localise, wing.CombatModifiers));
		return Text(text.ToString());
	}

	/// <summary>The ship it shoots at, an enemy or a friend it mistook for one, while it is still in the battle.</summary>
	public static CSubUnit? Target(CCombat combat, CSubUnit ship)
	{
		if (ship.TargetId == 0)
		{
			return null;
		}
		foreach (CCombatSide side in new[] { combat.Attacker, combat.Defender })
		{
			foreach (CUnit fleet in side.Units)
			{
				foreach (CSubUnit other in fleet.SubUnits)
				{
					if (other.Id == ship.TargetId)
					{
						return other;
					}
				}
			}
		}
		return null;
	}

	private static string Strength(Func<string, string> localise, CFixedPoint share, bool hasMost)
	{
		// With no most the original divides to -1, which it writes as a tenth below nothing.
		CFixedPoint shown = hasMost ? share : new CFixedPoint(-100);
		return localise("CURR_COMB_STR") + "§Y" + CNumberText.Format(shown, 1) + "%§W\n";
	}

	private static string Organisation(Func<string, string> localise, CFixedPoint organisation)
	{
		return localise("CURR_COMB_ORG") + "§Y" + CNumberText.Format(organisation, 1) + "§W\n";
	}

	private static bool HasStrength(CUnit unit)
	{
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (subUnit.Stats[CSubUnitType.MaxStrength].Raw != 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A part over a whole as a percentage in thousandths: the share cut to a thousandth, times a hundred.</summary>
	private static CFixedPoint Share(long part, long whole)
	{
		return whole == 0 ? CFixedPoint.Zero : new CFixedPoint(part * Scale / whole * 100);
	}

	/// <summary>A multiplier as a percentage to two places: 1.25 is 125.00.</summary>
	private static string Hundredfold(CFixedPoint value)
	{
		return CNumberText.Format(new CFixedPoint(value.Raw * 100), 2);
	}

	private static CTooltip Text(string text)
	{
		return text.Length == 0 ? CTooltip.Empty : new CTooltip { Text = text };
	}
}
