using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the politics screen writes: the government's lines, the pie, the organisation bars, strategic warfare's
/// figures, the cabinet's rows and the choice of a minister, and the laws with their prices. See
/// <c>docs/mechanics/politics.md</c>, section 11.
/// </summary>
public static class CPoliticsScreenText
{
	/// <summary>The government's name, or its <c>&lt;key&gt;_vassal</c> text for a subject where there is one.</summary>
	public static string GovernmentName(Func<string, string> localise, CCountry country)
	{
		if (country.Overlord is not null)
		{
			string vassal = country.GovernmentType + "_vassal";
			string text = localise(vassal);
			if (text != vassal)
			{
				return text;
			}
		}
		return localise(country.GovernmentType);
	}

	/// <summary>
	/// The elections line: <c>TERM_FOR_LIFE</c> for a government with no term, else <c>TERM_DURATION</c> with the term
	/// in months and, where the government holds elections, <c>NEXT_ELECTION</c> and the date - with no closing colour
	/// and no line break, so what follows runs on in yellow - then the government's modifier, a line a value.
	/// </summary>
	public static string ElectionsLine(Func<string, string> localise, CGameState state, CCountry country)
	{
		CGovernment? government = state.GovernmentOf(country);
		int term = CElections.Term(state, country);
		string text;
		if (government is null || government.Duration == 0)
		{
			text = localise("TERM_FOR_LIFE") + "\n";
		}
		else
		{
			text = CTextTemplate.Fill(localise("TERM_DURATION"), ("YEARS", term.ToString(CultureInfo.InvariantCulture))) + "\n";
			if (government.HasElections)
			{
				CDate next = country.LastElection.AddMonths(term);
				text += localise("NEXT_ELECTION") + ": §Y" + localise(next.MonthKey) + " " + next.Day + ", " + next.Year;
			}
		}
		return government is null ? text : text + CModifierText.AllValues(localise, government.Modifier);
	}

	/// <summary>
	/// The pie's slices, one for every ideology from 1 in its colour, each its whole percent of the total rounded half
	/// to even; none where the total is 1.000 or less. Party 0 is never in it.
	/// </summary>
	public static List<(int Percent, CColor Colour)> PieSlices(CGameState state, CCountry country)
	{
		var slices = new List<(int, CColor)>();
		long total = country.Popularity.Sum();
		if (total <= 1000)
		{
			return slices;
		}
		for (int i = 1; i < state.Ideologies.Count && i < country.Popularity.Count; i++)
		{
			long share = Math.Max(country.Popularity[i] * 100000 / 1000 * 1000 / total, 0);
			slices.Add(((int)MathF.Round((float)share / 1000f, MidpointRounding.ToEven), state.Ideologies[i].Colour));
		}
		return slices;
	}

	/// <summary>A party's organisation bar, in whole percent, cut towards nought.</summary>
	public static int OrganisationBar(CCountry country, int ideology)
	{
		return (int)(country.Organisation[ideology] / 1000);
	}

	/// <summary>A strategic-warfare figure: red below nought, green with a plus above, yellow at nought, two places.</summary>
	public static string StrategicWarfare(long impact)
	{
		string figure = CNumberText.Format(new CFixedPoint(impact), 2);
		return impact < 0 ? "§R" + figure : impact > 0 ? "§G+" + figure : "§Y" + figure;
	}

	/// <summary>A minister's personality in a post: his type's name there, or "???" where he has none or is nobody.</summary>
	public static string Personality(Func<string, string> localise, CMinister? minister, string post)
	{
		return minister?.TypeIn(post) is { } type ? localise(type.Name) : "???";
	}

	/// <summary>What his type in that post does, a line a value; nothing where he has none.</summary>
	public static string Ability(Func<string, string> localise, CMinister? minister, string post)
	{
		return minister?.TypeIn(post) is { } type ? CModifierText.AllValues(localise, type.Modifier) : "";
	}

	/// <summary>
	/// The ministers a post may be given, in the pool's order: those whose ideology the government accepts and whose
	/// block names the post. The holder among them, and the post's being fixed is not asked.
	/// </summary>
	public static List<CMinister> Choices(CGameState state, CCountry country, string post)
	{
		var choices = new List<CMinister>();
		CGovernment? government = state.GovernmentOf(country);
		foreach (CMinister minister in country.PossibleMinisters)
		{
			if (government is not null && government.Accepted.Contains(minister.Ideology) && minister.MayTake(post))
			{
				choices.Add(minister);
			}
		}
		return choices;
	}

	/// <summary>Whether a cabinet row's Replace button may be pressed: the post changeable, and another man for it.</summary>
	public static bool MayReplace(CGameState state, CCountry country, CGovernmentPosition position)
	{
		if (!position.Changeable)
		{
			return false;
		}
		country.Government.TryGetValue(position.Name, out CMinister? holder);
		return Choices(state, country, position.Name).Exists(minister => minister != holder);
	}

	/// <summary>A law named for the country: <c>&lt;law&gt;_&lt;TAG&gt;</c> where there is one, else its own name.</summary>
	public static string LawName(Func<string, string> localise, CLaw law, CCountry country)
	{
		return CPartyText.Name(localise, law.Name, country.Tag.Value);
	}

	/// <summary>
	/// A law's row in the choice of a law: its name and, in brackets, its price, green where the money is there and
	/// red where not - "Volunteer Army (§G12.3§W)".
	/// </summary>
	public static string PossibleLawName(Func<string, string> localise, CGameState state, CCountry country, CLaw law)
	{
		CFixedPoint cost = CLaws.ChangeCost(state, country, law);
		string colour = Money(state, country) >= cost ? "§G" : "§R";
		return LawName(localise, law, country) + " (" + colour + CNumberText.Format(cost, 1) + "§W)";
	}

	/// <summary>Whether a law may be chosen: allowed, the money there, and not the law in force.</summary>
	public static bool MayChoose(CGameState state, CCountry country, CLaw law)
	{
		return CLaws.IsAllowed(state, country, law) && Money(state, country) >= CLaws.ChangeCost(state, country, law)
			&& !IsCurrent(country, law);
	}

	public static bool IsCurrent(CCountry country, CLaw law)
	{
		return country.Laws.TryGetValue(law.Group, out CLaw? current) && current == law;
	}

	public static CFixedPoint Money(CGameState state, CCountry country)
	{
		return state.Stockpile(country)?[EGoods.Money] ?? CFixedPoint.Zero;
	}
}
