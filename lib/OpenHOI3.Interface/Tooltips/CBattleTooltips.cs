using System;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the window of a battle says in a tooltip by the element's name: its terrain, its width,
/// the retreat and close buttons, a fleet's positioning, the arrow between the tactics, each
/// side's tactic and leader, and the icons of the modifiers in play; with the lines a tactic and a
/// combat modifier are written in, which the entries share. See
/// <c>docs/mechanics/combatview.md</c>, section 12.
/// </summary>
public static class CBattleTooltips
{
	/// <summary>The window's own elements.</summary>
	public static CTooltip Window(Func<string, string> localise, CGameState state, CCombat combat, string element)
	{
		switch (element)
		{
			case "closebutton":
				// The in-game screen's, before any window's: every close button nobody claims.
				return Text(localise("CLOSE"));
			case "combat_terrain":
				return state.Province(combat.Province).Terrain is { } terrain ? Text(localise(terrain.Name)) : CTooltip.Empty;
			case "combat_width":
				return Text(localise("COMBAT_WIDTH"));
			case "retreat_button":
				return Text(localise("COMBAT_RETREAT"));
			case "combat_attack_indicator" when combat.Kind == ECombatKind.Land:
				return Text(localise(CCombatViewText.IndicatorFrame(combat) switch
				{
					CCombatViewText.DefenderCountered => "DEFENDER_COUNTERED",
					CCombatViewText.AttackerCountered => "ATTACKER_COUNTERED",
					_ => "NONE_COUNTERED",
				}));
			default:
				return CTooltip.Empty;
		}
	}

	/// <summary>A side's half: a fleet's positioning, its tactic on land, and its leader.</summary>
	public static CTooltip Side(Func<string, string> localise, CGameState state, CCombat combat, CCombatSide side, string element)
	{
		switch (element)
		{
			case "positioning":
				return Text(localise("FLEET_POSITIONING"));
			case "tactic_name" or "tactic_picture" when combat.Kind == ECombatKind.Land:
				return side.Tactic is { } tactic ? Text(Tactic(localise, state, tactic)) : CTooltip.Empty;
			case "leader_name" or "leader_photo":
				return Leader(localise, state, combat, side);
			default:
				return CTooltip.Empty;
		}
	}

	/// <summary>
	/// A side's highest ranked leader's own tooltip, and where his skill is above the other
	/// side's leader's, <c>LEADER_INITIATIVE</c> after a moment. Nothing with no leader.
	/// </summary>
	public static CTooltip Leader(Func<string, string> localise, CGameState state, CCombat combat, CCombatSide side)
	{
		if (CCombatLeaders.HighestRanked(side) is not { } leader)
		{
			return CTooltip.Empty;
		}
		CUnit? unit = leader.Unit;
		foreach (CUnit each in side.Units)
		{
			if (each.Leader == leader)
			{
				unit = each;
				break;
			}
		}
		string text = unit is null ? "" : CLeaderTooltip.For(localise, state, unit, leader);
		bool initiative = leader.Skill > CCombatLeaders.Skill(combat.Other(side));
		return new CTooltip { Text = text, Delayed = initiative ? localise("LEADER_INITIATIVE") : "" };
	}

	/// <summary>An icon of the modifiers in play: the modifier's name, and after a moment what it is.</summary>
	public static CTooltip ModifierIcon(Func<string, string> localise, string key)
	{
		return new CTooltip { Text = localise(key), Delayed = localise(key + "_desc") };
	}

