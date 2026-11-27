using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the four windows a unit is ordered from say in a tooltip, as the original's write it: a
/// column's header its name and then what it means, the step buttons what Ctrl does to them, the
/// division designer's combined arms and its terrain summary, and the practical a unit teaches.
/// Anything else in them says nothing. See <c>docs/mechanics/builders.md</c>, section 11.
/// </summary>
public static class CBuilderTooltips
{
	public const string PracticalIcon = "on_complete_icon";
	public const string PracticalFigure = "on_complete_impact";
	public const string TerrainBonus = "terrainbonus";
	public const string CombinedArmsBonus = "combined_arms_bonus_value";
	public const string CombinedArmsIndicator = "combined_arms_indicator_";

	/// <summary>What the step buttons say a held key steps by.</summary>
	public const int LongStep = 10;

	private const string Description = "_desc";
	private const string White = "§W";

	private static readonly string[] StepButtons = { "parallel_minus", "parallell_plus", "serial_minus", "serial_plus" };

	/// <summary>The four figures of an adjuster, in the order they are written, each with its label and whether its colour goes the other way.</summary>
	private static readonly (string Kind, string Label, bool Hundred, bool Turned)[] Figures =
	{
		("attack", "UA_ATTACK", true, false), ("defence", "UA_DEFENCE", true, false),
		("movement", "UA_MOVEMENT", true, false), ("attrition", "UA_ATTRITION", false, true),
	};

	/// <summary>The four adjusters after the terrains, with the define each adds to the attack and the label each is written under.</summary>
	private static readonly (string Adjuster, string Penalty, string Label)[] Circumstances =
	{
		("river", "RIVER_CROSSING_PENALTY", "RIVER_ADJUSTER"), ("night", "BASE_NIGHT_PENALTY", "NIGHT_ADJUSTER"),
		("fort", "BASE_FORT_PENALTY", "FORT_ADJUSTER"), ("amphibious", "AMPHIBIOUS_LANDING_PENALTY", "AMPH_ADJUSTER"),
	};

	/// <summary>The designer's headers: its picker's buttons and its own totals.</summary>
	public static IReadOnlyDictionary<string, (string Text, string Delayed)> Designer { get; } = Columns(
		("sort_prio", "BUILD_PRIO"), ("sort_str", "BUILD_STR"), ("total_str", "BUILD_STR"), ("sort_sa", "BUILD_SA"),
		("total_sa", "BUILD_SA"), ("sort_ha", "BUILD_HA"), ("total_ha", "BUILD_HA"), ("sort_piercing", "BUILD_PIERCING"),
		("total_piercing", "BUILD_PIERCING"), ("sort_armored", "BUILD_ARMOR"), ("total_armored", "BUILD_ARMOR"),
		("sort_ad", "BUILD_AD"), ("total_ad", "BUILD_AD"), ("sort_org", "BUILD_ORG"), ("total_org", "BUILD_ORG"),
		("sort_cw", "BUILD_CW"), ("total_cw", "BUILD_CW"), ("sort_aa", "BUILD_AA"), ("total_aa", "BUILD_AA"),
		("sort_def", "BUILD_DEF"), ("total_def", "BUILD_DEF"), ("sort_tou", "BUILD_TOU"), ("total_tou", "BUILD_TOU"),
		("sort_speed", "BUILD_SPEED_DD"), ("total_speed", "BUILD_SPEED_DD"), ("sort_sup", "BUILD_SUP"),
		("total_sup", "BUILD_SUP"), ("sort_sc", "BUILD_SC"), ("total_sc", "BUILD_SC"), ("sort_fc", "BUILD_FC"),
		("total_oc", "BUILD_FC"), ("sort_ic", "BUILD_IC"), ("sort_mp", "BUILD_MP"), ("sort_time", "BUILD_TIME_DD"),
		("sort_sof", "BUILD_SOF"), ("total_sof", "BUILD_SOF"), ("sort_off", "BUILD_OFF_DD"));

