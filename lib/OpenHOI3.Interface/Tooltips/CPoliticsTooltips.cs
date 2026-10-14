using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The politics screen's tooltips, by its element: the pie's slice and its change factors, an organisation bar, the
/// strategic-warfare figures, mobilisation, a cabinet row and a law. See <c>docs/mechanics/politics.md</c>, section 11.
/// </summary>
public static class CPoliticsTooltips
{
	/// <summary>
	/// The pie's slice under the mouse: the party's name and the slice's percent; delayed, its popularity's change
	/// factors, a rule, the terms of its drift without the draw, and the seats it qualifies for.
	/// </summary>
	public static CTooltip Slice(Func<string, string> localise, CGameState state, CCountry country, int ideology, int percent)
	{
		string name = CPartyText.Qualified(localise, state.Ideologies[ideology].Name, country.Tag.Value);
		string delayed = Factors(localise, country.Popularity, ideology) + "\n--------------\n"
			+ Drift(localise, state, country, ideology) + "\n"
			+ CTextTemplate.Fill(localise("QUALIFIES_FOR_CABINET"),
				("COUNT", CCabinet.Seats(country, ideology).ToString(CultureInfo.InvariantCulture)));
		return new CTooltip { Text = name + " - §Y" + percent + "%§W", Delayed = delayed };
	}

	/// <summary>An organisation bar: the party's name and its organisation; delayed, its change factors.</summary>
	public static CTooltip Organisation(Func<string, string> localise, CGameState state, CCountry country, int ideology)
	{
		string name = CPartyText.Qualified(localise, state.Ideologies[ideology].Name, country.Tag.Value);
		return new CTooltip
		{
			Text = name + ": " + CNumberText.Format(new CFixedPoint(country.Organisation[ideology]), 2),
			Delayed = Factors(localise, country.Organisation, ideology),
		};
	}

	/// <summary>
	/// A figure's change this month: the drift's change, the effects' tally and the spies' - each only where it is not
	/// nought, the last two ending in a plus as the original writes them - or <c>PO_NO_CHANGE</c>.
	/// </summary>
	public static string Factors(Func<string, string> localise, CPartyFigures figures, int ideology)
	{
		string text = "";
		long last = figures.LastChange.Length > ideology ? figures.LastChange[ideology] : 0;
		if (last != 0)
		{
			text += localise("POL_PARTY_POP_FACTORS") + (last <= 0 ? "§R" : "§G+") + CNumberText.Trimmed(new CFixedPoint(last)) + "§W\n";
		}
		foreach ((string key, int slot) in new[] { ("POL_EVENTS", CPartyFigures.EffectSlot), ("POL_SPIES", CPartyFigures.SpySlot) })
		{
			long tally = ideology < figures.Count ? figures.Tallies[ideology, slot] : 0;
			if (tally != 0)
			{
				text += localise(key) + (tally > 0 ? "§G" : "§R") + CNumberText.Trimmed(new CFixedPoint(tally)) + "+\n";
			}
		}
		return text.Length > 0 ? text : localise("PO_NO_CHANGE");
	}

	/// <summary>
	/// The terms of a party's monthly drift without its draw: the ruling party's support with where it comes from,
	/// the dissent penalty where its ministers hold two posts or more, and its distance from its organisation.
	/// </summary>
	public static string Drift(Func<string, string> localise, CGameState state, CCountry country, int ideology)
	{
		string text = "";
		if (state.Ideology(country.RulingIdeology)?.Index == ideology)
		{
			text += CModifierText.Sources(localise, country.ModifierSources, CParties.RulingPartySupport);
		}
		int posts = CParties.PostsHeld(state, country, ideology);
		if (posts >= 2)
		{
			long penalty = posts * country.Dissent.Raw * state.Defines[CCabinet.Section, "ELECTION_MINISTER_DISSENT_PENALTY"].Raw / 1000;
			text += "\n" + localise("POL_PARTY_MINISTER_DISSENT") + CNumberText.Format(new CFixedPoint(penalty), 2);
		}
		long rate = state.Defines[CCabinet.Section, "PARTY_POP_CHANGE_RATE"].Raw;
		long distance = rate == 0 ? -1000 : (country.Organisation[ideology] - country.Popularity[ideology]) * 1000 / rate;
		return text + "\n" + localise("POL_PARTY_DISTANCE_FROM_ORG") + CNumberText.Format(new CFixedPoint(distance), 2);
	}

