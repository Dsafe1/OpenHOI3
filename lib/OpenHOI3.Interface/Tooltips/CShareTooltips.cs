using System;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What a slider says in a tooltip: what its share would do at the slider's position, and on
/// the knob whether a right click locks or unlocks it. The production screen's six and the
/// technology screen's four. See <c>docs/mechanics/sliders.md</c>, section 6.
/// </summary>
public static class CShareTooltips
{
	private const string White = "§W";
	private const string GreenPlus = "§G+";
	private const string WarDemand = "war_consumer_goods_demand";
	private const string PeaceDemand = "peace_consumer_goods_demand";
	private const string DissentKind = "dissent";
	private const string GlobalSupplies = "global_supplies";
	private const string TechnologySupplies = "ic_to_supplies";
	private const string ReinforcementBonus = "reinforcement_bonus";
	private const string OfficerRecruitment = "officer_recruitment";

	private static readonly CFixed15 Hundred = CFixed15.FromInt(100);

	/// <summary>
	/// The production screen's share at a slider's position: the amount over the country's
	/// IC, rounded to the nearest 32768th - the original divides as floats and rounds after.
	/// With no IC, the amount itself.
	/// </summary>
	public static CFixed15 ProductionShareAt(CFixedPoint amount, int totalIc)
	{
		long over = totalIc > 0 ? (long)totalIc * CFixedPoint.Scale : CFixedPoint.Scale;
		return new CFixed15(RoundedQuotient(amount.Raw * CFixed15.Scale, over));
	}

	/// <summary>
	/// The technology screen's share at a slider's position: the amount made a 48.15 number,
	/// rounded, and then divided by the country's leadership in 48.15, cut. With no leadership,
	/// the amount itself.
	/// </summary>
	public static CFixed15 LeadershipShareAt(CFixedPoint amount, CFixedPoint leadership)
	{
		var position = new CFixed15(RoundedQuotient(amount.Raw * CFixed15.Scale, CFixedPoint.Scale));
		CFixed15 total = CFixed15.FromThousandths(leadership);
		return total.Raw == 0 ? position : new CFixed15((position.Raw << CFixed15.Places) / total.Raw);
	}

	/// <summary>A quotient rounded to the nearest whole, half away from nought.</summary>
	private static long RoundedQuotient(long numerator, long denominator)
	{
		long twice = numerator * 2 / denominator;
		return twice >= 0 ? (twice + 1) / 2 : -((-twice + 1) / 2);
	}

	/// <summary>The knob's line: a right click locks it, or unlocks it.</summary>
	public static string LockLine(Func<string, string> localise, bool locked)
	{
		return localise(locked ? "SLIDER_LOCKED_TOOLTIP" : "SLIDER_UNLOCKED_TOOLTIP");
	}

	/// <summary>What one of the six shares of IC would do at a share, as its slider says it.</summary>
	public static string Production(Func<string, string> localise, CGameState state, CCountry country,
		EProductionCategory category, CFixed15 share)
	{
		return category switch
		{
			EProductionCategory.ConsumerGoods => ConsumerGoods(localise, state, country, share),
			EProductionCategory.Production => Production(localise, country, share),
			EProductionCategory.Supplies => Supplies(localise, state, country, share),
			EProductionCategory.Reinforcement => Reinforcement(localise, country, share),
			EProductionCategory.Upgrades => Upgrades(localise, country, share),
			_ => "",                                         // lend-lease says nothing
		};
	}