	public static IReadOnlyDictionary<string, (string Text, string Delayed)> Ship { get; } = Columns(
		("sort_prio", "BUILD_PRIO"), ("sort_ic", "BUILD_IC"), ("sort_mp", "BUILD_MP"), ("sort_off", "BUILD_OFF_DD"),
		("sort_time", "BUILD_TIME_DD"), ("sort_sc", "BUILD_SC"), ("sort_fc", "BUILD_FC"), ("sort_org", "BUILD_ORG"),
		("sort_con_a", "BUILD_CON_A"), ("sort_sea_a", "BUILD_SEA_A"), ("sort_sub_a", "BUILD_SUB_A"),
		("sort_air_a", "BUILD_AIR_A"), ("sort_shore", "BUILD_SHORE"), ("sort_sea_d", "BUILD_SEA_D"),
		("sort_air_d", "BUILD_AIR_D"), ("sort_sur_det", "BUILD_SUR_DET"), ("sort_air_det", "BUILD_AIR_DET"),
		("sort_sub_det", "BUILD_SUB_DET"), ("sort_visibi", "BUILD_VISI"), ("sort_trans", "BUILD_TRANS"),
		("sort_range", "BUILD_RANGE"), ("sort_distance", "BUILD_DISTANCE"), ("sort_speed", "BUILD_SSPEED"),
		("sort_hull", "BUILD_HULL"), ("sort_pos", "BUILD_POS"));

	/// <summary>The air builder's, whose surface defence alone takes a plain key for its name.</summary>
	public static IReadOnlyDictionary<string, (string Text, string Delayed)> Air { get; } = With(Columns(
		("sort_prio", "BUILD_PRIO"), ("sort_ic", "BUILD_IC"), ("sort_mp", "BUILD_MP"), ("sort_time", "BUILD_TIME_DD"),
		("sort_sc", "BUILD_SC"), ("sort_fc", "BUILD_FC"), ("sort_org", "BUILD_ORG"), ("sort_sea_a", "BUILD_SEA_A"),
		("sort_air_a", "BUILD_AIR_A"), ("sort_air_d", "BUILD_AIR_D"), ("sort_sur_det", "BUILD_SUR_DET"),
		("sort_air_det", "BUILD_AIR_DET"), ("sort_trans", "BUILD_TRANS"), ("sort_range", "BUILD_RANGE"),
		("sort_speed", "BUILD_SSPEED"), ("sort_soft_a", "BUILD_SA"), ("sort_hard_a", "BUILD_HA"),
		("sort_strat_a", "BUILD_STRAT_A"), ("sort_off", "BUILD_OFF_DD")),
		"sort_surf_d", ("SURFACE_DEFENCE", "BUILD_SURF_D_DRO"));

	public static IReadOnlyDictionary<string, (string Text, string Delayed)> Brigade { get; } = Columns(
		("sort_prio", "BUILD_PRIO"), ("sort_str", "BUILD_STR"), ("sort_sa", "BUILD_SA"), ("sort_ha", "BUILD_HA"),
		("sort_piercing", "BUILD_PIERCING"), ("total_piercing", "BUILD_PIERCING"), ("sort_armored", "BUILD_ARMOR"),
		("total_armored", "BUILD_ARMOR"), ("sort_org", "BUILD_ORG"), ("sort_cw", "BUILD_CW"), ("sort_aa", "BUILD_AA"),
		("sort_def", "BUILD_DEF"), ("sort_tou", "BUILD_TOU"), ("sort_speed", "BUILD_SPEED_DD"), ("sort_sup", "BUILD_SUP"),
		("sort_sc", "BUILD_SC"), ("sort_fc", "BUILD_FC"), ("sort_ic", "BUILD_IC"), ("sort_mp", "BUILD_MP"),
		("sort_time", "BUILD_TIME_DD"), ("sort_sof", "BUILD_SOF"), ("sort_ad", "BUILD_AD"), ("sort_off", "BUILD_OFF_DD"));

	private static Dictionary<string, (string, string)> Columns(params (string Element, string Key)[] columns)
	{
		return columns.ToDictionary(column => column.Element, column => (column.Key + "_IRO", column.Key + "_DRO"),
			StringComparer.Ordinal);
	}

	private static Dictionary<string, (string, string)> With(Dictionary<string, (string, string)> columns,
		string element, (string, string) keys)
	{
		columns[element] = keys;
		return columns;
	}

