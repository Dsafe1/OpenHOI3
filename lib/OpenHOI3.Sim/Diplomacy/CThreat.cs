using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// Threat: how much threat one country feels from another, its two spreaders - to everyone, and over
/// the map - what spreads it, and each country's most-threatening country. Nothing lowers it over time.
/// Thousandths, every division cut toward nought. See <c>docs/mechanics/diplomacy.md</c>, section 5.
/// </summary>
public static class CThreat
{
	public const string ImpactModifier = "threat_impact";
	public const string ResistanceModifier = "threat_resistance";

	public const string DiplomacySection = "diplomacy";
	public const string MilitarySection = "military";
	public const string EconomySection = "economy";
	public const string Construction = "REGULAR_CONSTRUCTION_THREAT_IMPACT";
	public const string LandCombat = "LAND_COMBAT_THREAT_IMPACT";
	public const string NavalCombat = "NAVAL_COMBAT_THREAT_IMPACT";
	public const string AirCombat = "AIR_COMBAT_THREAT_IMPACT";
	public const string Bombing = "BOMBING_THREAT_IMPACT";
	public const string Convoys = "THREAT_FROM_CONVOYS_MODIFIER";

	/// <summary>A scale that adds threat, and one that takes it away.</summary>
	public const long Adds = 1000;
	public const long Takes = -1000;

	/// <summary>How much a step on the map's walk lowers the threat: 130 thousandths a size unit.</summary>
	private const long FallPerStep = 130;

	private const long Unwalked = 99999;

	/// <summary>
	/// Threat the feared country spreads to every other country, the dead included: weighed by how near
	/// its capital is and how far apart they stand on the triangle, less what each resists, and never
	/// under a twentieth of the whole (section 5.1).
	/// </summary>
	public static void SpreadToAll(CGameState state, long amount, long scale, CCountry feared)
	{
		long whole = Whole(state, amount, feared);
		long floor = whole * 50 / 1000;
		var neighbours = new HashSet<CCountry>(CCountryLists.Neighbours(state, feared));
		int[] owned = Owned(state);
		CProvince fearedCapital = state.Province(feared.ActingCapital);
		foreach (CCountry country in state.Countries)
		{
			if (country == feared)
			{
				continue;
			}
			long reach = Math.Min(28L * owned[country.Index], 4000);
			long distance = CMapDistance.Between(state.Province(country.ActingCapital), fearedCapital, state.MapWidth);
			long near = reach > 0 ? Math.Max(1000 - distance * 1000 / reach, 0) : 1001;
			if (neighbours.Contains(country))
			{
				near = 1000;
			}
			long felt = whole * near / 1000;
			felt = felt * CAlignment.ThreatFactor(state, feared, CAlignment.GroupOf(state, country)) / 1000;
			felt = felt * (1000 - country.Modifier[ResistanceModifier].Raw) / 1000;
			felt = Math.Max(felt, floor);
			Add(country, feared, scale * felt / 1000);
		}
	}

	/// <summary>
	/// Threat the feared country spreads over the map from <paramref name="origin"/>: a walk outward,
	/// first in first out, the amount falling with every step, each controller charged once at the first
	/// of its provinces the walk reaches (section 5.2).
	/// </summary>
	public static void SpreadOverMap(CGameState state, CCountry feared, long amount, long scale, CProvince origin)
	{
		long whole = Whole(state, amount, feared);
		long floor = whole * 50 / 1000;
		var distance = new long[state.Provinces.Count];
		Array.Fill(distance, Unwalked);
		var marked = new bool[state.Countries.Count];
		marked[feared.Index] = true;
		distance[origin.Id] = 0;
		var queue = new Queue<CProvince>();
		queue.Enqueue(origin);
		while (queue.Count > 0)
		{
			CProvince current = queue.Dequeue();
			foreach (CProvinceEdge edge in current.Edges)
			{
				CProvince next = state.Province(edge.To);
				long far = distance[current.Id] + SizeClass(next);
				if (distance[next.Id] <= far)
				{
					continue;
				}
				long left = Math.Max(whole - FallPerStep * far, floor);
				if (state.Country(next.Controller) is { } controller && !marked[controller.Index] && left > 0)
				{
					left = left * (1000 - controller.Modifier[ResistanceModifier].Raw) / 1000;
					Add(controller, feared,
						left * scale / 1000 * CAlignment.ThreatFactor(state, feared, CAlignment.GroupOf(state, controller)) / 1000);
					marked[controller.Index] = true;
				}
				distance[next.Id] = far;
				if (left > 0)
				{
					queue.Enqueue(next);
				}
			}
		}
	}