	/// <summary>What one of the four shares of leadership would do at a share, as its slider says it.</summary>
	public static string Leadership(Func<string, string> localise, CGameState state, CCountry country,
		ELeadershipCategory category, CFixed15 share)
	{
		CFixed15 leadership = CFixed15.FromThousandths(country.TotalLeadership) * share;
		switch (category)
		{
			case ELeadershipCategory.Officers:
			{
				CFixed15 made = leadership * Define(state, CLeadership.LeadershipToOfficers)
					* (CFixed15.FromThousandths(country.Modifier[OfficerRecruitment]) + CFixed15.One);
				string text = CTextTemplate.Fill(localise("DCG_OFFICERS"), ("VAL", Rounded(made, 1)));
				string sources = CModifierText.Sources(localise, country.ModifierSources, OfficerRecruitment);
				return sources.Length > 2 ? text + "\n" + sources : text;
			}
			case ELeadershipCategory.Diplomacy:
				// Nothing here influences anybody, so nothing is spent on it.
				return CTextTemplate.Fill(localise("DCG_DIPLOMACY"),
					("VAL", Rounded(leadership * Define(state, CLeadership.LeadershipToDiplomacy), 2)),
					("UPKEEP", Rounded(CFixed15.Zero, 2)));
			case ELeadershipCategory.Espionage:
				return CTextTemplate.Fill(localise("DCG_ESPIONAGE"),
					("VAL", Rounded(leadership * Define(state, CLeadership.LeadershipToSpies), 2)));
			default:
			{
				int count = country.ResearchQueue.Count;
				CFixed15 ratio = count == 0 ? CFixed15.One : new CFixed15((leadership.Raw << CFixed15.Places) / CFixed15.FromInt(count).Raw);
				string percent = CColouredText.In(ratio < CFixed15.One ? CColouredText.Red : CColouredText.Green)
					+ Rounded(ratio * Hundred, 2) + "%" + White;
				return CTextTemplate.Fill(localise("DCG_RESEARCH"),
					("SPEED", Rounded(leadership, 2)),
					("COUNT", count.ToString(CultureInfo.InvariantCulture)),
					("PERCENT", percent));
			}
		}
	}

	private static CFixed15 Define(CGameState state, string name)
	{
		return CFixed15.FromThousandths(state.Defines[CResources.EconomySection, name]);
	}

	/// <summary>
	/// What the country needs on consumer goods, what its standing army asks, the sources of
	/// the demand, and where it moves, what a day would do to dissent at this share.
	/// </summary>
	private static string ConsumerGoods(Func<string, string> localise, CGameState state, CCountry country, CFixed15 share)
	{
		CFixed15 need = CFixed15.FromThousandths(CProduction.ConsumerGoodsDemand(state, country))
			+ CFixed15.FromThousandths(country.Modifier[DissentKind]);
		if (need < CFixed15.Zero)
		{
			need = CFixed15.Zero;
		}
		var text = new System.Text.StringBuilder();
		text.Append('\n').Append(CTextTemplate.Fill(localise("DCG_TOTAL_NEED"), ("PERC", Rounded(need * Hundred, 2)))).Append('\n');

		// The standing army's term of the demand, by the brigades in service the hour counted - those waiting to be placed
		// among them - as the demand itself takes it (production.md, section 3).
		text.Append(CTextTemplate.Fill(localise("DCG_TROOPS_GOODS"),
			("IC", Rounded(CFixed15.FromThousandths(CProduction.StandingArmy(country)) * Hundred, 2)),
			("TROOPS", country.LandBrigadesInService.ToString(CultureInfo.InvariantCulture)))).Append('\n');
		text.Append(CModifierText.Sources(localise, country.ModifierSources, state.IsAtWar(country.Tag) ? WarDemand : PeaceDemand));

		CFixed15 change = CProduction.DissentChange(state, country, share);
		if (change.Raw != 0)
		{
			string colour = change.Raw < 0 ? CColouredText.In(CColouredText.Green) : CColouredText.In(CColouredText.Red) + "+";
			text.Append('\n').Append(CTextTemplate.Fill(localise("DCG_DISSENT"), ("VAL", colour + Rounded(change, 2) + White))).Append(' ');
		}
		return text.ToString();
	}

