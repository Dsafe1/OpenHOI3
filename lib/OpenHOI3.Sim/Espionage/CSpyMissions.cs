using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// Every country's spies at work once a day: each presence with spies in a country that owns a province runs its
/// missions, its own presence included, and then its counter-espionage at home once more. The two party boosts, threat
/// and covert-ops points are here; the rest have classes of their own. See <c>docs/mechanics/espionage.md</c>, section 5.
/// </summary>
public static class CSpyMissions
{
	public static void RunDaily(CGameState state)
	{
		CCountryFacts facts = CCountryFacts.Of(state);
		foreach (CCountry country in state.Countries)
		{
			RunCountry(state, facts, country);
		}
	}

	/// <summary>
	/// One country's spies at work, in its own day - only for a country that owns a province or is a government in
	/// exile: one annexed keeps its spies abroad and they do nothing (FINDINGS-liveread.md, section 121).
	/// </summary>
	public static void RunCountry(CGameState state, CCountryFacts facts, CCountry country)
	{
		if (facts.OwnedCount(country.Tag) <= 0 && !country.InExile)
		{
			return;
		}
		foreach (CCountry target in state.Countries)
		{
			if (facts.OwnedCount(target.Tag) <= 0)
			{
				continue;
			}
			CSpyPresence presence = CGameState.Presence(country, target);
			if (presence.Level > 0)
			{
				Run(state, presence, country, target);
			}
		}
		CSpyPresence home = CGameState.Presence(country, country);
		if (home.Weight(ESpyMission.CounterEspionage) != 0)
		{
			CCounterEspionage.Run(state, home, country, country);
		}
	}

	/// <summary>The turn of the month: every country's count of spies caught back to nought, as the screen counts a month's.</summary>
	public static void ResetCaught(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.SpiesCaught = 0;
		}
	}

	/// <summary>One presence's missions, in the original's order, each where its weight is above nought.</summary>
	public static void Run(CGameState state, CSpyPresence presence, CCountry actor, CCountry target)
	{
		if (presence.Weight(ESpyMission.CounterEspionage) > 0)
		{
			CCounterEspionage.Run(state, presence, actor, target);
		}
		if (presence.Weight(ESpyMission.Technology) > 0)
		{
			CTechnologyEspionage.Run(state, presence, actor, target);
		}
		if (presence.Weight(ESpyMission.Military) > 0 && IsMilitaryDay(state, target))
		{
			CMilitaryEspionage.Run(state, presence, actor, target);
		}
		if (presence.Weight(ESpyMission.BoostRulingParty) > 0)
		{
			BoostParty(state, presence, target, target, ESpyMission.BoostRulingParty);
		}
		if (presence.Weight(ESpyMission.BoostOurParty) > 0)
		{
			BoostParty(state, presence, actor, target, ESpyMission.BoostOurParty);
		}
		if (presence.Weight(ESpyMission.IncreaseThreat) > 0)
		{
			long amount = Effect(presence, state.Defines["country", "SPY_THREAT_INCREASE_ACTION"].Raw, ESpyMission.IncreaseThreat)
				* 1000 / 10000;
			CThreat.SpreadToAll(state, amount, 1000, target);
		}
		if (presence.Weight(ESpyMission.CovertOps) > 0)
		{
			// The define is a month's gain, so a day's is a thirtieth of it.
			long gain = Effect(presence, state.Defines["country", "SPY_COVERT_OPS_GAIN"].Raw, ESpyMission.CovertOps) * 1000 / 30000;
			presence.CovertOpsPoints += gain;
			long most = state.Defines["country", "SPY_COVERT_OPS_MAX"].Raw;
			if (presence.CovertOpsPoints > most)
			{
				presence.CovertOpsPoints = most;
			}
		}
	}

	/// <summary>
	/// Military espionage runs on the days of the month (0 for the first) where the day and the target's number in the
	/// original's list - which counts its placeholder, so REB is 1 - come to a multiple of eight.
	/// </summary>
	public static bool IsMilitaryDay(CGameState state, CCountry target)
	{
		return (state.Date.Day - 1 + target.Index + 1) % 8 == 0;
	}

	/// <summary>The spies' level, times a define, times the mission's share: what every mission's figure starts from.</summary>
	public static long Effect(CSpyPresence presence, long define, ESpyMission mission)
	{
		return presence.Level * define / 1000 * presence.Share(mission) / 1000;
	}

	/// <summary>
	/// The organisation of <paramref name="whose"/> ruling party in the target's figures raised; nothing written where it
	/// would come out below nought, and the new figure added to the spies' tally.
	/// </summary>
	private static void BoostParty(CGameState state, CSpyPresence presence, CCountry whose, CCountry target, ESpyMission mission)
	{
		long boost = Effect(presence, state.Defines["country", "SPY_PARTY_ORGANIZATION_BOOST"].Raw, mission);
		int party = state.Ideology(whose.RulingIdeology)?.Index ?? 0;
		target.Organisation.Set(party, target.Organisation[party] + boost, CPartyFigures.SpySlot);
	}
}
