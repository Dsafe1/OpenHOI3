using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Events;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the order of battle browser's rows say, what a row may have dropped on it and what a drop posts. See
/// <c>docs/mechanics/oobbrowser.md</c>.
/// </summary>
public static class COobBrowserText
{
	public const string WindowName = "oobbrowser";
	public const string SplitName = "split_window";
	public const string UnitRowName = "oob_unit_entry";
	public const string LeaderRowName = "oob_leader_entry";

	/// <summary>How far a row stands in for each level of the tree.</summary>
	public const int Indent = 16;

	/// <summary>How much less than the screen the window stands, and the list than the window.</summary>
	public const int ScreenMargin = 260;

	public const int ListMargin = 90;

	public const int WindowWidth = 373;

	/// <summary>How far the mouse goes before a press is a drag, in pixels; more than this on either axis.</summary>
	public const float DragThreshold = 2f;

	/// <summary>How long a closed row is hovered over in a drag before it opens.</summary>
	public const double HoverOpens = 1.0;

	private const int ShownBrigades = 5;

	private static readonly string[] LevelWords =
	{
		"", "NUM_GROUPS", "NUM_ARMIES", "NUM_CORPS", "NUM_DIVISONS", "NUM_SHIPS", "NUM_WINGS", "NUM_UNKNOWNS",
	};

	public static string Title(Func<string, string> localise, CCountry player)
	{
		return CTextTemplate.Fill(localise("OOB_LABEL"), ("COUNTRY_ADJ", CEventText.Adjective(localise, player)));
	}

	/// <summary>A row's second line: the counter of each of the first five brigades, and dots where there are more.</summary>
	public static string UnitInfo(CUnit unit)
	{
		var text = new StringBuilder();
		foreach (CSubUnit subUnit in unit.SubUnits.Take(ShownBrigades))
		{
			text.Append('£').Append(subUnit.Type.Key).Append(' ');
		}
		if (unit.SubUnits.Count > ShownBrigades)
		{
			text.Append("...");
		}
		return text.ToString();
	}

