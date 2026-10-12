using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// The relation between two countries: how a new game gives it, the month's drift, and every change,
/// which writes both sides. See <c>docs/mechanics/diplomacy.md</c>, sections 2 to 4.
/// </summary>
public static class CRelations
{
	public static readonly CFixedPoint Highest = CFixedPoint.FromInt(200);
	public static readonly CFixedPoint Lowest = CFixedPoint.FromInt(-200);

	/// <summary>What a new game gives a pair with nothing: the other is REB or owns no province.</summary>
	private const int Unowned = 25;

	/// <summary>What a friend of a province's owner gains with it for a land battle won there.</summary>
	private static readonly CFixedPoint BattleWon = CFixedPoint.FromInt(5);

	public static CFixedPoint Of(CCountry holder, CCountry other) => CGameState.Status(holder, other).Relation;

	/// <summary>
	/// Adds <paramref name="change"/> to <paramref name="holder"/>'s relation towards
	/// <paramref name="other"/>, clamps it, and writes the result to both sides.
	/// </summary>
	public static void Change(CCountry holder, CCountry other, CFixedPoint change)
	{
		CFixedPoint relation = Clamp(Of(holder, other) + change);
		CGameState.Status(holder, other).Relation = relation;
		CGameState.Status(other, holder).Relation = relation;
	}

	/// <summary>One side only, clamped, as the history and a new game set it.</summary>
	public static void SetOneSide(CCountry holder, CCountry other, CFixedPoint relation)
	{
		CGameState.Status(holder, other).Relation = Clamp(relation);
	}

	/// <summary>
	/// The history's <c>relation</c> entries, in the order of their dates up to the start, each one
	/// side; an entry that has ended sets that side back to 0 (section 2).
	/// </summary>
	public static void SetFromHistory(CGameState state, IEnumerable<CHistoryRelation> entries, CDate start)
	{
		var events = new List<(CDate Date, int Order, CHistoryRelation Entry, bool Ends)>();
		int order = 0;
		foreach (CHistoryRelation entry in entries)
		{
			if (entry.Start.CompareTo(start) <= 0)
			{
				events.Add((entry.Start, order++, entry, false));
			}
			if (entry.End is { } end && end.CompareTo(start) <= 0)
			{
				events.Add((end, order++, entry, true));
			}
		}
		events.Sort((a, b) => a.Date.CompareTo(b.Date) is int byDate and not 0 ? byDate : a.Order.CompareTo(b.Order));
		foreach ((CDate _, int _, CHistoryRelation entry, bool ends) in events)
		{
			if (state.Country(entry.First) is { } first && state.Country(entry.Second) is { } second)
			{
				SetOneSide(first, second, ends ? CFixedPoint.Zero : entry.Value);
			}
		}
	}

	/// <summary>
	/// The relations nobody gave, as a new game sets them: for each country owning a province but
	/// REB, towards each country in order, 25 or what the install's Lua says - one side, and only
	/// where that side is still exactly 0 (section 2). Without the Lua, the relations it would give
	/// are left at 0 and reported.
	/// </summary>
	public static void SetAtStart(CGameState state, Action<string> report)
	{
		int unasked = 0;
		bool[] owns = Owning(state);
		foreach (CCountry country in state.Countries)
		{
			if (country.Tag.Value == CRelationConditions.Rebels || !owns[country.Index])
			{
				continue;
			}
			foreach (CCountry other in state.Countries)
			{
				int? value = Unowned;
				if (other.Tag.Value != CRelationConditions.Rebels && owns[other.Index])
				{
					value = state.AiScripts?.NonHistoricalRelation(state, country, other);
				}
				if (value is not { } whole)
				{
					unasked++;
					continue;
				}
				if (CGameState.Status(country, other).Relation == CFixedPoint.Zero)
				{
					SetOneSide(country, other, CFixedPoint.FromInt(whole));
				}
			}
		}
		if (unasked > 0)
		{
			report($"{unasked} relations of a new game were not given: AI_GenerateNonHistoricalRelation could not be asked");
		}
	}

