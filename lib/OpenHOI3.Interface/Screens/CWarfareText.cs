using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.StrategicWarfare;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the Strategic Warfare Manager, <c>swm_window</c>, shows: the wars listed, each chart's lines and the common scale of a pair, and the words beside them. See
/// <c>docs/mechanics/strategicwarfare.md</c>, section 4.
/// </summary>
public static class CWarfareText
{
	public const string WindowName = "swm_window";
	public const string RowName = "swm_list_entry";

	/// <summary>The least a chart's scale may be: a hundredth, in 15-place fixed point.</summary>
	public const long LeastScale = 328;

	private const long ComparedIc = 8 * CFixed15.Scale;
	private const long ComparedMen = 2 * CFixed15.Scale;

	/// <summary>The tooltip of each of the window's parts that has one, by its name.</summary>
	private static readonly Dictionary<string, string> Tooltips = new()
	{
		["tab_naval"] = "SWM_TOOLTIP_TAB_NAVAL", ["tab_air"] = "SWM_TOOLTIP_TAB_AIR", ["tab_land"] = "SWM_TOOLTIP_TAB_LAND",
		["linechart_our_convoy_ic"] = "SWM_TOOLTIP_NAVAL_OUR_CONVOYS", ["linechart_our_enemy_ic"] = "SWM_TOOLTIP_NAVAL_OUR_ATTACKERS",
		["linechart_our_convoy_cargo"] = "SWM_TOOLTIP_NAVAL_OUR_CARGO", ["linechart_their_convoy_ic"] = "SWM_TOOLTIP_NAVAL_THEIR_CONVOYS",
		["linechart_their_enemy_ic"] = "SWM_TOOLTIP_NAVAL_THEIR_ATTACKERS", ["linechart_their_convoy_cargo"] = "SWM_TOOLTIP_NAVAL_THEIR_CARGO",
		["linechart_our_province"] = "SWM_TOOLTIP_AIR_OUR_PROVINCE", ["linechart_our_bombers"] = "SWM_TOOLTIP_AIR_OUR_BOMBERS",
		["linechart_their_province"] = "SWM_TOOLTIP_AIR_THEIR_PROVINCE", ["linechart_their_bombers"] = "SWM_TOOLTIP_AIR_THEIR_BOMBERS",
		["linechart_ic_loss_to_us"] = "SWM_TOOLTIP_LAND_IC_US", ["linechart_ic_loss_to_them"] = "SWM_TOOLTIP_LAND_IC_THEM",
		["linechart_man_loss_to_us"] = "SWM_TOOLTIP_LAND_MAN_US", ["linechart_man_loss_to_them"] = "SWM_TOOLTIP_LAND_MAN_THEM",
	};

	/// <summary>The words over a chart or a tab, or nothing for any other part: only the shown tab's charts answer.</summary>
	public static CTooltip Tooltip(Func<string, string> localise, string name)
	{
		return Tooltips.TryGetValue(name, out string? key) ? new CTooltip { Text = localise(key) } : new CTooltip();
	}

	/// <summary>Running wars first, in the countries' order among themselves.</summary>
	public static List<CWarfareRecord> Rows(CWarfareLedger ledger)
	{
		return ledger.Records.Where(record => !record.Ended).Concat(ledger.Records.Where(record => record.Ended)).ToList();
	}

	public static CDate DateOf(long hour) => CDate.FromDayNumber((int)(hour / CGameState.HoursPerDay));

	/// <summary>A selected war's line on a chart: a column of its ring, newest last, and its country's colour.</summary>
	public static List<CWarfareSeries> Series(CGameState state, IEnumerable<CWarfareRecord> selected, int category, int column, int duration)
	{
		var series = new List<CWarfareSeries>();
		foreach (CWarfareRecord record in selected)
		{
			List<long> values = record.Categories[category].Ring(duration).Series(column);
			values.Reverse();
			series.Add(new CWarfareSeries(values, state.Country(record.Enemy)?.Color ?? CColor.Gray));
		}
		return series;
	}

	/// <summary>The greatest of the values, and never less than a hundredth: what two charts beside each other share.</summary>
	public static long Scale(params IReadOnlyList<CWarfareSeries>[] charts)
	{
		long most = LeastScale;
		foreach (IReadOnlyList<CWarfareSeries> chart in charts)
		{
			foreach (CWarfareSeries line in chart)
			{
				foreach (long value in line.Values)
				{
					most = Math.Max(most, value);
				}
			}
		}
		return most;
	}

	public static long Total(IReadOnlyList<CWarfareSeries> chart) => chart.Sum(line => line.Values.Sum());

	/// <summary>A point's height in hundredths of the chart, whole.</summary>
	public static int Height(long value, long scale) => (int)((value * CFixed15.Scale / scale * 100) >> CFixed15.Places);

	public static string Figure(long fixed15, int decimals) => CNumberText.Format(new CFixed15(fixed15).ToThousandths(), decimals);

	/// <summary>The naval comparators: equal within eight IC; otherwise the second frame where the first figure is the less.</summary>
	public static int NavalFrame(long first, long second) => Math.Abs(first - second) <= ComparedIc ? 3 : first < second ? 2 : 1;

	public static int LandIcFrame(long first, long second) => LandFrame(first, second, ComparedIc);

	public static int LandMenFrame(long first, long second) => LandFrame(first, second, ComparedMen);

	/// <summary>The land comparators are the other way round.</summary>
	private static int LandFrame(long first, long second, long close) => Math.Abs(first - second) <= close ? 3 : second < first ? 2 : 1;
}