	/// <summary>The frame of <c>counter_size</c> by the unit's level; a division's by whether it has more than one brigade.</summary>
	public static int SizeFrame(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Theatre => 6,
			EUnitKind.ArmyGroup => 5,
			EUnitKind.Army => 4,
			EUnitKind.Corps => 3,
			EUnitKind.Division => unit.SubUnits.Count == 1 ? 1 : 2,
			EUnitKind.Navy => 2,
			_ => 1,
		};
	}

	/// <summary>Over the leader's picture his own tooltip, where he exists; anywhere else in the row the unit's.</summary>
	public static CTooltip Tooltip(Func<string, string> localise, CGameState state, CUnit unit, string element)
	{
		if (element == "leader_photo")
		{
			return unit.Leader is { } leader ? new CTooltip { Text = CLeaderTooltip.For(localise, state, leader) } : CTooltip.Empty;
		}
		var text = new StringBuilder();
		if (unit.Leader is { } commander)
		{
			text.Append("§Y").Append(commander.Name).Append("§W\n");
		}
		text.Append(unit.Name).Append('\n');
		text.Append(state.Province(unit.Location).Name).Append('\n');
		foreach (IGrouping<string, CSubUnit> type in unit.SubUnits.GroupBy(subUnit => subUnit.Type.Key))
		{
			text.Append(type.Count().ToString(CultureInfo.InvariantCulture)).Append(" £").Append(type.Key).Append(' ');
			foreach (CSubUnit subUnit in type)
			{
				text.Append('{').Append(Bar(subUnit).ToString("00", CultureInfo.InvariantCulture));
			}
			text.Append('\n');
		}
		return new CTooltip { Text = text.ToString(), Delayed = UnderCommand(localise, unit) };
	}

	/// <summary>A brigade's strength in tenths, cut, for a bar; nothing where its type has no most.</summary>
	public static int Bar(CSubUnit subUnit)
	{
		long most = subUnit.Stats[CSubUnitType.MaxStrength].Raw;
		return most == 0 ? 0 : (int)(subUnit.Strength.Raw * 1000 / most * 10 / 1000);
	}

	private static string UnderCommand(Func<string, string> localise, CUnit unit)
	{
		var counts = new int[LevelWords.Length];
		var brigades = new List<CSubUnit>(unit.SubUnits);
		foreach (CUnit below in Below(unit))
		{
			counts[(int)below.Kind]++;
			brigades.AddRange(below.SubUnits);
		}
		var text = new StringBuilder(localise("OOB_UNDER_COMMAND"));
		for (int level = 0; level < counts.Length; level++)
		{
			if (counts[level] > 0)
			{
				text.Append(counts[level].ToString(CultureInfo.InvariantCulture)).Append(' ').Append(localise(LevelWords[level])).Append('\n');
			}
		}
		text.Append('\n');
		foreach (IGrouping<string, CSubUnit> type in brigades.GroupBy(subUnit => subUnit.Type.Key))
		{
			text.Append(type.Count().ToString(CultureInfo.InvariantCulture)).Append(" £").Append(type.Key).Append("  ");
		}
		return text.ToString();
	}

	private static IEnumerable<CUnit> Below(CUnit unit)
	{
		foreach (CUnit child in unit.Children)
		{
			yield return child;
			foreach (CUnit deeper in Below(child))
			{
				yield return deeper;
			}
		}
	}

	/// <summary>What the floating row of a dragged leader says of him.</summary>
	/// <param name="withinTheTable">The leader tooltip's, an enhanced game's fix <c>shown_figures</c>.</param>
	public static string LeaderInfo(Func<string, string> localise, CLeader leader, bool withinTheTable = false)
	{
		var text = new StringBuilder();
		text.Append(localise("LEADER_SKILL")).Append(" §Y").Append(leader.Skill.ToString(CultureInfo.InvariantCulture)).Append("§b ")
			.Append(localise("LEADER_XP")).Append(" §Y")
			.Append(CNumberText.Format(new CFixedPoint(CLeaderTooltip.ExperienceShare(leader, withinTheTable) * 100000 / 1000), 2)).Append('%');
		return text.ToString();
	}

	/// <summary>Whether a dragged unit may go under the row's.</summary>
	public static bool MayTakeUnit(CGameState state, CUnit row, CUnit dragged)
	{
		return !ReferenceEquals(row, dragged) && CDeployment.MayTakeSubordinate(state, row, dragged);
	}

	/// <summary>
	/// Whether a dragged brigade, ship or wing may join the row's unit - in the same province, but for one dragged out of the
	/// deploy menu, which the test asks anywhere (<c>docs/mechanics/oobbrowser.md</c>, section 6).
	/// </summary>
	public static bool MayTakeSubUnit(CGameState state, CCountry country, CUnit row, CSubUnit dragged, bool sameProvince = true)
	{
		return !ReferenceEquals(row, dragged.Unit) && CSubUnitTransfer.MayTake(state, country, row, dragged, sameProvince);
	}

	/// <summary>Whether a dragged leader may take the row's unit, and where it has a leader, he the dragged leader's.</summary>
	public static bool MayTakeLeader(CUnit row, CLeader dragged)
	{
		if (ReferenceEquals(row, dragged.Unit) || !CLeaders.MayCommand(dragged, row, false))
		{
			return false;
		}
		return row.Leader is not { } old || dragged.Unit is not { } other || CLeaders.MayCommand(old, other, false);
	}

	/// <summary>A drop of a leader on a row, a swap where the row's unit has one.</summary>
	public static IEnumerable<CCommand> LeaderDrop(CCountryTag owner, CUnit row, CLeader dragged)
	{
		CUnit? other = dragged.Unit;
		CLeader? old = row.Leader;
		if (row.Lender is not null || other?.Lender is not null)
		{
			yield break;
		}
		yield return new CAssignLeaderCommand(owner, row.Id, dragged.Id, onlyFree: false);
		if (old is not null && other is not null)
		{
			yield return new CAssignLeaderCommand(owner, other.Id, old.Id, onlyFree: false);
		}
	}
}