	/// <summary>
	/// A window's element: a header its name and, after a moment, what it means; a step
	/// button that Ctrl and a click steps by ten; anything else nothing.
	/// </summary>
	public static CTooltip For(Func<string, string> localise, IReadOnlyDictionary<string, (string Text, string Delayed)> columns,
		string element)
	{
		if (columns.TryGetValue(element, out (string Text, string Delayed) keys))
		{
			return new CTooltip { Text = localise(keys.Text), Delayed = localise(keys.Delayed) };
		}
		if (StepButtons.Contains(element))
		{
			return new CTooltip
			{
				Text = CTextTemplate.Fill(localise("BUILD_ADD_REM_BUNDLE"), ("NUM", LongStep.ToString(CultureInfo.InvariantCulture))),
			};
		}
		return CTooltip.Empty;
	}

	/// <summary>
	/// The designer's element: its terrain summary, its combined arms bonus and each group's
	/// icon, and otherwise as <see cref="For"/>.
	/// </summary>
	/// <param name="underGroup">What an enhanced game writes under a group's name (<see cref="CDesignerLines.UnderGroup"/>); nothing where none is given.</param>
	public static CTooltip ForDesigner(Func<string, string> localise, IReadOnlyList<CCombinedArmsGroup> groups,
		Func<string> terrainSummary, string element, Func<CCombinedArmsGroup, string>? underGroup = null)
	{
		if (element == TerrainBonus)
		{
			return new CTooltip { Text = terrainSummary() };
		}
		if (element == CombinedArmsBonus)
		{
			return new CTooltip { Text = localise("BUILD_CA_BONUS_VALUE") };
		}
		if (element.StartsWith(CombinedArmsIndicator, StringComparison.Ordinal))
		{
			return Group(localise, groups, element[CombinedArmsIndicator.Length..], underGroup);
		}
		return For(localise, Designer, element);
	}

	/// <summary>
	/// A combined arms group's icon: the group the number after the name counts to, from one,
	/// by its name, and what it is for after a moment. The number is read as C's atoi reads it.
	/// </summary>
	private static CTooltip Group(Func<string, string> localise, IReadOnlyList<CCombinedArmsGroup> groups, string number,
		Func<CCombinedArmsGroup, string>? underGroup)
	{
		int index = Atoi(number);
		// The original reads its list without asking its length; past the end here is nothing.
		if (index <= 0 || index > groups.Count)
		{
			return CTooltip.Empty;
		}
		string key = groups[index - 1].Name;
		string under = underGroup?.Invoke(groups[index - 1]) ?? "";
		return new CTooltip
		{
			Text = under.Length == 0 ? localise(key) : localise(key) + "\n" + under,
			Delayed = localise(key + Description),
		};
	}

	/// <summary>C's atoi: spaces, a sign, digits, and nothing for anything else.</summary>
	public static int Atoi(string text)
	{
		int at = 0;
		while (at < text.Length && char.IsWhiteSpace(text[at]))
		{
			at++;
		}
		int sign = 1;
		if (at < text.Length && (text[at] == '-' || text[at] == '+'))
		{
			sign = text[at] == '-' ? -1 : 1;
			at++;
		}
		long value = 0;
		while (at < text.Length && text[at] is >= '0' and <= '9' && value < int.MaxValue)
		{
			value = value * 10 + (text[at] - '0');
			at++;
		}
		return (int)Math.Clamp(sign * value, int.MinValue, int.MaxValue);
	}

	/// <summary>
	/// The practical a unit teaches, which the in-game screen answers for wherever its icon
	/// and its figure are: its name and how much cheaper and quicker the country builds what
	/// it covers. Nothing for a practical the game has no category of.
	/// </summary>
	public static CTooltip Practical(Func<string, string> localise, CGameState state, CCountry country, string category)
	{
		if (category.Length == 0 || !state.TechnologyCategories.Any(known => known.Name == category))
		{
			return CTooltip.Empty;
		}
		long discount = CBuildCosts.CategoryDiscount(country, category);
		return new CTooltip
		{
			Text = localise(category) + " " + localise("TC_BUILD_COST_AND_TIME") + " §Y"
				+ CNumberText.Format(new CFixedPoint(discount * 100), 1) + "%" + White,
		};
	}

