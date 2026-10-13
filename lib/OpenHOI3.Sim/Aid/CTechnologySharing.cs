using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Aid;

/// <summary>
/// Sharing a theory: the receiver reads the sharer's level where it is higher than its own; a new sharer of
/// one category ends every category the old one gave the receiver; each day a share is dropped where the
/// receiver owns nothing or the sharer is no longer above it; and each share costs the sharer leadership.
/// See <c>docs/mechanics/aid.md</c>, section 2.
/// </summary>
public static class CTechnologySharing
{
	public const string LeadershipCostDefine = "SHARE_TECH_LEADERSHIP_COST";

	/// <summary>The sharer begins sharing the category with the receiver.</summary>
	public static void Start(CCountry sharer, CCountry receiver, string category)
	{
		sharer.TechnologyShares.Add((receiver, category));
		if (receiver.TechnologySharedFrom.TryGetValue(category, out CCountry? old))
		{
			Stop(old, receiver);
		}
		receiver.TechnologySharedFrom[category] = sharer;
	}

	/// <summary>The sharer stops sharing every category with the receiver.</summary>
	public static void Stop(CCountry sharer, CCountry receiver)
	{
		foreach ((CCountry with, string category) in sharer.TechnologyShares.ToArray())
		{
			if (with != receiver)
			{
				continue;
			}
			sharer.TechnologyShares.Remove((with, category));
			if (receiver.TechnologySharedFrom.TryGetValue(category, out CCountry? from) && from == sharer)
			{
				receiver.TechnologySharedFrom.Remove(category);
			}
		}
	}

	/// <summary>Whether the sharer shares anything with the receiver.</summary>
	public static bool IsSharing(CCountry sharer, CCountry receiver) => sharer.TechnologyShares.Exists(s => s.Receiver == receiver);

	/// <summary>The first category the sharer shares with the receiver, or empty.</summary>
	public static string FirstShared(CCountry sharer, CCountry receiver) =>
		sharer.TechnologyShares.Find(s => s.Receiver == receiver).Category ?? "";

	/// <summary>
	/// The day's look at every share: dropped - and with it everything that sharer gives that receiver -
	/// where the receiver owns no province or the sharer's own level is not above the receiver's own.
	/// </summary>
	public static void CheckDaily(CGameState state)
	{
		CCountryFacts facts = CCountryFacts.Of(state);
		foreach (CCountry sharer in state.Countries)
		{
			foreach ((CCountry receiver, string category) in sharer.TechnologyShares.ToArray())
			{
				if (!sharer.TechnologyShares.Contains((receiver, category)))
				{
					continue;
				}
				if (facts.OwnedCount(receiver.Tag) == 0 || sharer.OwnAbility(category) <= receiver.OwnAbility(category))
				{
					Stop(sharer, receiver);
				}
			}
		}
	}

	/// <summary>
	/// What sharing costs a country's leadership, taken off its total after everything else: the define for
	/// each share, and while that would leave it under one, its newest receiver's shares are ended.
	/// </summary>
	public static long DrainLeadership(CGameState state, CCountry country, long total)
	{
		long cost = state.Defines[Agreements.COfferRules.Section, LeadershipCostDefine].Raw;
		long count = country.TechnologyShares.Count;
		while (total < count * cost + CFixedPoint.Scale && count > 0)
		{
			Stop(country, country.TechnologyShares[^1].Receiver);
			count = country.TechnologyShares.Count;
		}
		return total - count * cost;
	}
}
