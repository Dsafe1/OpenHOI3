using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A faction in play: a country coming into one with its wars, leaving one, the rules its members hold. See
/// <c>docs/mechanics/factions.md</c>, sections 1, 2, 4 and 5.
/// </summary>
public static class CFactions
{
	/// <summary>The rule that offers a limited war.</summary>
	public const string LimitedWarRule = "limited_war";

	/// <summary>The rule that makes a member's guarantee and call to arms the whole faction's, so spelled.</summary>
	public const string GuaranteeRule = "allience_wide_guarantee";

	/// <summary>The rule that lets members trade for nothing.</summary>
	public const string FreeGiftsRule = "free_resource_gifts";

	/// <summary>A faction's ideology group, or none for the null faction.</summary>
	public static CIdeologyGroup? Group(CGameState state, string faction)
	{
		if (faction.Length == 0)
		{
			return null;
		}
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			if (string.Equals(group.Faction, faction, StringComparison.Ordinal))
			{
				return group;
			}
		}
		return null;
	}

	/// <summary>The faction a script names, as its group spells it; "" where no group's faction is so named.</summary>
	public static string Named(CGameState state, string name)
	{
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			if (group.Faction.Length > 0 && group.Faction.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return group.Faction;
			}
		}
		return "";
	}

	/// <summary>Whether the country holds the rule: a member holds its faction's rules, a non-member none.</summary>
	public static bool HasRule(CGameState state, CCountry country, string rule)
	{
		return Group(state, country.Faction)?.FactionRules.Contains(rule) == true;
	}

	/// <summary>Whether the country's place on the triangle is under the define's distance from the faction's corner.</summary>
	public static bool Within(CGameState state, CCountry country, CIdeologyGroup group, string define)
	{
		long distance = state.Defines[CAlignment.Section, define].Raw;
		return CAlignment.Squared(group.X - country.AlignmentX, group.Y - country.AlignmentY) < distance * distance / 1000;
	}

	/// <summary>
	/// The original's <c>CFaction::AddCountry</c>: out of its old faction, at the back of the new one - nothing where
	/// it is no real faction or already the country's - and with <paramref name="joinWars"/> into every war the
	/// faction's leader is in that it is not, a puppet only into its overlord's: beside the leader, as an attacker
	/// only where the war is not limited, its relation with each enemy falling by <c>WARDEC_WAR_DIPLOMACY_HIT</c>.
	/// </summary>
	public static void AddCountry(CGameState state, CCountry country, string faction, bool joinWars)
	{
		if (country.Faction.Length > 0 && country.Faction != faction)
		{
			CFactionMembership.Leave(state, country);
		}
		if (faction.Length == 0 || country.Faction == faction)
		{
			return;
		}
		CFactionMembership.Join(state, country, faction);
		// A change of diplomacy (politics.md, section 9).
		Politics.CLaws.OnDiplomacyChanged(state, country);
		if (joinWars)
		{
			JoinFactionWars(state, country, faction);
		}
		// And in a running game the joiner's AI strategy is made again, and its neighbours' (aistrategy.md, section 1).
		Ai.CAiStrategies.AfterFactionJoined(state, country);
	}

	/// <summary>The war sweep of a join: every war the faction's leader fights, joined on its side, the relation hit taken.</summary>
	private static void JoinFactionWars(CGameState state, CCountry country, string faction)
	{
		if (state.Country(COfferRules.Leader(state, faction)) is not { } leader || leader == country)
		{
			return;
		}
		CFixedPoint hit = COfferRules.Define(state, CDeclarations.RelationHit);
		foreach (CWar war in state.Wars.ToArray())
		{
			if (war.Attackers.Count == 0 || war.Defenders.Count == 0
				|| war.Attackers.Contains(country.Tag) || war.Defenders.Contains(country.Tag))
			{
				continue;
			}
			if (country.Overlord is { } overlord && !war.Attackers.Contains(overlord) && !war.Defenders.Contains(overlord))
			{
				continue;
			}
			bool attacking = war.Attackers.Contains(leader.Tag) && !war.IsLimited;
			if (!attacking && !war.Defenders.Contains(leader.Tag))
			{
				continue;
			}
			CWarSides.Join(state, war, country.Tag, attacking);
			foreach (CCountryTag enemy in (attacking ? war.Defenders : war.Attackers).ToArray())
			{
				if (state.Country(enemy) is { } other)
				{
					CRelations.Change(country, other, -hit);
				}
			}
		}
	}

	/// <summary>
	/// <c>leave_faction</c>: a member of the faction first leaves every war in which its side is led by the faction's
	/// leader, then the faction. Nothing for a country not in it.
	/// </summary>
	public static void LeaveByEffect(CGameState state, CCountry country, string faction)
	{
		if (faction.Length == 0 || country.Faction != faction)
		{
			return;
		}
		if (COfferRules.Leader(state, faction) is { } leader)
		{
			foreach (CWar war in state.Wars.ToArray())
			{
				List<CCountryTag>? side = war.Attackers.Contains(country.Tag) ? war.Attackers
					: war.Defenders.Contains(country.Tag) ? war.Defenders : null;
				if (side is { Count: > 0 } && side[0] == leader)
				{
					CWarSides.Leave(state, war, country.Tag);
				}
			}
		}
		CFactionMembership.Leave(state, country);
		Politics.CLaws.OnDiplomacyChanged(state, country);
	}
}
