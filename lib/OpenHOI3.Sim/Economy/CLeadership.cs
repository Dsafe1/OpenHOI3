using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// A country's leadership: how much it has, and what its four shares of it make each
/// day. See <c>docs/mechanics/leadership.md</c>.
///
/// Officers, diplomatic influence and spies are made here. What the research share does
/// with its leadership is <see cref="CResearch"/>.
/// </summary>
public static class CLeadership
{
	public const int CategoryCount = 4;

	private const string LocalLeadership = "local_leadership";
	private const string LocalLeadershipModifier = "local_leadership_modifier";
	private const string GlobalLeadership = "global_leadership";
	private const string GlobalLeadershipModifier = "global_leadership_modifier";
	private const string TechnologyLeadership = "leadership_gain";
	private const string OfficerRecruitment = "officer_recruitment";

	/// <summary>The defines that say what a point of leadership makes in a day.</summary>
	public const string LeadershipToOfficers = "LEADERSHIP_TO_OFFICERS";
	public const string LeadershipToDiplomacy = "LEADERSHIP_TO_DIPLOMACY";
	public const string LeadershipToSpies = "LEADERSHIP_TO_SPIES";

	/// <summary>No country has less than this, whatever it holds.</summary>
	private static readonly CFixedPoint Least = CFixedPoint.One;

	/// <summary>The rebels have a tag and nothing to lead.</summary>
	private const string Rebels = "REB";

	/// <summary>Whether the country works through its shares each day: every one but the rebels.</summary>
	public static bool TakesPart(CCountry country)
	{
		return !country.Tag.Value.Equals(Rebels, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// What a province gives: its leadership and its <c>local_leadership</c>, times one and its
	/// <c>local_leadership_modifier</c>, never below nothing - no country's term. Also what the province view shows
	/// (<c>provinceview.md</c>, section 2).
	/// </summary>
	public static CFixedPoint OfProvince(CProvince province)
	{
		long leadership = province.Leadership.Raw + province.Modifier[LocalLeadership].Raw;
		long factor = Math.Max(0, CFixedPoint.Scale + province.Modifier[LocalLeadershipModifier].Raw);
		return new CFixedPoint(Math.Max(0, leadership * factor / CFixedPoint.Scale));
	}

	/// <summary>
	/// Works out every country's leadership, as the original does once a day and once on
	/// entering a game: what the provinces it controls give, each scaled by its own
	/// modifier, plus what every country is given, all scaled by the country's laws,
	/// ministers and technology.
	/// </summary>
	public static void Update(CGameState state)
	{
		var sums = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is not { } controller)
			{
				continue;
			}

			long contribution = OfProvince(province).Raw;
			if (contribution <= 0)
			{
				continue;
			}
			sums.TryGetValue(controller.Value, out long sum);
			sums[controller.Value] = sum + contribution;
		}

		foreach (CCountry country in state.Countries)
		{
			sums.TryGetValue(country.Tag.Value, out long sum);

			// What base_values gives every country reaches it through its modifier, and is
			// scaled with the rest.
			sum += country.Modifier[GlobalLeadership].Raw;

			long factor = CFixedPoint.Scale + country.Modifier[GlobalLeadershipModifier].Raw
				+ country.Technology[TechnologyLeadership].Raw;
			long total = sum * factor / CFixedPoint.Scale;
			if (total < Least.Raw)
			{
				total = Least.Raw;
			}
			// Then what sharing technology costs, which never leaves it under one (aid.md, section 2).
			country.TotalLeadership = new CFixedPoint(Aid.CTechnologySharing.DrainLeadership(state, country, total));
		}
	}

	/// <summary>How much base IC it takes to start a game with one point of diplomatic influence.</summary>
	public const int BaseIcPerInfluenceAtStart = 5;

	/// <summary>
	/// Gives every country what a new game starts with: a quarter of its leadership on
	/// each of the four, and a point of diplomatic influence for every five of its base
	/// IC, in whole points.
	/// </summary>
	public static void SetAtStart(CGameState state)
	{
		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		var quarter = new CFixed15(CFixed15.Scale / CategoryCount);
		foreach (CCountry country in state.Countries)
		{
			Array.Fill(country.LeadershipShares, quarter);
			country.DiplomaticInfluence = CFixedPoint.FromInt(country.BaseIc / BaseIcPerInfluenceAtStart);
		}
	}

