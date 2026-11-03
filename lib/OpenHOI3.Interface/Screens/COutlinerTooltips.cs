using System;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the outliner's rows say in a tooltip: a unit where it stands, who leads it and where it goes; a land battle's
/// progress, leaders and brigades; a sea or an air battle's sides; an allied objective's askers. See
/// <c>docs/mechanics/outliner.md</c>, sections 5 to 7.
/// </summary>
public static class COutlinerTooltips
{
	public const string MovementElement = "movment";
	public const string FigureElement = "info_text";

	/// <summary>
	/// A unit's row, by the element under the mouse: a land unit's arrow while it moves, its figure, or else the row;
	/// a fleet's and an air unit's the row whatever the element.
	/// </summary>
	public static CTooltip Unit(Func<string, string> localise, CGameState state, CUnit unit, string element)
	{
		bool land = unit.Kind is not (EUnitKind.Navy or EUnitKind.Air);
		if (land && element == MovementElement && unit.IsMoving)
		{
			return new CTooltip { Text = CTextTemplate.Fill(localise("OL_UNIT_MOVING"),
				("TARGET", state.Province(unit.Path[0]).Name)) };
		}
		if (land && element == FigureElement && state.Country(unit.Owner) is { } owner)
		{
			return new CTooltip { Text = Shatter(localise, state, owner, unit) };
		}
		(string standing, string moving) = unit.Kind switch
		{
			EUnitKind.Navy => ("OL_NAVY", "OL_NAVY_MOVE"),
			EUnitKind.Air => ("OL_AIR", "OL_AIR_MOVE"),
			_ => ("OL_ARMY", "OL_ARMY_MOVE"),
		};
		string commander = unit.Leader is { } leader
			? CTextTemplate.Fill(localise("OL_COMMANDER"), ("COMMANDER", leader.Name))
			: localise("OL_NOCOMMANDER");
		string text = CTextTemplate.Fill(localise(standing), ("COMMANDER", commander));
		if (unit.Location > 0)
		{
			text = CTextTemplate.Fill(text, ("PROV", state.Province(unit.Location).Name));
		}
		if (unit.IsMoving)
		{
			text += "\n" + CTextTemplate.Fill(localise(moving), ("DEST", state.Province(unit.Path[^1]).Name));
		}
		return new CTooltip { Text = text };
	}

	/// <summary>
	/// <c>SHATTER_TOOLTIP</c>: the unit's strength against the most its brigades have had, the threshold under which it
	/// shatters, and what of that comes from its officers, experience and leader - each in percent to one decimal.
	/// </summary>
	public static string Shatter(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		long officers = Math.Min(CFixedPoint.Scale - CCombatDamage.MostShatterThreshold * owner.OfficerRatio.Raw / CFixedPoint.Scale,
			CCombatDamage.MostShatterThreshold);
		long experience = CUnitTotals.AverageExperienceWithPride(state, unit).Raw * CCombatDamage.ShatterExperienceWeight
			/ CFixedPoint.Scale;
		long skill = (unit.Leader?.Skill ?? 0) * CCombatDamage.ShatterSkillWeight;
		return CTextTemplate.Fill(localise("SHATTER_TOOLTIP"),
			("STRENGTH", Percent(CCombatDamage.StrengthShare(unit))),
			("THRESHOLD", Percent(CCombatDamage.ShatterThreshold(state, owner, unit))),
			("OFFICER", Percent(officers)),
			("EXP", Percent(experience)),
			("LEADER", Percent(skill)),
			("NAME", unit.Leader?.Name ?? ""));
	}

	/// <summary>
	/// A battle's row: a land battle's progress, leaders, enemy and brigades from the player's side; a sea or an air
	/// battle's name and each side's leader and count, the attacker's first and the player's green; a bombing's nothing.
	/// </summary>
	public static CTooltip Combat(Func<string, string> localise, CGameState state, CCombat combat, bool playerAttacks)
	{
		if (CBombing.IsBombing(combat.Kind))
		{
			return CTooltip.Empty;
		}
		if (combat.Kind != ECombatKind.Land)
		{
			return new CTooltip { Text = SeaOrAir(localise, state, combat, playerAttacks) };
		}
		CCombatSide ours = playerAttacks ? combat.Attacker : combat.Defender;
		CCombatSide theirs = playerAttacks ? combat.Defender : combat.Attacker;
		string enemy = theirs.Standing.Count > 0 && state.Country(theirs.Standing[0]) is { } first
			? CEventsAdjective(localise, first)
			: "NONE";
		string text = CTextTemplate.Fill(localise(playerAttacks ? "OL_LAND_COMBAT_ATTACK" : "OL_LAND_COMBAT_DEFEND"),
			("PROGRESS", CNumberText.Format(new CFixedPoint(CCombatShares.AttackerShare(combat) * 100), 1)),
			("OUR_LEAD", LeaderName(localise, ours)),
			("THEIR_LEAD", LeaderName(localise, theirs)),
			("ENEMY", enemy),
			("OUR_NUM", Brigades(ours.Front).ToString(System.Globalization.CultureInfo.InvariantCulture)),
			("OUR_RES", Brigades(ours.Reserves).ToString(System.Globalization.CultureInfo.InvariantCulture)),
			("THEIR_NUM", Brigades(theirs.Front).ToString(System.Globalization.CultureInfo.InvariantCulture)),
			("THEIR_RES", Brigades(theirs.Reserves).ToString(System.Globalization.CultureInfo.InvariantCulture)));
		return new CTooltip { Text = text };
	}

