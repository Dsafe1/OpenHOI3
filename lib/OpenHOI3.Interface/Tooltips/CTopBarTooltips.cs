using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What each element of the top bar says in a tooltip, by the element's name, as the
/// original's top bar writes it. See <c>docs/mechanics/topbar.md</c>, section 14.
/// </summary>
public static class CTopBarTooltips
{
	private const string White = "§W";
	private const string GreenPlus = "§G+";

	private const string TechnologyIc = "ic_modifier";
	private const string TechnologyManpower = "manpower_gain";
	private const string GlobalIc = "global_ic";
	private const string DissentKind = "dissent";
	private const string NationalUnity = "national_unity";
	private const string NationalUnityEffect = "national_unity_effect";
	private const string OfficerRecruitment = "officer_recruitment";
	private const string GlobalManpower = "global_manpower";
	private const string GlobalManpowerModifier = "global_manpower_modifier";

	/// <summary>A need for manpower from this on is written to one place, and below it to two.</summary>
	private static readonly CFixedPoint ManpowerInTenths = CFixedPoint.FromInt(10);

	/// <summary>The pictures beside the figures: each gives a heading, with its description to come.</summary>
	private static readonly Dictionary<string, string> Headings = new(StringComparer.OrdinalIgnoreCase)
	{
		["icon_ic_small"] = "IC",
		["icon_energy_small"] = "ENERGY",
		["icon_metal_small"] = "METAL",
		["icon_raremat_small"] = "RAREMAT",
		["icon_oil_small"] = "CRUDE_OIL",
		["icon_fuel_small"] = "FUEL",
		["icon_supplies_small"] = "SUPPLIES",
		["icon_money_small"] = "MONEY",
		["icon_dissent"] = "DISSENT",
		["icon_diplomacy"] = "DIPLOINF",
		["icon_national_unity"] = "NATIONAL_UNITY",
		["icon_espionage"] = "ESPIONAGE",
		["icon_leadership"] = "OFFICERS",
		["icon_manpower"] = "MANPOWER",
	};

	/// <summary>The resources' numbers, and the good each is the ledger of.</summary>
	private static readonly Dictionary<string, EGoods> Ledgers = new(StringComparer.OrdinalIgnoreCase)
	{
		["energy_number"] = EGoods.Energy,
		["metal_number"] = EGoods.Metal,
		["raremat_number"] = EGoods.RareMaterials,
		["oil_number"] = EGoods.CrudeOil,
		["fuel_number"] = EGoods.Fuel,
		["supplies_number"] = EGoods.Supplies,
		["money_number"] = EGoods.Money,
	};

	/// <summary>The names of the elements that have a tooltip of their own.</summary>
	public static IEnumerable<string> Elements()
	{
		foreach (string name in Headings.Keys)
		{
			yield return name;
		}
		foreach (string name in Ledgers.Keys)
		{
			yield return name;
		}
		yield return "ic_number";
		yield return "dissent_number";
		yield return "diplomacy_number";
		yield return "national_unity_number";
		yield return "espionage_number";
		yield return "leadership_number";
		yield return "manpower_number";
	}

	/// <summary>
	/// The tooltip of the element of that name, or an empty one where the bar says nothing
	/// of it - the buttons say something only in a scenario, which there are none of.
	/// </summary>
	public static CTooltip For(Func<string, string> localise, CGameState state, CCountry country, string element)
	{
		if (Headings.TryGetValue(element, out string? heading))
		{
			return new CTooltip { Text = localise(heading + "_IRO"), Delayed = localise(heading + "_DRO") };
		}
		if (Ledgers.TryGetValue(element, out EGoods goods))
		{
			// A government in exile's supplies and fuel, shown as a dash, say nothing.
			return country.InExile && goods is EGoods.Supplies or EGoods.Fuel ? CTooltip.Empty
				: new CTooltip { Text = Ledger(localise, country, goods) };
		}
		return element.ToLowerInvariant() switch
		{
			"ic_number" => new CTooltip { Text = Ic(localise, country) },
			"dissent_number" => new CTooltip { Text = Dissent(localise, state, country) },
			"diplomacy_number" => new CTooltip { Text = Diplomacy(localise, state, country) },
			"national_unity_number" => new CTooltip { Text = NationalUnityText(localise, country) },
			"espionage_number" => new CTooltip { Text = localise("FREE_SPIES") },
			"leadership_number" => new CTooltip { Text = Officers(localise, state, country) },
			"manpower_number" => Manpower(localise, country),
			_ => CTooltip.Empty,
		};
	}

