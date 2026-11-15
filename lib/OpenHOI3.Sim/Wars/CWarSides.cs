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
			// And every foreign minister is told of the pair (foreignminister.md, section 8).
			Ai.CForeignAi.TellWar(state, tag, other, war);
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
				// A change of diplomacy: a country now at peace loses the laws only a war allowed (politics.md, section 9).
				Politics.CLaws.OnDiplomacyChanged(state, enemy);
			}
		}
		if (leaver is not null)
		{
			Politics.CLaws.OnDiplomacyChanged(state, leaver);
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
	/// is at war with; in a running game its AI strategy is made again (aistrategy.md, section 1); at war and not
	/// mobilised, it is given <c>initial_mobilization</c> for <c>MOBILIZATION_BONUS_DAYS</c>; at war with nobody and
	/// mobilised, it is taken off - with <paramref name="allowDemobilise"/>, unless <paramref name="suppressDemobilise"/>;
	/// without, only where nobody plays it. Entering a game passes the first and, for a loaded game, the second; a change
	/// of a war's sides passes neither (reversing/findings/FINDINGS-war.md, A.9).
	/// </summary>
	public static void BringUpToDate(CGameState state, CCountry country, bool allowDemobilise = false,
		bool suppressDemobilise = false)
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
		Ai.CAiStrategies.Remake(state, country);
		if (atWar && !country.Mobilised)
		{
			if (state.FindStaticModifier(Mobilisation) is { } modifier)
			{
				int days = (int)(state.Defines["country", "MOBILIZATION_BONUS_DAYS"].Raw / 1000);
				country.EventModifiers.Add(CTimedModifier.Given(state, modifier, days));
			}
			country.Mobilised = true;
		}
		else if (!atWar && country.Mobilised
			&& (allowDemobilise ? !suppressDemobilise : !state.Played.Contains(country.Tag)))
		{
			country.EventModifiers.RemoveAll(given => given.Modifier.Name == Mobilisation);
			country.Mobilised = false;
		}
	}

	/// <summary>
	/// Every country brought up to date, as a game is entered: a country mobilised at peace is taken off, played or not,
	/// but not in a loaded game, which keeps the mobilisation its save gave.
	/// </summary>
	public static void BringAllUpToDate(CGameState state, bool loaded = false)
	{
		foreach (CCountry country in state.Countries)
		{
			BringUpToDate(state, country, allowDemobilise: true, suppressDemobilise: loaded);
		}
	}

	/// <summary>
	/// What the history's wars did as they were replayed, before a new game is entered: each country added to a side was
	/// brought up to date there, with the game's clock at the defines' <c>start_date</c> - so every country at war on the
	/// date is mobilised already, its <c>initial_mobilization</c> counted from the start date and gone by a later start, and
	/// each joiner's record towards the countries then on the other side has <c>last_war</c> at the start date
	/// (war.md, section 4; measured, the bookmarks' start saves). Every country on either side of such a meeting was
	/// mobilised there and had its modifier made, as giving a timed modifier makes it - the other side's too, which the
	/// saves' threat shows and their <c>last_war</c> does not (inferred) - and <see cref="CGameState.MobilisedByHistory"/>
	/// keeps them for the new game's spreading of history threat (diplomacy.md, section 5.3).
	/// </summary>
	/// <param name="joins">Each country added to a side by the history, and each country then on the other side, in turn.</param>
	public static void SetFromHistory(CGameState state, IEnumerable<(CCountryTag Joiner, CCountryTag Enemy)> joins)
	{
		long start = state.StartDate.DayNumber * (long)CGameState.HoursPerDay;
		state.MobilisedByHistory.Clear();
		foreach ((CCountryTag joiner, CCountryTag enemy) in joins)
		{
			if (state.Country(joiner) is { } country && state.Country(enemy) is { } other && country != other)
			{
				CGameState.Status(country, other).LastWar = start;
				state.MobilisedByHistory.Add(joiner);
				state.MobilisedByHistory.Add(enemy);
			}
		}
		int days = (state.Defines.Has("country", "MOBILIZATION_BONUS_DAYS")
			? (int)(state.Defines["country", "MOBILIZATION_BONUS_DAYS"].Raw / 1000) : 0)
			- (state.Date.DayNumber - state.StartDate.DayNumber);
		foreach (CCountry country in state.Countries)
		{
			if (!state.IsAtWar(country.Tag) || country.Mobilised)
			{
				continue;
			}
			if (days > 0 && state.FindStaticModifier(Mobilisation) is { } modifier)
			{
				country.EventModifiers.Add(CTimedModifier.Given(state, modifier, days));
			}
			country.Mobilised = true;
		}
	}

	/// <summary>
	/// The original's <c>RemoveLandlessCountriesFromWars</c>, as a new game's entry ends: every country owning no province
	/// and not in exile taken off each war it is on - a side left empty leaves the war standing - and, in a real faction,
	/// out of it, its modifier made again (war.md, section 4; measured, the 1941 bookmarks' start saves, whose Invasion of the
	/// Baltic States has Germany alone).
	/// </summary>
	public static void RemoveLandless(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (country.InExile || CCountryLists.OwnedCount(state, country.Tag) > 0)
			{
				continue;
			}
			foreach (CWar war in state.Wars.ToArray())
			{
				Leave(state, war, country.Tag);
			}
			if (COfferRules.RealFaction(country))
			{
				CFactionMembership.Leave(state, country);
				CCountryModifiers.RebuildOne(state, country);
			}
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