	/// <summary>
	/// An allied objective's row: who asked it of the player, or whom the player asked; then defend near the province
	/// where its controller is in the player's faction, else attack.
	/// </summary>
	public static CTooltip Objective(Func<string, string> localise, CGameState state, CCountryTag player, int province,
		bool askedOfPlayer)
	{
		var names = new List<string>();
		if (askedOfPlayer)
		{
			foreach ((int at, CCountryTag requester) in state.AlliedObjectives.AskedOf(player))
			{
				if (at == province && state.Country(requester) is { } country)
				{
					names.Add(country.Name);
				}
			}
		}
		else
		{
			foreach (CCountryTag asked in state.AlliedObjectives.AskedBy(player, province))
			{
				if (state.Country(asked) is { } country)
				{
					names.Add(country.Name);
				}
			}
		}
		string listed = List(localise, names);
		var text = new StringBuilder();
		text.Append(askedOfPlayer ? listed + localise("OL_AO_THEY_REQUEST") : localise("OL_AO_WE_REQUEST") + listed);
		CCountryTag? controller = state.Province(province).Controller;
		text.Append(SameFaction(state, controller, player) ? localise("OL_AO_DEFEND") : localise("OL_AO_ATTACK"));
		text.Append("§Y").Append(state.Province(province).Name).Append("§W.");
		return new CTooltip { Text = text.ToString() };
	}

	/// <summary>Countries named, each yellow, joined by commas and the last by " and " - with the key's own space doubled, as the original writes it.</summary>
	public static string List(Func<string, string> localise, IReadOnlyList<string> names)
	{
		var text = new StringBuilder();
		for (int i = 0; i < names.Count; i++)
		{
			if (i > 0)
			{
				text.Append(i == names.Count - 1 ? " " + localise("AND") + " " : ", ");
			}
			text.Append("§Y").Append(names[i]).Append("§W");
		}
		return text.ToString();
	}

	/// <summary>Whether two countries are in one faction - two in none counting as one, as the original compares them.</summary>
	private static bool SameFaction(CGameState state, CCountryTag? a, CCountryTag b)
	{
		if (a is not { } first || state.Country(first) is not { } one || state.Country(b) is not { } other)
		{
			return false;
		}
		return one.Faction == other.Faction;
	}

	private static string SeaOrAir(Func<string, string> localise, CGameState state, CCombat combat, bool playerAttacks)
	{
		string label = localise(combat.Kind == ECombatKind.Air ? "OL_WING" : "OL_SHIPS");
		var text = new StringBuilder(CCombatViewText.Name(localise, state, combat));
		Side(text, combat.Attacker, playerAttacks ? "§G" : "§R", label);
		text.Append("\n-");
		Side(text, combat.Defender, playerAttacks ? "§R" : "§G", label);
		return text.ToString();
	}

	private static void Side(StringBuilder text, CCombatSide side, string colour, string label)
	{
		if (CCombatLeaders.HighestRanked(side) is { } leader)
		{
			text.Append('\n').Append(leader.Name);
		}
		text.Append('\n').Append(colour).Append(COutlinerText.Count(side)).Append("§W ").Append(label);
	}

	private static string LeaderName(Func<string, string> localise, CCombatSide side)
	{
		return CCombatLeaders.HighestRanked(side) is { } leader ? leader.Name : localise("NO_COMMANDER");
	}

	private static int Brigades(List<CUnit> units)
	{
		int count = 0;
		foreach (CUnit unit in units)
		{
			count += unit.SubUnits.Count;
		}
		return count;
	}

	private static string CEventsAdjective(Func<string, string> localise, CCountry country)
	{
		return Events.CEventText.Adjective(localise, country);
	}

	private static string Percent(long thousandths)
	{
		return CNumberText.Format(new CFixedPoint(thousandths * 100), 1);
	}
}
