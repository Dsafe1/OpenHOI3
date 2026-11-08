using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the brigades of a selected unit say in a tooltip: a fleet's crowding over its list, and
/// each brigade's width, whether it is a reserve, and otherwise its summary - which model it
/// is, its strength, organisation and experience, a ship's or a wing's speed and range, and
/// the level of each technology it carries. See <c>docs/mechanics/unitpanel.md</c>, section 10.
/// </summary>
public static class CBrigadeTooltips
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>
	/// The window round the rows: a fleet's <c>positioning</c> - how far its hulls are over the
	/// limit and what that costs it. Every other element is a row's.
	/// </summary>
	public static CTooltip Window(Func<string, string> localise, CGameState state, CUnit unit, string element)
	{
		if (element != "positioning" || unit.Kind != EUnitKind.Navy)
		{
			return CTooltip.Empty;
		}
		CDefines defines = state.Defines;
		long limit = defines[CManpower.MilitarySection, CNavalBattle.HullLimit].Raw;
		long each = defines[CManpower.MilitarySection, CNavalBattle.HullPenalty].Raw;
		long hull = 0;
		foreach (CSubUnit ship in unit.SubUnits)
		{
			hull += ship.Stats[CNavalBattle.Hull].Raw;
		}
		long total = Math.Clamp((hull - limit) * each / Scale, 0, defines[CManpower.MilitarySection, CNavalBattle.MostPenalty].Raw);
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("POS_STACK_PENALTY"),
				("MAX", CNumberText.Format(new CFixedPoint(limit), 0)),
				("EACH", CNumberText.Format(new CFixedPoint(each * 100), 0)),
				("TOTAL", CNumberText.Format(new CFixedPoint(total * 100), 1))),
		};
	}

	/// <summary>A brigade's row: its width, its reserve mark, and anywhere else its summary.</summary>
	public static CTooltip Row(Func<string, string> localise, Func<string, string?> find, CGameState state, CCountry owner,
		CSubUnit subUnit, string element)
	{
		switch (element)
		{
			case "upgrade_button":
				return new CTooltip { Text = UpgradeButton(localise, state, owner, subUnit) };
			case "unit_cw_strip":
				return new CTooltip { Text = CUnitStatusTooltips.Width(localise, subUnit.Type.Stats[CSubUnitType.CombatWidth]) };
			case "unitstatus_reserve":
				// Nothing mobilises a country here, so a reserve is always one not yet mobilised.
				return new CTooltip
				{
					Text = CTextTemplate.Fill(localise("UW_RESERVE_INFO"),
						("VAL", CNumberText.Format(new CFixedPoint(-owner.Modifier[CUnits.ReservesPenaltySize].Raw * 100), 0))),
				};
			default:
				return new CTooltip { Text = Summary(localise, find, state, owner, subUnit) };
		}
	}

	/// <summary>The upgrade button's words: the types the brigade may become, a line each.</summary>
	public static string UpgradeButton(Func<string, string> localise, CGameState state, CCountry player, CSubUnit subUnit)
	{
		if (subUnit.Type.UpgradeTargets.Count == 0)
		{
			return "";
		}
		var text = new StringBuilder(localise("UNIT_CAN_UPGRADE_TO"));
		foreach (CSubUnitType target in CBrigadeUpgrades.Targets(state, player, subUnit))
		{
			text.Append('\n').Append(localise(target.Key));
		}
		return text.ToString();
	}

	/// <summary>What a line changing a brigade's type says of itself, and the experience it will keep in its delayed half.</summary>
	public static CTooltip UpgradeLine(Func<string, string> localise, CBrigadeOrder order)
	{
		string from = order.Upgrade is { } upgrade ? localise(upgrade.From.Key) : "";
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("BRIGADE_UPGRADE_MIL_CON_INFO"), ("FROM", from), ("TO", localise(order.Type.Key))),
			Delayed = order.Experience.Raw > 0
				? CTextTemplate.Fill(localise("BRIGADE_UPGRADE_INHERITED_EXP"), ("EXP", CNumberText.Format(order.Experience, 2)))
				: "",
		};
	}

	/// <summary>
	/// A brigade's summary: its model in brackets, the pride of the fleet, its strength,
	/// organisation and experience, a ship's or a wing's speed and range, and its equipment.
	/// The original marks a model that is not the one its country builds today with <c>+*</c>;
	/// which one that is, is not built here, and nothing is marked.
	/// </summary>
	public static string Summary(Func<string, string> localise, Func<string, string?> find, CGameState state, CCountry owner,
		CSubUnit subUnit)
	{
		var text = new StringBuilder();
		text.Append("(§Y").Append(ModelName(localise, find, owner, subUnit)).Append("§W)\n");
		if (subUnit.IsPride)
		{
			text.Append(localise("PRIDE_OF_FLEET")).Append('\n');
		}
		bool isLand = subUnit.Type.Branch == EUnitBranch.Land;
		CFixedPoint most = subUnit.Stats[CSubUnitType.MaxStrength];
		text.Append(localise("SUBTT_STRENGTH")).Append(": §Y").Append(CUnitHeaderTooltips.StrengthFigure(subUnit)).Append("§W/")
			.Append(isLand ? Screens.CBuilderText.Men(most) : (most.Raw / Scale).ToString(CultureInfo.InvariantCulture) + "%").Append('\n');
		text.Append(localise("SUBTT_ORG")).Append(": §Y").Append(CNumberText.Trimmed(subUnit.Organisation)).Append("§W/")
			.Append(CNumberText.Trimmed(COrganisation.Ceiling(state, owner, subUnit))).Append("§W\n");
		long pride = subUnit.IsPride ? state.Defines[CManpower.MilitarySection, CReinforcement.PrideBonus].Raw : 0;
		text.Append(localise("SUBTT_EXP")).Append(": §Y")
			.Append(CNumberText.Format(new CFixedPoint(Math.Max(0, subUnit.Experience.Raw) + pride), 3)).Append("§W%\n");
		if (!isLand)
		{
			text.Append(StatLine(localise, "MAXIMUM_SPEED", subUnit.Type.Stats[CMovementSpeed.MaximumSpeed], 2, "KPH"));
			text.Append(StatLine(localise, "RANGE", subUnit.Type.Stats[CUnitOrders.Range], 0, "KM"));
		}
		text.Append(Equipment(localise, find, state, owner, subUnit));
		return text.ToString();
	}

	/// <summary>A figure's line in yellow - its label, the figure, its unit - and nothing for nothing.</summary>
	public static string StatLine(Func<string, string> localise, string label, CFixedPoint value, int decimals, string unitKey)
	{
		if (value.Raw == 0)
		{
			return "";
		}
		return localise(label) + " §Y" + CNumberText.Format(value, decimals) + "§W " + localise(unitKey) + " \n";
	}

	/// <summary>
	/// A line for each technology the brigade carries: its name and the level's - or yes and no
	/// for a technology without models - and how far the brigade is towards the next level where
	/// it is on its way.
	/// </summary>
	public static string Equipment(Func<string, string> localise, Func<string, string?> find, CGameState state, CCountry owner,
		CSubUnit subUnit)
	{
		var text = new StringBuilder();
		foreach ((string key, int level) in subUnit.TechnologyLevels)
		{
			if (!state.Technologies.TryGetValue(key, out CTechnology? technology))
			{
				continue;
			}
			text.Append(localise(technology.Name)).Append(" : §Y");
			if (technology.IsSingleLevel)
			{
				text.Append(localise(level >= 1 ? "YES" : "NO"));
			}
			else
			{
				text.Append(LevelName(localise, find, owner.Tag, technology, level));
			}
			long progress = subUnit.TechnologyProgress.GetValueOrDefault(key).Raw;
			if (progress > 0)
			{
				text.Append("§W(+").Append(CNumberText.Format(new CFixedPoint(progress * 100), 2)).Append("%)");
			}
			text.Append("§W\n");
		}
		return text.ToString();
	}

	/// <summary>
	/// The air builder's lines for a type: each technology on it, at the level its country has, by name and level. The
	/// original asks with no country at all, so no country's own name for a level comes in.
	/// </summary>
	public static List<string> DescriptionLines(Func<string, string> localise, Func<string, string?> find, CCountry country, CSubUnitType type)
	{
		var lines = new List<string>();
		foreach (CTechnology technology in type.Technologies)
		{
			int level = country.TechnologyLevels.GetValueOrDefault(technology.Name);
			lines.Add(localise(technology.Name) + " : §Y" + LevelName(localise, find, NoCountry, technology, level) + "§W");
		}
		return lines;
	}

	private static readonly CCountryTag NoCountry = new("---");

	/// <summary>
	/// The name of a level of a technology with models: the country's own text for it, the
	/// game's, or "Model" and the year - its start year at the first level and its offsets on
	/// from there. Nothing at level 0.
	/// </summary>
	public static string LevelName(Func<string, string> localise, Func<string, string?> find, CCountryTag tag, CTechnology technology,
		int level)
	{
		if (level <= 0)
		{
			return localise("NO");
		}
		string suffix = technology.Name + "_" + level.ToString(CultureInfo.InvariantCulture);
		if (find(tag.Value + "_" + suffix) is { } own)
		{
			return own;
		}
		if (find(suffix) is { } game)
		{
			return game;
		}
		int year = level == 1 ? technology.StartYear : (level - 2) * technology.AdditionalOffset + technology.FirstOffset;
		return localise("MODEL_TAG") + " " + year.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Which model of its type a brigade is: the country's name for it, the game's, or the
	/// type's name and the model's number in Roman numerals. The original also tries the name
	/// a faction's leader gives it, which is not built.
	/// </summary>
	public static string ModelName(Func<string, string> localise, Func<string, string?> find, CCountry owner, CSubUnit subUnit)
	{
		return ModelName(localise, find, owner, subUnit.Type, Math.Max(subUnit.HistoricalModel, 0));
	}

	/// <summary>The name of a numbered model of a type.</summary>
	public static string ModelName(Func<string, string> localise, Func<string, string?> find, CCountry owner, CSubUnitType type, int model)
	{
		string suffix = type.Key + "_" + model.ToString(CultureInfo.InvariantCulture);
		return find(owner.Tag.Value + "_" + suffix) ?? find(suffix)
			?? localise(type.Key) + " (" + CRomanNumerals.Format(model + 1) + ")";
	}
}
