using System;
using System.Linq;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Interface.BattlePlans;

/// <summary>What the battle plans window says and the rules of a file's name. See <c>docs/mechanics/battleplans.md</c>.</summary>
public static class CBattlePlanText
{
	public const string WindowName = "battleplans_window";
	public const string RowName = "battleplan_entry";
	public const string Extension = ".pln";

	public static readonly string[] ToolButtons = { "select", "arrows", "borders", "icons", "colors", "text", "erase" };

	private static readonly string[] ToolTips =
	{
		"BP_TOOLTIP_TOOL_SELECT", "BP_TOOLTIP_TOOL_ARROW", "BP_TOOLTIP_TOOL_BORDER", "BP_TOOLTIP_TOOL_ICON", "BP_TOOLTIP_TOOL_COLOR", "BP_TOOLTIP_TOOL_TEXT", "BP_TOOLTIP_TOOL_ERASE",
	};

	public static string ToolTip(int tool) => ToolTips[tool];

	/// <summary>A file row's name: after the last slash and before the first point.</summary>
	public static string FileNameShown(string path)
	{
		string name = path[(path.LastIndexOfAny(new[] { '/', '\\' }) + 1)..];
		int point = name.IndexOf('.');
		return point >= 0 ? name[..point] : name;
	}

	/// <summary>Whether a name typed may be a file's: no byte of 0x80 or above and no slash.</summary>
	public static bool IsLegalFileName(string name)
	{
		return name.All(character => character < 0x80 && character != '/' && character != '\\');
	}

	/// <summary>The name with <c>.pln</c> added, unless it ends so already - the point past the first character.</summary>
	public static string WithExtension(string name)
	{
		int point = name.LastIndexOf('.');
		return point > 0 && name[point..] == Extension ? name : name + Extension;
	}

	/// <summary>The row's share button: what it is, and delayed who a shared plan goes to.</summary>
	public static CTooltip ShareTooltip(Func<string, string> localise, CGameState state, CCountry author, bool shared)
	{
		var tooltip = new CTooltip { Text = localise("BP_TOOLTIP_SHARE_PLAN") };
		if (!shared)
		{
			return tooltip;
		}
		var delayed = new StringBuilder(localise("BP_SHARED_WITH"));
		var allies = CCountryLists.Allies(state, author);
		if (allies.Count == 0)
		{
			delayed.Append("\n- §Y").Append(localise("NONE")).Append("§!");
		}
		foreach (CCountry ally in allies)
		{
			delayed.Append("\n- @").Append(ally.Tag.Value).Append(' ').Append(localise(ally.Tag.Value));
		}
		return tooltip with { Delayed = delayed.ToString() };
	}
}