	/// <summary>
	/// A good's day: what it changed by, a line for each thing that moved it, and the
	/// sources of its global modifier.
	/// </summary>
	public static string Ledger(Func<string, string> localise, CCountry country, EGoods goods)
	{
		CFixedPoint convoyedIn = ConvoyedIn(country, goods);
		CFixedPoint change = DayChange(country, goods);

		var text = new StringBuilder();
		text.Append(Line(localise, "RES_CHANGE", (change.Raw < 0 ? Red : GreenPlus) + CNumberText.Format(change, 2)));
		Gain(text, localise, "RES_PROD", country.HomeProduced[goods]);
		Gain(text, localise, "RES_CONVOYED_IN", convoyedIn);
		Gain(text, localise, "RES_TRADED_FOR", country.TradedFor[goods]);
		Gain(text, localise, "RES_INC_DEBT", goods == EGoods.Money ? country.IncomeFromDebt : CFixedPoint.Zero);
		Gain(text, localise, "RES_FROM_PUPPETS", country.TributeReceived[goods]);
		// The used half of converting is written negated, whichever way it went.
		Signed(text, localise, "RES_CONVERTED", -country.ConversionUsed[goods]);
		Signed(text, localise, "RES_CONVERTED", country.ConversionMade[goods]);
		Loss(text, localise, "RES_TRADED_AWAY", country.TradedAway[goods]);
		Loss(text, localise, "RES_REPAID_AWAY", goods == EGoods.Money ? country.RepaidAway : CFixedPoint.Zero);
		Loss(text, localise, "RES_CONVOYED_OUT", country.ConvoyedOut[goods]);
		// Tribute sent is held below nought and written green with a plus though it is a loss - the original's.
		if (country.TributeSent[goods].Raw < 0)
		{
			text.Append(Line(localise, "RES_SENT_TO_MASTER", GreenPlus + CNumberText.Format(-country.TributeSent[goods], 2)));
		}
		// Into the network is what was made away from the capital, and used is all that left the stockpile to be used -
		// industry's, and the supplies and fuel the network and the units took out of the capital (topbar.md, section 14).
		Loss(text, localise, "RES_INTO_NET", country.SentTo[goods]);
		Loss(text, localise, "RES_USED", country.Usage[goods]);
		Gain(text, localise, "RES_SHIPPED_BACK", country.SentBack[goods]);
		text.Append('\n').Append(CModifierText.Sources(localise, country.ModifierSources, GlobalKind(goods)));
		return text.ToString();
	}

	/// <summary>
	/// What a good changed by in the day: what came in less what went out, supplies and fuel taken
	/// without what convoys brought in. The diplomacy screen's rows are tinted by it too.
	/// </summary>
	public static CFixedPoint DayChange(CCountry country, EGoods goods)
	{
		return CTopBarText.DayIn(country, goods) + country.TradedFor[goods] - CTopBarText.DayOut(country, goods);
	}

	private static CFixedPoint ConvoyedIn(CCountry country, EGoods goods) =>
		goods is EGoods.Supplies or EGoods.Fuel ? CFixedPoint.Zero : country.ConvoyedIn[goods];

	private static string Red => CColouredText.In(CColouredText.Red);

	/// <summary>A key whose <c>$VAL$</c> is the figure given, closed in white.</summary>
	private static string Line(Func<string, string> localise, string key, string figure)
	{
		return CTextTemplate.Fill(localise(key), ("VAL", figure + White));
	}

	/// <summary>A line for something that came in, where anything did: green, with a plus.</summary>
	private static void Gain(StringBuilder text, Func<string, string> localise, string key, CFixedPoint value)
	{
		if (value.Raw > 0)
		{
			text.Append(Line(localise, key, GreenPlus + CNumberText.Format(value, 2)));
		}
	}

	/// <summary>A line for something that went out, where anything did: red, and negated.</summary>
	private static void Loss(StringBuilder text, Func<string, string> localise, string key, CFixedPoint value)
	{
		if (value.Raw > 0)
		{
			text.Append(Line(localise, key, Red + CNumberText.Format(-value, 2)));
		}
	}

	/// <summary>A line for something that may go either way, where it is not nothing: green with a plus above nothing, red below.</summary>
	private static void Signed(StringBuilder text, Func<string, string> localise, string key, CFixedPoint value)
	{
		if (value.Raw != 0)
		{
			text.Append(Line(localise, key, (value.Raw > 0 ? GreenPlus : Red) + CNumberText.Format(value, 2)));
		}
	}

	/// <summary>The kind of modifier that adds to a good everywhere.</summary>
	public static string GlobalKind(EGoods goods)
	{
		return goods switch
		{
			EGoods.Supplies => "global_supplies",
			EGoods.Fuel => "global_fuel",
			EGoods.Money => "global_money",
			EGoods.CrudeOil => "global_crude_oil",
			EGoods.Metal => "global_metal",
			EGoods.Energy => "global_energy",
			_ => "global_rare_materials",
		};
	}