	/// <summary>
	/// Whether four numbers can be a country's shares: none below nothing, and no more
	/// than one between them.
	/// </summary>
	public static bool AreValidShares(ReadOnlySpan<CFixed15> shares)
	{
		if (shares.Length != CategoryCount)
		{
			return false;
		}
		long sum = 0;
		foreach (CFixed15 share in shares)
		{
			if (share < CFixed15.Zero)
			{
				return false;
			}
			sum += share.Raw;
		}
		return sum <= CFixed15.Scale;
	}

	/// <summary>The leadership one share has to spend today, in the kind of number shares are worked in.</summary>
	public static CFixed15 Allocated(CCountry country, ELeadershipCategory category)
	{
		return country.LeadershipShares[(int)category] * CFixed15.FromThousandths(country.TotalLeadership);
	}

	/// <summary>The officers a day's officer share trains. The conscription law scales it.</summary>
	public static CFixedPoint OfficersMade(CGameState state, CCountry country)
	{
		CFixed15 made = CFixed15.FromThousandths(state.Defines[CResources.EconomySection, LeadershipToOfficers])
			* Allocated(country, ELeadershipCategory.Officers);
		made *= CFixed15.FromThousandths(country.Modifier[OfficerRecruitment]) + CFixed15.One;
		return made.ToThousandths();
	}

	/// <summary>The diplomatic influence a day's diplomacy share earns.</summary>
	public static CFixedPoint InfluenceMade(CGameState state, CCountry country)
	{
		CFixed15 made = CFixed15.FromThousandths(state.Defines[CResources.EconomySection, LeadershipToDiplomacy])
			* Allocated(country, ELeadershipCategory.Diplomacy);
		return made.ToThousandths();
	}

	/// <summary>
	/// The spies a day's espionage share trains. Unlike the other two, the leadership is
	/// turned into thousandths before the define is applied.
	/// </summary>
	public static CFixedPoint SpiesMade(CGameState state, CCountry country)
	{
		long leadership = Allocated(country, ELeadershipCategory.Espionage).ToThousandths().Raw;
		return new CFixedPoint(leadership * state.Defines[CResources.EconomySection, LeadershipToSpies].Raw / CFixedPoint.Scale);
	}

	/// <summary>
	/// How many things the country may research at once: its research share of its
	/// leadership, to the nearest whole number, and to the even one when it falls
	/// exactly between two. The share is turned into thousandths before it is multiplied.
	/// </summary>
	public static int ResearchSlots(CCountry country)
	{
		long share = country.LeadershipShares[(int)ELeadershipCategory.Research].ToThousandths().Raw;
		long leadership = share * country.TotalLeadership.Raw / CFixedPoint.Scale;
		long whole = leadership / CFixedPoint.Scale;
		long rest = leadership % CFixedPoint.Scale;
		long half = CFixedPoint.Scale / 2;
		if (rest > half || (rest == half && (whole & 1) == 1))
		{
			whole++;
		}
		return (int)whole;
	}

	/// <summary>
	/// One day of every country's four shares, in their own order. Nothing is taken from
	/// anywhere: leadership is not a stock, and what a share does not use is lost.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (!TakesPart(country))
			{
				continue;
			}

			country.Officers = AddNeverBelowNothing(country.Officers, OfficersMade(state, country));
			country.DiplomaticInfluence = AddNeverBelowNothing(country.DiplomaticInfluence, InfluenceMade(state, country));
			// The spies trained, and whole ones placed out of the free ones (espionage.md, section 4).
			Espionage.CSpyPlacing.Run(state, country, SpiesMade(state, country));
			CResearch.RunDaily(state, country, Allocated(country, ELeadershipCategory.Research));
		}
	}

	private static CFixedPoint AddNeverBelowNothing(CFixedPoint held, CFixedPoint made)
	{
		CFixedPoint total = held + made;
		return total < CFixedPoint.Zero ? CFixedPoint.Zero : total;
	}
}
