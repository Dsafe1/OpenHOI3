using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the unit panel's rows for a headquarters under an AI say: its three stances, the two powers and what the plan wants. See
/// <c>docs/mechanics/unitpanel.md</c>, section 9.
/// </summary>
public static class CPlanRowsText
{
	private static readonly string[] Ground = { "WITHDRAW", "PREPARE", "DEFEND", "ATTACK", "BLITZ" };
	private static readonly string[] AirOrNaval = { "PREPARE", "DEFEND", "ATTACK" };

	/// <summary>How many stances a list offers.</summary>
	public static int StanceCount(EPlanStanceKind kind) => kind == EPlanStanceKind.Ground ? Ground.Length : AirOrNaval.Length;

	public static int StanceOf(CUnitPlan plan, EPlanStanceKind kind)
	{
		return kind switch
		{
			EPlanStanceKind.Air => plan.AirStance,
			EPlanStanceKind.Naval => plan.NavalStance,
			_ => plan.Stance,
		};
	}

	/// <summary>A stance's key, or "" for a value that is none.</summary>
	public static string StanceKey(EPlanStanceKind kind, int stance)
	{
		string[] names = kind == EPlanStanceKind.Ground ? Ground : AirOrNaval;
		if (stance < 0 || stance >= names.Length)
		{
			return "";
		}
		return kind switch
		{
			EPlanStanceKind.Air => "STANCE_AIR_",
			EPlanStanceKind.Naval => "STANCE_NAVAL_",
			_ => "STANCE_",
		} + names[stance];
	}

	/// <summary>Whether a unit above this one has an agent, which greys its stance buttons: only the unit holding the agent may be pressed.</summary>
	public static bool AgentAboveItsOwn(CUnit unit)
	{
		for (CUnit? above = unit.Parent; above is not null; above = above.Parent)
		{
			if (above.Agent is not null)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The two bars in whole percents, the larger side about full.</summary>
	public static (int Ours, int Theirs) PowerBars(long our, long their)
	{
		long scale = Math.Max(our, their) * 1000 / 100000;
		if (scale > 0)
		{
			our = our * 1000 / scale;
			their = their * 1000 / scale;
		}
		return ((int)(our / 1000), (int)(their / 1000));
	}

	private static IEnumerable<(int Count, CSubUnitType Type)> Short(CGameState state, CUnit unit)
	{
		int[] group = CPlanWants.GroupDifference(state, unit);
		return state.SubUnitTypes.Values.OrderBy(type => type.DatabaseIndex)
			.Where(type => group[type.DatabaseIndex] > 0)
			.Select(type => (group[type.DatabaseIndex], type));
	}

	/// <summary>"We need: 3 and a counter, ..." with each type's counter drawn, or "We need Nothing".</summary>
	public static string Desired(Func<string, string> localise, CGameState state, CUnit unit)
	{
		string list = string.Join(", ", Short(state, unit).Select(entry =>
			entry.Count.ToString(CultureInfo.InvariantCulture) + " §W£" + entry.Type.Key + "§b"));
		return localise("WE_NEED") + (list.Length > 0 ? ": " + list : " " + localise("TRADE_NOTHING"));
	}

	public static CTooltip DesiredTooltip(Func<string, string> localise, CGameState state, CUnit unit, string element)
	{
		if (element != "desired")
		{
			return CTooltip.Empty;
		}
		string lines = string.Concat(Short(state, unit).Select(entry =>
			entry.Count.ToString(CultureInfo.InvariantCulture) + " " + localise(entry.Type.Key) + "\n"));
		return new CTooltip { Text = localise("AGENT_NEED_DESC") + ":\n" + lines };
	}

	/// <summary>The mark a parent's row gives a unit with an aggression of its own: red over 0.1, blue under -0.1, grey between.</summary>
	public static string AggressionMark(CUnit unit)
	{
		if (!unit.HasOwnAggression)
		{
			return "";
		}
		return unit.Aggression.Raw > 100 ? "§R*§!" : unit.Aggression.Raw < -100 ? "§B*§!" : "§g*§!";
	}
}