	/// <summary>The three IC figures the bar shows, what technology adds, and the sources of global IC.</summary>
	public static string Ic(Func<string, string> localise, CCountry country)
	{
		var text = new StringBuilder(CTextTemplate.Fill(localise("IC_NUMBER_IRO"),
			("WASTED", CProduction.Wasted(country).ToString(CultureInfo.InvariantCulture)),
			("AVAILABLE", country.TotalIc.ToString(CultureInfo.InvariantCulture)),
			("BASE", country.MaxIc.ToString(CultureInfo.InvariantCulture))));
		text.Append(FromTechnology(localise, country.Technology[TechnologyIc]));
		text.Append(CModifierText.Sources(localise, country.ModifierSources, GlobalIc));
		return text.ToString();
	}

	/// <summary>What technology adds, as a percentage to two places, where it adds anything.</summary>
	private static string FromTechnology(Func<string, string> localise, CFixedPoint bonus)
	{
		if (bonus.Raw == 0)
		{
			return "";
		}
		return localise("FROM_TECHNOLOGY") + ": " + GreenPlus + CNumberText.Format(new CFixedPoint(bonus.Raw * 100), 2) + "%" + White + "\n";
	}

	/// <summary>
	/// Which way dissent is going and by how much, what else pushes it, and what the dissent
	/// there is costs.
	/// </summary>
	public static string Dissent(Func<string, string> localise, CGameState state, CCountry country)
	{
		CFixed15 change = CProduction.DissentChange(state, country);
		char colour = change.Raw > 0 ? CColouredText.Red : change.Raw == 0 ? CColouredText.Yellow : CColouredText.Green;
		var text = new StringBuilder(CTextTemplate.Fill(localise("DISSENTV_IRO"),
			("CHANGE", CColouredText.In(colour) + Rounded(change, 2) + White)));

		string sources = CModifierText.Sources(localise, country.ModifierSources, DissentKind);
		if (sources.Length > 1)
		{
			text.Append('\n').Append(localise("DISSENTV_IRO2")).Append(sources);
		}
		if (country.Dissent.Raw != 0 && state.FindStaticModifier(CCountryModifiers.Dissent) is { } dissent)
		{
			text.Append('\n').Append(localise("DISSENTV_IRO3"))
				.Append(CModifierText.AllValues(localise, CCountryModifiers.Scaled(dissent, country.Dissent)));
		}
		return text.ToString();
	}

	/// <summary>
	/// A number of the kind shares are worked in, rounded to so many places as the
	/// original's <c>sprintf</c> rounds it - where every figure in thousandths is cut.
	/// </summary>
	public static string Rounded(CFixed15 value, int decimals)
	{
		double number = value.Raw / (double)CFixed15.Scale;
		return number.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
	}

	/// <summary>A share of leadership in thousandths, cut, as the tooltips work it out.</summary>
	private static long ShareInThousandths(CCountry country, ELeadershipCategory category)
	{
		return country.LeadershipShares[(int)category].Raw * CFixedPoint.Scale / CFixed15.Scale;
	}

	/// <summary>What a day's share of leadership makes, in thousandths, cut at each step.</summary>
	private static long Made(CGameState state, CCountry country, ELeadershipCategory category, string define)
	{
		long made = ShareInThousandths(country, category) * country.TotalLeadership.Raw / CFixedPoint.Scale;
		return made * state.Defines[CResources.EconomySection, define].Raw / CFixedPoint.Scale;
	}

	/// <summary>The day's influence: what leadership makes of it, what influencing others costs, and the two together.</summary>
	public static string Diplomacy(Func<string, string> localise, CGameState state, CCountry country)
	{
		var produced = new CFixedPoint(Made(state, country, ELeadershipCategory.Diplomacy, CLeadership.LeadershipToDiplomacy));

		// Nothing here influences anybody, so nothing is spent on it.
		CFixedPoint upkeep = CFixedPoint.Zero;
		CFixedPoint daily = produced - upkeep;
		string colour = daily.Raw < 0 ? Red : daily.Raw == 0 ? CColouredText.In(CColouredText.Yellow) : GreenPlus;
		return CTextTemplate.Fill(localise("DIPDETAILS_IRO"),
			("PRODUCED", CNumberText.Format(produced, 2)),
			("UPKEEP", CNumberText.Format(upkeep, 2)),
			("DAILY", colour + CNumberText.Format(daily, 2)));
	}