	/// <summary>
	/// A tactic's own text: its figures and a line break; what counters it; and what it
	/// counters, a line each, in the order of the tactics file.
	/// </summary>
	public static string Tactic(Func<string, string> localise, CGameState state, CCombatTactic tactic)
	{
		var text = new StringBuilder();
		text.Append(TacticFigures(localise, tactic, true, true, true)).Append('\n');
		if (tactic.CounteredByNumber > 0)
		{
			foreach (CCombatTactic other in state.Tactics)
			{
				if (other.Number == tactic.CounteredByNumber)
				{
					text.Append(CTextTemplate.Fill(localise("TACTIC_COUNTERED_BY"), ("NAME", localise(other.Name))));
					break;
				}
			}
		}
		var countered = new List<string>();
		foreach (CCombatTactic other in state.Tactics)
		{
			if (other.CounteredByNumber == tactic.Number)
			{
				countered.Add("§Y  " + localise(other.Name) + "§!\n");
			}
		}
		if (countered.Count > 0)
		{
			text.Append(localise("TACTIC_COUNTERS"));
			if (countered.Count > 1)
			{
				text.Append('\n');
			}
			text.Append(string.Concat(countered));
		}
		return text.ToString();
	}

	/// <summary>
	/// A tactic's figures, each a line where it is not nothing: its width always where asked, the
	/// attacker's damage and speed for the attacking side, the defender's damage for the defending.
	/// </summary>
	public static string TacticFigures(Func<string, string> localise, CCombatTactic tactic, bool width, bool attackers,
		bool defenders)
	{
		var text = new StringBuilder();
		if (width)
		{
			Figure(text, localise, "COMEV_COMBATWIDTH", tactic.Width.Raw, CNumberText.Format(tactic.Width, 0), "§W\n");
		}
		if (attackers)
		{
			Figure(text, localise, "COMEV_DAMAGE_ATTACKER", tactic.Attacker.Raw, Percent(tactic.Attacker), "%§W\n");
		}
		if (defenders)
		{
			Figure(text, localise, "COMEV_DAMAGE_DEFENDER", tactic.Defender.Raw, Percent(tactic.Defender), "%§W\n");
		}
		if (attackers)
		{
			Figure(text, localise, "COMEV_MOVEMENTSPEED", tactic.Speed.Raw, Percent(tactic.Speed), "%§W\n");
		}
		return text.ToString();
	}

	private static void Figure(StringBuilder text, Func<string, string> localise, string key, long raw, string figure, string suffix)
	{
		if (raw == 0)
		{
			return;
		}
		text.Append(localise(key)).Append(": ").Append(raw > 0 ? "§G+" : "§R").Append(figure).Append(suffix);
	}

	private static string Percent(CFixedPoint value)
	{
		return CNumberText.Format(new CFixedPoint(value.Raw * 100), 0);
	}

	/// <summary>
	/// A unit's, a ship's or a wing's combat modifiers, a line each: the modifier's name, and its
	/// figure - or, where it does not do the same to attack and defence, both, the attack's first.
	/// </summary>
	public static string Modifiers(Func<string, string> localise, IEnumerable<CCombatModifier> modifiers)
	{
		var text = new StringBuilder();
		foreach (CCombatModifier modifier in modifiers)
		{
			text.Append(localise(Key(modifier.Kind))).Append(": ");
			if (modifier.Attack == modifier.Defence)
			{
				text.Append(ModifierFigure(modifier.Attack)).Append("§W%\n");
			}
			else
			{
				text.Append(ModifierFigure(modifier.Attack)).Append("§W%/").Append(ModifierFigure(modifier.Defence)).Append("§W%\n");
			}
		}
		return text.ToString();
	}

	/// <summary>A modifier's figure: red at nothing or less, green with a plus above, as a percentage to two places.</summary>
	public static string ModifierFigure(CFixedPoint value)
	{
		return (value.Raw < 1 ? "§R" : "§G+") + CNumberText.Format(new CFixedPoint(value.Raw * 100), 2);
	}

	/// <summary>A modifier's key, its <c>BM_</c> name.</summary>
	public static string Key(ECombatModifier kind)
	{
		foreach ((ECombatModifier each, string key) in CCombatModifierKeys.All)
		{
			if (each == kind)
			{
				return key;
			}
		}
		return "";
	}

	private static CTooltip Text(string text)
	{
		return text.Length == 0 ? CTooltip.Empty : new CTooltip { Text = text };
	}
}
