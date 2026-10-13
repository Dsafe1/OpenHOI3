using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A country joining a side of a war, as the original's <c>CWar::AddAttacker</c> and <c>AddDefender</c> put
/// it there, and a country brought up to date with its wars - the <c>last_war</c> stamp a truce counts from,
/// and mobilisation. See <c>docs/mechanics/war.md</c>, sections 1 and 4.
/// </summary>
public static class CWarSides
{
	/// <summary>The modifier a country at war is mobilised with.</summary>
	public const string Mobilisation = "initial_mobilization";

	/// <summary>
	/// Puts the country on the attackers or the defenders - nothing where it is there already - at the front
	/// or the back as the original does, then has every country on the other side at war with it and brings
	/// both up to date. Only a call to arms lets a joiner take the lead.
	/// </summary>
	public static void Join(CGameState state, CWar war, CCountryTag tag, bool attacking, bool mayTakeLead = false)
	{
		List<CCountryTag> side = attacking ? war.Attackers : war.Defenders;
		if (side.Contains(tag) || state.Country(tag) is not { } joiner)
		{
			return;
		}
		CCountryTag? leader = side.Count > 0 ? side[0] : null;
		bool front;
		if (attacking)
		{
			// A joiner may take the lead only from a subject; a faction's leader takes it from anyone.
			if (state.Country(leader) is { } first && !COfferRules.IsPuppet(first))
			{
				mayTakeLead = false;
			}
			front = LeadsRealFaction(state, joiner) || mayTakeLead;
		}
		else
		{
			front = mayTakeLead && (state.Country(leader) is not { } first || !state.Played.Contains(first.Tag)
				|| COfferRules.IsPuppet(first));
		}
		if (front)
		{
			side.Insert(0, tag);
		}
		else
		{
			side.Add(tag);
		}
		List<CCountryTag> others = attacking ? war.Defenders : war.Attackers;
		if (front && leader is { } led)
		{
			string targets = string.Join(", ", others.Select(o => state.Country(o)?.Name ?? o.Value));
			state.RaisePactMessage(new CPactMessage("ALLYTAKEOVERWE", "ALLYTAKEOVER", "", tag, led)
			{
				Values = new[] { ("TARGETLIST", targets) },
			});
		}
		foreach (CCountryTag other in others.ToArray())
		{
			CPacts.PairWarSet(state, tag, other);
			if (state.Country(other) is { } enemy)
			{
				BringUpToDate(state, joiner);
				BringUpToDate(state, enemy);
			}
		}
	}

	/// <summary>
	/// The country leaves the war, the original's <c>RemoveAttacker</c> or <c>RemoveDefender</c>: off its side,
	/// every war goal it holds and every one against it deleted, and then for each country left on the other
	/// side the pair's war cleared and both brought up to date - which stamps no truce, the two being at peace
	/// by then. Nothing where it is on neither side.
	/// </summary>
	public static void Leave(CGameState state, CWar war, CCountryTag tag)
	{
		bool attacking = war.Attackers.Remove(tag);
		if (!attacking && !war.Defenders.Remove(tag))
		{
			return;
		}
		RemoveGoalsOf(war, tag);
		CCountry? leaver = state.Country(tag);
		foreach (CCountryTag other in (attacking ? war.Defenders : war.Attackers).ToArray())
		{
			CPacts.PairWarSet(state, tag, other);
			if (leaver is not null && state.Country(other) is { } enemy)
			{
				BringUpToDate(state, leaver);
				BringUpToDate(state, enemy);
			}
		}
	}

	/// <summary>The original's <c>CWar::RemoveWarGoalsOf</c>: every goal the country holds, and every goal against it.</summary>
	public static void RemoveGoalsOf(CWar war, CCountryTag tag)
	{
		war.Goals.Remove(tag);
		foreach (List<CWarGoal> goals in war.Goals.Values)
		{
			goals.RemoveAll(goal => goal.Recipient == tag);
		}
	}

	/// <summary>Whether the country is the first member of a real faction.</summary>
	public static bool LeadsRealFaction(CGameState state, CCountry country)
	{
		return COfferRules.RealFaction(country) && COfferRules.Leader(state, country.Faction) == country.Tag;
	}

	/// <summary>
	/// The original's <c>UpdateAtWarAndEnemies</c>: <c>last_war</c> is now towards every country the country
	/// is at war with; at war and not mobilised, it is given <c>initial_mobilization</c> for
	/// <c>MOBILIZATION_BONUS_DAYS</c>; at war with nobody and mobilised, it is taken off where nobody plays it.
	/// </summary>
	public static void BringUpToDate(CGameState state, CCountry country)
	{
		bool atWar = false;
		foreach (CCountry other in state.Countries)
		{
			if (other != country && CCountryLists.AtWar(state, country.Tag, other.Tag))
			{
				atWar = true;
				CGameState.Status(country, other).LastWar = state.TotalHours;
			}
		}
		if (atWar && !country.Mobilised)
		{
			if (state.FindStaticModifier(Mobilisation) is { } modifier)
			{
				int days = (int)(state.Defines["country", "MOBILIZATION_BONUS_DAYS"].Raw / 1000);
				country.EventModifiers.Add(CTimedModifier.Given(state, modifier, days));
			}
			country.Mobilised = true;
		}
		else if (!atWar && country.Mobilised && !state.Played.Contains(country.Tag))
		{
			country.EventModifiers.RemoveAll(given => given.Modifier.Name == Mobilisation);
			country.Mobilised = false;
		}
	}

	/// <summary>Every country brought up to date, as a game is entered.</summary>
	public static void BringAllUpToDate(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			BringUpToDate(state, country);
		}
	}

	/// <summary>The war the two are on opposite sides of, or none.</summary>
	public static CWar? Between(CGameState state, CCountryTag one, CCountryTag other)
	{
		foreach (CWar war in state.Wars)
		{
			if ((war.Attackers.Contains(one) && war.Defenders.Contains(other))
				|| (war.Attackers.Contains(other) && war.Defenders.Contains(one)))
			{
				return war;
			}
		}
		return null;
	}
}