	/// <summary>
	/// What ground, a river, the night, forts and a landing do to a design, a line each that
	/// has something to say. Each is the brigades' own adjuster averaged, with a terrain's own
	/// attack and defence, or the define's penalty to the attack, counted once in the average.
	/// </summary>
	/// <param name="terrainsAttrition">
	/// An enhanced game's fix <c>shown_figures</c>: a terrain's own attrition is counted in its line as its attack and
	/// defence are, where the original leaves it out. Attrition is a percentage as it stands - a terrain's 2 is 2% a
	/// month, a technology's -1 a point less - so it is written without a hundredfold, as the original writes it.
	/// </param>
	public static string TerrainSummary(Func<string, string> localise, CDefines defines, IReadOnlyList<CTerrain> terrains,
		IReadOnlyList<CSubUnitStats> brigades, bool terrainsAttrition = false)
	{
		if (brigades.Count == 0)
		{
			return "";
		}
		var text = new StringBuilder();
		foreach (CTerrain terrain in terrains)
		{
			long[] sum = Sum(brigades, terrain.Name);
			sum[0] += terrain.Attack.Raw * brigades.Count;
			sum[1] += terrain.Defence.Raw * brigades.Count;
			if (terrainsAttrition)
			{
				sum[3] += terrain.Attrition.Raw * brigades.Count;
			}
			Line(text, localise(terrain.Name), Describe(localise, Averaged(sum, brigades.Count)));
		}
		foreach ((string adjuster, string penalty, string label) in Circumstances)
		{
			long[] sum = Sum(brigades, adjuster);
			sum[0] += defines[CManpower.MilitarySection, penalty].Raw * brigades.Count;
			Line(text, localise(label), Describe(localise, Averaged(sum, brigades.Count)));
		}
		return text.ToString();
	}

	private static void Line(StringBuilder text, string name, string figures)
	{
		if (figures.Length != 0)
		{
			text.Append(name).Append(": ").Append(figures).Append('\n');
		}
	}

	/// <summary>The brigades' adjuster of one name added up, figure by figure in the order of <see cref="Figures"/>.</summary>
	private static long[] Sum(IReadOnlyList<CSubUnitStats> brigades, string adjuster)
	{
		var sum = new long[Figures.Length];
		foreach (CSubUnitStats brigade in brigades)
		{
			if (!brigade.Adjusters.TryGetValue(adjuster, out CModifier? figures))
			{
				continue;
			}
			for (int index = 0; index < Figures.Length; index++)
			{
				sum[index] += figures[Figures[index].Kind].Raw;
			}
		}
		return sum;
	}

	/// <summary>A sum over the brigades, as the original divides one: by the count in thousandths, cut toward nothing.</summary>
	private static long[] Averaged(long[] sum, int count)
	{
		long divisor = (long)count * CFixedPoint.Scale;
		return sum.Select(figure => figure * CFixedPoint.Scale / divisor).ToArray();
	}

	/// <summary>One adjuster's line, its figures as it holds them.</summary>
	public static string Describe(Func<string, string> localise, CModifier adjuster)
	{
		return Describe(localise, Figures.Select(figure => adjuster[figure.Kind].Raw).ToArray());
	}

	/// <summary>
	/// An adjuster's four figures in one line: attack, defence and movement times a hundred,
	/// and attrition as it is, each left out at nothing. Attrition is a percentage as it stands, the other three
	/// shares of one; from 2026-11-25 to 2026-11-27 an enhanced game's fix wrote it times a hundred too, wrongly.
	/// </summary>
	public static string Describe(Func<string, string> localise, IReadOnlyList<long> figures)
	{
		var text = new StringBuilder();
		for (int index = 0; index < Figures.Length; index++)
		{
			(string _, string label, bool hundred, bool turned) = Figures[index];
			long value = hundred ? figures[index] * 100 : figures[index];
			if (value == 0)
			{
				continue;
			}
			if (text.Length > 0)
			{
				text.Append(", ");
			}
			text.Append(localise(label)).Append(' ');
			text.Append((turned ? -value : value) < 0 ? "§R" : "§G");
			if (value > 0)
			{
				text.Append('+');
			}
			text.Append(CNumberText.Format(new CFixedPoint(value), 1)).Append('%').Append(White);
		}
		return text.ToString();
	}
}
