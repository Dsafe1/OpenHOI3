using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// Who has the use of which strategic resource, and how much of it. See
/// <c>docs/mechanics/modifiers.md</c>, section 6.
///
/// A resource is worth as much as the infrastructure of the province it is in is
/// intact, to whoever controls the province and to the bigger members of its faction.
/// A country's figure for a resource is the best any such province gives, and once
/// reached it is kept: nothing found in the original takes it down again, which is a
/// search coming up empty and not something seen in a running game.
/// </summary>
public static class CStrategicResources
{
	private const string Infrastructure = "infrastructure";
	private const string LocalInfrastructure = "local_infrastructure";
	private const string GlobalInfrastructure = "global_infrastructure";

	/// <summary>Infrastructure is never counted as less than this, nor as more than whole.</summary>
	private static readonly CFixedPoint LeastInfrastructure = new(10);

	/// <summary>The define, in the alignment block, of how near a faction's corner a country must stand to give its leader its resources.</summary>
	public const string BonusDistance = "FACTION_STRAT_BONUS_DIST";

	/// <summary>A country with more base IC than this is given its faction's resources as a major is.</summary>
	private const int IcOfABigCountry = 100;

	/// <summary>
	/// How much of its resource a province gives: what is left of its infrastructure, as
	/// a share of what it was built to. A province with none gives nothing.
	/// </summary>
	public static CFixedPoint Efficiency(CProvince province)
	{
		// Nothing damages infrastructure yet, so what it has and what it was built to are
		// the same figure, and only the two percentages can part them.
		long built = province.Modifier[Infrastructure].Raw;
		long standing = built * (CFixedPoint.Scale + province.Modifier[LocalInfrastructure].Raw
			+ province.Modifier[GlobalInfrastructure].Raw) / CFixedPoint.Scale;
		standing = Math.Clamp(standing, LeastInfrastructure.Raw, CFixedPoint.Scale);

		long efficiency = built == 0 ? -1 : standing * CFixedPoint.Scale / built;
		return efficiency < 0 ? CFixedPoint.Zero : new CFixedPoint(efficiency);
	}

	/// <summary>
	/// Whether a country is given the resources another holds. It always has its own.
	/// Otherwise the two must not be at war with each other, and either they are in one faction and the
	/// receiver is a major or has a large industry, or the receiver leads a faction at war and the holder
	/// stands outside it within the bonus distance of its corner; and their capitals must be on
	/// one continent.
	/// </summary>
	public static bool GetsFrom(CGameState state, CCountry receiver, CCountry holder)
	{
		if (ReferenceEquals(receiver, holder))
		{
			return true;
		}
		if (AreAtWar(state, receiver.Tag, holder.Tag))
		{
			return false;
		}
		bool sameFaction = receiver.Faction.Length > 0
			&& receiver.Faction.Equals(holder.Faction, StringComparison.OrdinalIgnoreCase);
		bool member = (receiver.IsMajor || receiver.BaseIc > IcOfABigCountry) && sameFaction;
		// A faction's leader at war, from a country outside it aligned within the bonus distance of its corner
		// (factions.md, section 6).
		bool aligned = !sameFaction && Wars.CFactions.Group(state, receiver.Faction) is { } group
			&& Agreements.COfferRules.Leader(state, receiver.Faction) == receiver.Tag && state.IsAtWar(receiver.Tag)
			&& Wars.CFactions.Within(state, holder, group, BonusDistance);
		if (!member && !aligned)
		{
			return false;
		}

		string here = state.Province(receiver.ActingCapital).Continent;
		string there = state.Province(holder.ActingCapital).Continent;
		return here.Length > 0 && here.Equals(there, StringComparison.OrdinalIgnoreCase);
	}

	private static bool AreAtWar(CGameState state, CCountryTag one, CCountryTag other)
	{
		foreach (CWar war in state.Wars)
		{
			if ((war.Attackers.Contains(one) && war.Defenders.Contains(other))
				|| (war.Attackers.Contains(other) && war.Defenders.Contains(one)))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Works out what every country has of every resource, as the original does at the
	/// start of each day. A figure is only ever raised.
	/// </summary>
	public static void Update(CGameState state)
	{
		HashSet<string> landowners = CManpower.Landowners(state);
		foreach (CProvince province in state.Provinces)
		{
			if (province.StrategicResource.Length == 0 || state.Country(province.Controller) is not { } holder)
			{
				continue;
			}

			CFixedPoint efficiency = Efficiency(province);
			foreach (CCountry country in state.Countries)
			{
				if (!landowners.Contains(country.Tag.Value) || !GetsFrom(state, country, holder))
				{
					continue;
				}
				country.StrategicResources.TryGetValue(province.StrategicResource, out CFixedPoint had);
				if (efficiency > had)
				{
					country.StrategicResources[province.StrategicResource] = efficiency;
				}
			}
		}
	}
}