	/// <summary>How much of the build queue the share pays for, what it needs and what it spent.</summary>
	private static string Production(Func<string, string> localise, CCountry country, CFixed15 share)
	{
		CFixed15 needed = CFixed15.FromThousandths(country.ProductionCost);
		return CTextTemplate.Fill(localise("DCG_PRODUCTION"),
			("VAL", Rounded(Covered(country, share, needed) * Hundred, 2)),
			("USE", Rounded(CFixed15.FromThousandths(country.ProductionSpent), 2)),
			("NEED", Rounded(needed, 2)));
	}

	/// <summary>The share's IC over what is needed - or the IC itself where nothing is.</summary>
	private static CFixed15 Covered(CCountry country, CFixed15 share, CFixed15 needed)
	{
		CFixed15 ic = CFixed15.FromInt(country.TotalIc) * share;
		return needed.Raw > 0 ? new CFixed15((ic.Raw << CFixed15.Places) / needed.Raw) : ic;
	}

	/// <summary>The supplies the share makes, the share itself, the sources of the supply modifier and what technology adds.</summary>
	private static string Supplies(Func<string, string> localise, CGameState state, CCountry country, CFixed15 share)
	{
		CFixed15 made = CProduction.SuppliesMade(state, country, share);
		string colour = made.Raw <= 0 ? CColouredText.In(CColouredText.Red) : GreenPlus;
		var text = new System.Text.StringBuilder(CTextTemplate.Fill(localise("DCG_SUPPLIES"),
			("VAL", colour + Rounded(made, 2) + White),
			("IC", Rounded(share * Hundred, 2))));

		// Straight after the sentence, with no line between.
		text.Append(CModifierText.Sources(localise, country.ModifierSources, GlobalSupplies));
		CFixedPoint technology = country.Technology[TechnologySupplies];
		if (technology.Raw != 0)
		{
			text.Append('\n').Append(localise("FROM_TECHNOLOGY")).Append(": ").Append(GreenPlus)
				.Append(Rounded(CFixed15.FromThousandths(technology) * Hundred, 2)).Append('%').Append(White).Append('\n');
		}
		return text.ToString();
	}

	/// <summary>How much of what reinforcing needs the share pays for, and the bonus a mobilisation gives.</summary>
	private static string Reinforcement(Func<string, string> localise, CCountry country, CFixed15 share)
	{
		CFixed15 cost = country.ReinforcementCost.Raw > 0 ? CFixed15.FromThousandths(country.ReinforcementCost) : CFixed15.Zero;
		string text = CTextTemplate.Fill(localise("DCG_REINFORCE"), ("VAL", Rounded(Covered(country, share, cost) * Hundred, 2)));
		CFixedPoint bonus = CFixedPoint.One + country.Modifier[ReinforcementBonus];
		if (bonus > CFixedPoint.One)
		{
			text += " " + CTextTemplate.Fill(localise("DCG_REINFORCE_BONUS"), ("VAL", CNumberText.Format(bonus, 1)));
		}
		return text;
	}

	/// <summary>What upgrading needs, and whether the share covers it.</summary>
	private static string Upgrades(Func<string, string> localise, CCountry country, CFixed15 share)
	{
		string text = CTextTemplate.Fill(localise("DCG_UPGRADE"),
			("UNITS", country.SubUnitsUpgrading.ToString(CultureInfo.InvariantCulture)),
			("IC", CNumberText.Format(country.UpgradeCost, 2)));

		// The share in thousandths, cut, times the IC: what it would pay.
		long paid = share.ToThousandths().Raw * country.TotalIc;
		if (paid >= country.UpgradeCost.Raw)
		{
			return text + localise("DCG_UPGRADE_OK");
		}
		CFixed15 cost = country.UpgradeCost.Raw > 0 ? CFixed15.FromThousandths(country.UpgradeCost) : CFixed15.Zero;
		return text + CTextTemplate.Fill(localise("DCG_UPGRADE_BAD"), ("VAL", Rounded(Covered(country, share, cost) * Hundred, 2)));
	}

	private static string Rounded(CFixed15 value, int decimals)
	{
		return CTopBarTooltips.Rounded(value, decimals);
	}
}