	/// <summary>National unity, what moves it each month, and what changes how much it moves.</summary>
	public static string NationalUnityText(Func<string, string> localise, CCountry country)
	{
		var text = new StringBuilder();
		text.Append(CColouredText.In(CColouredText.Yellow)).Append(CNumberText.Format(country.NationalUnity, 3)).Append('%').Append(White).Append('\n');

		// The original puts what strategic warfare does to unity first; there is none here.
		string list = CModifierText.Sources(localise, country.ModifierSources, NationalUnity);
		if (list.Length < 3)
		{
			text.Append(localise("NO_CURR_EFFECTS_ON_NU"));
		}
		else
		{
			text.Append(localise("STRMONTHLY_CHANGE")).Append('\n').Append(list);
		}

		string effect = CModifierText.Sources(localise, country.ModifierSources, NationalUnityEffect);
		if (effect.Length > 2)
		{
			text.Append('\n').Append(localise("EFFECT_SIZE_MOD_NU")).Append('\n').Append(effect);
		}
		return text.ToString();
	}

	/// <summary>The officers the country has, the brigades, ships and wings they cover, how many those want, and how many a day trains.</summary>
	public static string Officers(Func<string, string> localise, CGameState state, CCountry country)
	{
		long daily = Made(state, country, ELeadershipCategory.Officers, CLeadership.LeadershipToOfficers);
		daily = daily * (CFixedPoint.Scale + country.Modifier[OfficerRecruitment].Raw) / CFixedPoint.Scale;

		var text = new StringBuilder(CTextTemplate.Fill(localise("OFFICER_NUMBER2_IRO"),
			("OFFICERS", CNumberText.Format(country.Officers, 0)),
			("DIVISIONS", SubUnits(country).ToString(CultureInfo.InvariantCulture)),
			("NEED", CNumberText.Format(country.OfficersNeeded, 0)),
			("DAILY", CNumberText.Format(new CFixedPoint(daily), 1))));

		string sources = CModifierText.Sources(localise, country.ModifierSources, OfficerRecruitment);
		if (sources.Length > 2)
		{
			text.Append('\n').Append(sources);
		}
		return text.ToString();
	}

	/// <summary>The brigades, ships and wings the country has, those waiting to be placed among them, as the officers it needs are counted.</summary>
	public static int SubUnits(CCountry country)
	{
		int count = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			count += unit.SubUnits.Count;
		}
		foreach (CUnit unit in country.Deployments)
		{
			count += unit.SubUnits.Count;
		}
		return count;
	}

	/// <summary>
	/// What reinforcing takes, and - to come - what a month brings in, what the manpower
	/// before any modifier is, and the sources of both kinds of manpower modifier.
	/// </summary>
	public static CTooltip Manpower(Func<string, string> localise, CCountry country)
	{
		int decimals = country.ReinforcementManpowerNeeded < ManpowerInTenths ? 2 : 1;
		string text = CTextTemplate.Fill(localise("MANPOWER_DETAILS_IRO"),
			("NEED", CNumberText.Format(country.ReinforcementManpowerNeeded, decimals)),
			("USED", CNumberText.Format(country.ReinforcementManpowerUsed, decimals)));

		CFixedPoint technology = country.Technology[TechnologyManpower];
		var delayed = new StringBuilder(CTextTemplate.Fill(localise("MANPOWER_DETAILS_DRO"),
			("MONTHLY", CNumberText.Format(country.MonthlyManpower, 1)),
			("BASE", Base(country.MaxManpower, country.Modifier[GlobalManpowerModifier], technology))));

		var list = new StringBuilder(CModifierText.Sources(localise, country.ModifierSources, GlobalManpower));
		if (technology.Raw != 0)
		{
			if (list.Length > 1)
			{
				list.Append('\n');
			}
			list.Append(FromTechnology(localise, technology));
		}
		list.Append(CModifierText.Sources(localise, country.ModifierSources, GlobalManpowerModifier));
		delayed.Append(list);
		return new CTooltip { Text = text, Delayed = delayed.ToString() };
	}

	/// <summary>
	/// The manpower before modifiers, as the original's tooltip works it out: in single
	/// precision, half the most manpower over one and the two percentages, to three places.
	/// </summary>
	public static string Base(CFixedPoint maxManpower, CFixedPoint modifier, CFixedPoint technology)
	{
		float percentage = (float)((float)modifier.Raw / 1000.0);
		float gain = (float)((float)technology.Raw / 1000.0);
		float under = (float)((float)(percentage + 1.0) + (double)gain);
		float most = (float)((float)maxManpower.Raw / 1000.0);
		float over = (float)(most * 0.5);
		float value = (float)(over / (double)under);
		return ((double)value).ToString("F3", CultureInfo.InvariantCulture);
	}
}