	/// <summary>
	/// The month: each country, towards each country that owns a province, half its drift added and
	/// written to both sides (section 3). The original's loop starts at its second entry because its
	/// first is the <c>---</c> placeholder, which this list does not have; REB is visited like any other.
	/// </summary>
	public static void RunMonth(CGameState state)
	{
		bool[] owns = Owning(state);
		var atWar = new bool[state.Countries.Count];
		var neighbours = new HashSet<CCountry>[state.Countries.Count];
		foreach (CCountry country in state.Countries)
		{
			atWar[country.Index] = state.IsAtWar(country.Tag);
			neighbours[country.Index] = new HashSet<CCountry>(CCountryLists.Neighbours(state, country));
		}
		foreach (CCountry country in state.Countries)
		{
			foreach (CCountry other in state.Countries)
			{
				if (!owns[other.Index])
				{
					continue;
				}
				long drift = Drift(state, country, other, atWar, neighbours[country.Index]);
				Change(country, other, new CFixedPoint(drift * 1000 / 2000));
			}
		}
	}

	/// <summary>How far a month moves the pair, in thousandths, as the holder sees it (section 3).</summary>
	public static long Drift(CGameState state, CCountry country, CCountry other, bool[] atWar, ISet<CCountry> neighbours)
	{
		long drift = 0;
		if (country.Overlord == other.Tag || other.Overlord == country.Tag)
		{
			drift += 250;
		}
		if (CCountryLists.AtWar(state, country.Tag, other.Tag))
		{
			drift -= 100;
		}
		if (neighbours.Contains(other) && !atWar[country.Index] && !atWar[other.Index])
		{
			drift += 50;
		}
		if (CCountryLists.HasGuarantee(state, country.Tag, other.Tag))
		{
			drift += 80;
		}
		// Military access is the agreements' piece: there is none to add yet.
		if (Of(country, other) > CFixedPoint.FromInt(100) && drift > 0)
		{
			drift = 0;
		}
		bool nonePlayed = !state.Played.Contains(country.Tag) && !state.Played.Contains(other.Tag);
		if (nonePlayed && (country.Overlord == other.Tag || other.Overlord == country.Tag))
		{
			drift = Math.Max(drift + 2000, 2000);
		}
		return drift;
	}

	/// <summary>
	/// A land battle decided in a province whose owner is an enemy of the losers' first country: each
	/// winning country that is a friend of the owner, and not the owner, gains 5 with it (section 4).
	/// Like the battle's counts, only where both sides still have a country standing.
	/// </summary>
	public static void LandBattleWon(CGameState state, CCombat combat, bool attackerLost)
	{
		if (combat.Kind != ECombatKind.Land || combat.IsConvoyBattle)
		{
			return;
		}
		CCombatSide winner = attackerLost ? combat.Defender : combat.Attacker;
		CCombatSide loser = combat.Other(winner);
		if (winner.Standing.Count == 0 || loser.Standing.Count == 0
			|| state.Country(state.Province(combat.Province).Owner) is not { } owner
			|| !CMovementRules.IsEnemy(state, owner.Tag, loser.Standing[0]))
		{
			return;
		}
		foreach (CCountryTag tag in winner.Standing)
		{
			if (tag != owner.Tag && state.Country(tag) is { } country && CSupplyRelations.IsFriendly(state, country, owner))
			{
				Change(country, owner, BattleWon);
			}
		}
	}

	/// <summary>Whether each country owns a province, by its index: one walk of the map.</summary>
	public static bool[] Owning(CGameState state)
	{
		var owns = new bool[state.Countries.Count];
		foreach (Provinces.CProvince province in state.Provinces)
		{
			if (state.Country(province.Owner) is { } owner)
			{
				owns[owner.Index] = true;
			}
		}
		return owns;
	}

	private static CFixedPoint Clamp(CFixedPoint relation) => CFixedPoint.Clamp(relation, Lowest, Highest);
}
