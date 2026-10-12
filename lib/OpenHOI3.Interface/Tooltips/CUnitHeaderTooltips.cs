using System;
using System.Globalization;
using System.Text;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the header of a selected unit's panel says in a tooltip, and a row of a unit under
/// it, which shares most of it: what the unit is doing, its brigades, its level, its men, its
/// leader and its two bars. See <c>docs/mechanics/unitpanel.md</c>, section 10.
/// </summary>
public static class CUnitHeaderTooltips
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>A brigade's strength under this cannot move: a tenth.</summary>
	public static readonly CFixedPoint LeastStrength = new(100);

	/// <summary>A brigade's organisation under this cannot move: one.</summary>
	public static readonly CFixedPoint LeastOrganisation = CFixedPoint.One;

	/// <summary>A level's name and description, a theatre's first: the spellings are the game's.</summary>
	private static readonly string[] Levels = { "UNIT_THEATHRE", "UNIT_ARMYGROUP", "UNIT_ARMY", "UNIT_CORP", "UNIT_DIVISION", "UNIT_NAVY", "UNIT_AIR" };

	/// <summary>The header's element.</summary>
	public static CTooltip Header(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit, string element)
	{
		switch (element)
		{
			case "unit_activity":
				return new CTooltip { Text = Activity(localise, state, owner, unit) };
			case "remove_unit_from_selection_button":
				return Pair(localise, "DESELECT_UNIT", "DESELECT_UNIT_DRO");
			case "only_unit_from_selection_button":
				return Pair(localise, "OSELECT_UNIT", "OSELECT_UNIT_DRO");
			case "leader_button":
				return Pair(localise, "UW_LEADER_BUTTON_IRO", "UW_LEADER_BUTTON_DRO");
			case "leader_types":
				// The original's own: a key that names no text, both halves.
				return new CTooltip { Text = "xxx", Delayed = "xxx" };
			case "unitname":
				return Pair(localise, "UW_UNITNAMES_IRO", "UW_UNITNAMES_DRO");
			case "unitstrength":
				return new CTooltip { Text = Strength(localise, unit) };
			case "unitleader":
			case "leader_photo":
				return unit.Leader is { } leader ? new CTooltip { Text = CLeaderTooltip.For(localise, state, unit, leader) } : CTooltip.Empty;
			default:
				return Shared(localise, owner, unit, element);
		}
	}

	/// <summary>A row of a unit under the selected one.</summary>
	public static CTooltip ChildRow(Func<string, string> localise, CCountry owner, CUnit unit, string element)
	{
		return element switch
		{
			"order" => new CTooltip { Text = CUnitOrderTooltips.Description(localise, unit) },
			// An objective is an AI's to set, and there is none.
			"objective" => CTooltip.Empty,
			_ => Shared(localise, owner, unit, element),
		};
	}

	/// <summary>What the header and a row say alike: the brigades by their counters, the level and the two bars.</summary>
	private static CTooltip Shared(Func<string, string> localise, CCountry owner, CUnit unit, string element)
	{
		switch (element)
		{
			case "counter_type":
				return new CTooltip { Text = Brigades(localise, unit) };
			case "counter_size":
				return Level(localise, unit);
			case "org_bar":
				// The original adds what a brigade regains an hour, which is not built here.
				return new CTooltip
				{
					Text = localise("CURR_COMB_ORG") + "§Y" + CNumberText.Format(CUnitTotals.AverageOrganisation(unit), 1) + "§W\n",
				};
			case "str_bar":
				return new CTooltip { Text = localise("CURR_COMB_STR") + "§Y" + CNumberText.Format(StrengthShare(unit), 1) + "%§W\n" };
			default:
				return CTooltip.Empty;
		}
	}

	private static CTooltip Pair(Func<string, string> localise, string text, string delayed)
	{
		return new CTooltip { Text = localise(text), Delayed = localise(delayed) };
	}

	/// <summary>
	/// <c>unit_activity</c>: the first of these that holds - in a battle, falling back, held by
	/// an attack delay, on the march, too weak or too disorganised to move, unable to act -
	/// and otherwise nothing.
	/// </summary>
	public static string Activity(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		if (unit.IsFighting)
		{
			return localise(Screens.CUnitPanelText.IsAttacking(unit) ? "UV_ATTACKING" : "UV_UNDER_ATTACK");
		}
		if (unit.IsRetreating)
		{
			return localise("UV_RETREAT");
		}
		if (unit.AttackDelay > CFixedPoint.Zero)
		{
			return AttackDelay(localise, state, unit, "UW_ATTACK_DELAY");
		}
		if (unit.IsMoving)
		{
			return Arrival(localise, state, owner, unit);
		}
		if (unit.SubUnits.Count > 0)
		{
			CFixedPoint weakest = Weakest(unit, subUnit => subUnit.Strength);
			if (weakest < LeastStrength)
			{
				return CTextTemplate.Fill(localise("UV_NO_STRENGHT"),
					("STR", CNumberText.Format(weakest, 1)), ("NEED", CNumberText.Format(LeastStrength, 1)));
			}
			CFixedPoint worst = Weakest(unit, subUnit => subUnit.Organisation);
			if (worst < LeastOrganisation)
			{
				return CTextTemplate.Fill(localise("UV_NO_MORALE"),
					("ORG", CNumberText.Format(worst, 1)), ("NEED", CNumberText.Format(LeastOrganisation, 1)));
			}
			return "";
		}
		// Ours: what the original asks of a unit before it calls it unable to act was not read,
		// and a unit with nothing in it is the one the strip marks with its cross.
		return localise("UV_NOT_MOBILE");
	}

	private static CFixedPoint Weakest(CUnit unit, Func<CSubUnit, CFixedPoint> figure)
	{
		CFixedPoint weakest = figure(unit.SubUnits[0]);
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			weakest = CFixedPoint.Min(weakest, figure(subUnit));
		}
		return weakest;
	}

	/// <summary>A delay's line, with its hours and the date it runs out.</summary>
	public static string AttackDelay(Func<string, string> localise, CGameState state, CUnit unit, string key)
	{
		long hours = DelayHours(state, unit);
		return CTextTemplate.Fill(localise(key),
			("HOURS", hours.ToString(CultureInfo.InvariantCulture)), ("DATE", Date(localise, state.TotalHours + hours)));
	}

	/// <summary>
	/// The hours an attack delay has left, as the panel tells them: the delay whole, scaled by
	/// the hours attacked over the define's period where those are fewer, and never under one.
	/// </summary>
	public static long DelayHours(CGameState state, CUnit unit)
	{
		long hours = unit.AttackDelay.Raw / Scale;
		long period = state.Defines[Sim.Economy.CManpower.MilitarySection, Sim.Combat.CAttackDelay.DelayPeriod].Raw / Scale;
		if (unit.HoursAttacking > 0 && unit.HoursAttacking <= period)
		{
			hours = hours * unit.HoursAttacking / period;
		}
		return Math.Max(hours, 1);
	}

	/// <summary><c>ESTIMATE_ARIVAL</c>: the province it reaches next, and when.</summary>
	public static string Arrival(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		long hours = Math.Max(0, CMovement.HoursToNext(state, owner, unit));
		return CTextTemplate.Fill(localise("ESTIMATE_ARIVAL"),
			("DATE", Date(localise, state.TotalHours + hours)), ("PROV", state.Province(unit.Path[0]).Name));
	}

	/// <summary>
	/// A date as these tooltips write it: <c> 13:00, 1 January, 1936</c> - with a space in
	/// front, as the original's format has.
	/// </summary>
	public static string Date(Func<string, string> localise, long hour)
	{
		CDate date = CDate.FromDayNumber((int)(hour / CGameState.HoursPerDay));
		string month = date.MonthKey.Length == 0 ? "" : localise(date.MonthKey);
		return string.Create(CultureInfo.InvariantCulture,
			$" {hour % CGameState.HoursPerDay}:00, {date.Day} {month}, {date.Year}");
	}

	/// <summary><c>counter_type</c>: a line for each brigade, its counter and its name.</summary>
	public static string Brigades(Func<string, string> localise, CUnit unit)
	{
		var text = new StringBuilder();
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			text.Append(BrigadeName(subUnit)).Append('\n');
		}
		return text.ToString();
	}

	/// <summary>A brigade as these lists name it: its counter, drawn from <c>£</c> and its type, and its name.</summary>
	public static string BrigadeName(CSubUnit subUnit)
	{
		return "£" + subUnit.Type.Key + " " + subUnit.Name;
	}

	/// <summary><c>counter_size</c>: a lone brigade's name, or the name and description of the unit's level.</summary>
	public static CTooltip Level(Func<string, string> localise, CUnit unit)
	{
		if (CCounterText.Size(unit) == 0)
		{
			return Pair(localise, "UNIT_BRIGADE_NAME", "UNIT_BRIGADE_DESC");
		}
		return new CTooltip { Text = LevelName(localise, LevelOf(unit)), Delayed = LevelDescription(localise, LevelOf(unit)) };
	}

	/// <summary>A unit's level as the original numbers them: a theatre 0 to an air unit 6.</summary>
	public static int LevelOf(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Theatre => 0,
			EUnitKind.ArmyGroup => 1,
			EUnitKind.Army => 2,
			EUnitKind.Corps => 3,
			EUnitKind.Division => 4,
			EUnitKind.Navy => 5,
			_ => 6,
		};
	}

	public static string LevelName(Func<string, string> localise, int level)
	{
		return localise(level >= 0 && level < Levels.Length ? Levels[level] + "_NAME" : "NONE");
	}

	public static string LevelDescription(Func<string, string> localise, int level)
	{
		return localise(level >= 0 && level < Levels.Length ? Levels[level] + "_DESC" : "NONE");
	}

	/// <summary>
	/// <c>unitstrength</c>: a land unit's most men and each brigade's men against its most; a
	/// fleet's or an air unit's ships or wings that are short of their most, or that all are fit.
	/// </summary>
	public static string Strength(Func<string, string> localise, CUnit unit)
	{
		var text = new StringBuilder();
		if (CMovementSpeed.IsLand(unit))
		{
			long most = 0;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				most += subUnit.Stats[CSubUnitType.MaxStrength].Raw;
			}
			text.Append(CTextTemplate.Fill(localise("UW_TOTAL_STR_IRO"), ("VALUE", Screens.CBuilderText.Men(new CFixedPoint(most)))));
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				CFixedPoint full = subUnit.Stats[CSubUnitType.MaxStrength];
				string colour = subUnit.Strength.Raw * 2 < full.Raw ? "§R" : subUnit.Strength < full ? "§Y" : "§W";
				text.Append(BrigadeName(subUnit)).Append(" - ").Append(colour).Append(Screens.CBuilderText.Men(subUnit.Strength))
					.Append("§W/").Append(Screens.CBuilderText.Men(full)).Append('\n');
			}
			return text.ToString();
		}
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			long full = subUnit.Stats[CSubUnitType.MaxStrength].Raw;
			long share = full > 0 ? subUnit.Strength.Raw * Scale / full : Scale;
			if (share >= Scale)
			{
				continue;
			}
			string colour = share >= 900 ? " §G" : share >= 660 ? " §Y" : " §R";
			text.Append(BrigadeName(subUnit)).Append(colour).Append(StrengthFigure(subUnit)).Append("§W\n");
		}
		return text.Length > 0 ? localise("UW_FOLLOWING_HURT") + text : localise("UW_ALL_FIT");
	}

	/// <summary>
	/// A brigade's strength as the panel writes it: a land brigade's men, and otherwise the
	/// figure whole - which the original asks of the brigade itself, and was not read for a
	/// ship or a wing.
	/// </summary>
	public static string StrengthFigure(CSubUnit subUnit)
	{
		return subUnit.Type.Branch == EUnitBranch.Land
			? Screens.CBuilderText.Men(subUnit.Strength)
			: (subUnit.Strength.Raw / Scale).ToString(CultureInfo.InvariantCulture);
	}

	/// <summary><c>str_bar</c>'s figure: the strength over the most, as a percentage.</summary>
	public static CFixedPoint StrengthShare(CUnit unit)
	{
		long strength = 0;
		long most = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			strength += subUnit.Strength.Raw;
			most += subUnit.Stats[CSubUnitType.MaxStrength].Raw;
		}
		return most > 0 ? new CFixedPoint(strength * Scale / most * 100) : CFixedPoint.Zero;
	}
}