	/// <summary>
	/// A strategic-warfare figure's own tooltip, <c>POL_SW_CON</c>, <c>POL_SW_ALL</c> or <c>POL_SW_BOM</c>. Ours: the
	/// routine that fills its four places was not read; they are filled as the texts read - the entries against the
	/// country and their impact, and those for it and theirs.
	/// </summary>
	public static CTooltip WarfareFigure(Func<string, string> localise, string key, IReadOnlyList<(long Hour, long Value)> entries)
	{
		long against = 0;
		long againstImpact = 0;
		long for_ = 0;
		long forImpact = 0;
		foreach ((long _, long value) in entries)
		{
			if (value < 0)
			{
				against++;
				againstImpact += value;
			}
			else if (value > 0)
			{
				for_++;
				forImpact += value;
			}
		}
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise(key),
				("EVIL", against.ToString(CultureInfo.InvariantCulture)),
				("ESC", CNumberText.Format(new CFixedPoint(againstImpact), 2)),
				("GOOD", for_.ToString(CultureInfo.InvariantCulture)),
				("GSC", CNumberText.Format(new CFixedPoint(forImpact), 2))),
		};
	}

	/// <summary>
	/// The mobilise button: what it does, or that a country at war is mobilised already; delayed, how long mobilising
	/// lasts and the men full reserves need.
	/// </summary>
	public static CTooltip Mobilise(Func<string, string> localise, CGameState state, CCountry country)
	{
		long days = state.Defines["country", "MOBILIZATION_BONUS_DAYS"].Raw / 1000;
		long men = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				long most = brigade.Stats[CSubUnitType.MaxStrength].Raw;
				if (brigade.IsReserve && most > 0)
				{
					men += (most - brigade.Strength.Raw) * 1000 / most * brigade.Stats[CSubUnitStats.BuildCostManpower].Raw / 1000;
				}
			}
		}
		return new CTooltip
		{
			Text = localise(state.IsAtWar(country.Tag) ? "POL_MOBILIZE_WAR_IRO" : "POL_MOBILIZE_IRO"),
			Delayed = CTextTemplate.Fill(localise("POL_MOBILIZE_DRO_V2"), ("DAYS", days.ToString(CultureInfo.InvariantCulture))) + "\n"
				+ CTextTemplate.Fill(localise("POL_MOBILIZE_DRO_V3"), ("MP", CNumberText.Format(new CFixedPoint(men), 1))),
		};
	}

	public static CTooltip Demobilise(Func<string, string> localise, CGameState state, CCountry country)
	{
		return new CTooltip
		{
			Text = localise(state.IsAtWar(country.Tag) ? "POL_DEMOBILIZE_WAR_IRO" : "POL_DEMOBILIZE_IRO"),
			Delayed = localise("POL_DEMOBILIZE_DRO"),
		};
	}

	/// <summary>
	/// A law's choice button: the law in force, its <c>allow</c> block's account where it fails, <c>CANNOT_AFFORD</c>
	/// where the money is short, and otherwise the account again.
	/// </summary>
	public static string LawChoice(Func<string, string> localise, CGameState state, CCountry country, CLaw law,
		Func<CLaw, string> allowText)
	{
		if (CPoliticsScreenText.IsCurrent(country, law))
		{
			return localise("POL_CURRENT_LAW");
		}
		if (!CLaws.IsAllowed(state, country, law))
		{
			return allowText(law);
		}
		return CPoliticsScreenText.Money(state, country) < CLaws.ChangeCost(state, country, law)
			? localise("CANNOT_AFFORD")
			: allowText(law);
	}
}