	/// <summary>
	/// How big a province counts as on the walk: by the width and the height of its box, under 30 one,
	/// under 60 two, under 90 three, and four.
	/// </summary>
	public static long SizeClass(CProvince province)
	{
		return province.Span < 30 ? 1 : province.Span < 60 ? 2 : province.Span < 90 ? 3 : 4;
	}

	/// <summary>The threat effect's <c>who = TAG</c>: TAG's threat from the scope, floored at nought, unweighed.</summary>
	public static void Add(CCountry holder, CCountry feared, long change)
	{
		CDiplomacyStatus status = CGameState.Status(holder, feared);
		status.Threat = new CFixedPoint(Math.Max(status.Threat.Raw + change, 0));
	}

	/// <summary>A unit put on the map, or joining one there: its owner spreads threat to everyone.</summary>
	public static void UnitDeployed(CGameState state, CCountry owner, CUnit unit)
	{
		long amount = state.Defines[DiplomacySection, Construction].Raw;
		if (unit.SubUnits.Count > 0 && unit.SubUnits[0].IsReserve)
		{
			amount = amount * (1000 + owner.Modifier[CUnits.ReservesPenaltySize].Raw) / 1000 * 1000 / 5000;
		}
		SpreadToAll(state, amount, Adds, owner);
	}

	/// <summary>
	/// A battle started where the province has none of its kind: its attacker's owner spreads threat,
	/// where the province has no controller or its owner is the attacker's enemy.
	/// </summary>
	public static void BattleStarted(CGameState state, CProvince province, CUnit attacker, string define)
	{
		if (state.Country(attacker.Owner) is not { } owner)
		{
			return;
		}
		if (province.Controller is null || CRelationConditions.IsEnemy(state, province.Owner, attacker.Owner))
		{
			SpreadToAll(state, state.Defines[MilitarySection, define].Raw, Adds, owner);
		}
	}

	/// <summary>Ships sunk out of a convoy: the raider spreads threat by how many.</summary>
	public static void ConvoyShipsLost(CGameState state, CCountry raider, int ships)
	{
		SpreadToAll(state, state.Defines[EconomySection, Convoys].Raw * ships, Adds, raider);
	}

	/// <summary>
	/// Each country's most-threatening country: of those it may fear - not itself, REB, one owning no
	/// province, an ally, its master or puppet, or one of its own faction - the first taken even at
	/// nought and a later one only with more (section 5.4).
	/// </summary>
	public static void UpdateMostThreatening(CGameState state)
	{
		bool[] owns = CRelations.Owning(state);
		foreach (CCountry country in state.Countries)
		{
			// REB fears nobody: the start save has "---" for it where every other country has one
			// (measured; what keeps it so was not read).
			if (country.Tag.Value == CRelationConditions.Rebels)
			{
				country.MostThreatening = null;
				continue;
			}
			CCountry? most = null;
			long threat = -1000000;
			bool inFaction = CAlignment.FactionGroup(state, country) is not null;
			foreach (CCountry other in state.Countries)
			{
				if (other == country || other.Tag.Value == CRelationConditions.Rebels || !owns[other.Index]
					|| CCountryLists.HasAlliance(state, country.Tag, other.Tag)
					|| country.Overlord == other.Tag || other.Overlord == country.Tag
					|| (inFaction && other.Faction.Equals(country.Faction, StringComparison.Ordinal)))
				{
					continue;
				}
				long felt = CGameState.Status(country, other).Threat.Raw;
				if (felt > threat)
				{
					threat = felt;
					most = other;
				}
			}
			country.MostThreatening = most?.Tag;
		}
	}

	/// <summary>The amount weighed by the feared country's threat impact, and halved for one in no faction.</summary>
	private static long Whole(CGameState state, long amount, CCountry feared)
	{
		long whole = amount * (1000 + feared.Modifier[ImpactModifier].Raw) / 1000;
		if (CAlignment.FactionGroup(state, feared) is null)
		{
			whole = whole * 1000 / 2000;
		}
		return whole;
	}

	/// <summary>How many provinces each country owns, by its index.</summary>
	private static int[] Owned(CGameState state)
	{
		var owned = new int[state.Countries.Count];
		foreach (CProvince province in state.Provinces)
		{
			if (state.Country(province.Owner) is { } owner)
			{
				owned[owner.Index]++;
			}
		}
		return owned;
	}
}
