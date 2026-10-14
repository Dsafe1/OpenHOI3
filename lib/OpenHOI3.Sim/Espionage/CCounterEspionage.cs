using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// Counter-espionage: the actor's spies in the target looking for every other country's spies there, a whole spy at a
/// time. At home a country's own spies on it keep the whole chance, and a friend's hostile spies are caught on the
/// second roll alone. See <c>docs/mechanics/espionage.md</c>, section 6.
/// </summary>
public static class CCounterEspionage
{
	/// <summary>The share of the chance home spies keep with none of them on counter-espionage: a fifth, as 1 - 0.8.</summary>
	private const long HomeLoss = 800;

	/// <summary>What a friend's hostile spies add to the chance: more than the roll can reach.</summary>
	private const long Certain = 1000000;

	/// <summary>The second roll: one in ten.</summary>
	private const int SecondRoll = 10;

	public static void Run(CGameState state, CSpyPresence presence, CCountry actor, CCountry target)
	{
		bool home = actor == target;
		long share = presence.Share(ESpyMission.CounterEspionage);
		long chanceBase = state.Defines["country", "SPY_DETECTION_CHANCE"].Raw;
		if (home)
		{
			chanceBase += actor.Modifier["counter_espionage"].Raw;
		}
		long chance = presence.Level * chanceBase / 1000 * share / 1000;
		if (home)
		{
			chance = chance * (1000 - HomeLoss * (1000 - share) / 1000) / 1000;
		}

		CCountryFacts facts = CCountryFacts.Of(state);
		foreach (CCountry other in state.Countries)
		{
			if (other == actor || facts.OwnedCount(other.Tag) <= 0)
			{
				continue;
			}
			CSpyPresence theirs = CGameState.Presence(other, target);
			if (theirs.Level < 1000)
			{
				continue;
			}
			long roll = chance;
			if (CCountryLists.IsFriend(state, actor, other.Tag, withAccess: true))
			{
				if (!home || !IsHostile(state, theirs, other, target))
				{
					continue;
				}
				roll += Certain;
			}
			// The target's own bonus, and it lowers the chance.
			roll = roll * (1000 - target.Modifier["espionage_bonus"].Raw) / 1000;
			if (!theirs.HasAnyMission())
			{
				roll = roll * state.Defines["country", "NO_MISSION_SPY_DETECTION_MULTIPLIER"].Raw / 1000;
			}
			if (state.Random.Next() % 1000 >= roll)
			{
				continue;
			}
			if (state.Random.Next() % 100 >= SecondRoll)
			{
				continue;
			}
			theirs.AddLevel(-1000);
			if (home)
			{
				actor.SpiesCaught++;
			}
			CSpyMessages.Caught(state, actor, other, target);
		}
	}

	/// <summary>
	/// Whether a presence's missions are hostile: military, technology, boost our party, lower national unity, increase
	/// threat or covert ops, or counter-espionage in a country that is not its owner's friend (access not counting).
	/// </summary>
	public static bool IsHostile(CGameState state, CSpyPresence presence, CCountry owner, CCountry target)
	{
		foreach (ESpyMission mission in new[] { ESpyMission.Military, ESpyMission.Technology, ESpyMission.BoostOurParty,
			ESpyMission.LowerNationalUnity, ESpyMission.IncreaseThreat, ESpyMission.CovertOps })
		{
			if (presence.Weight(mission) > 0)
			{
				return true;
			}
		}
		return presence.Weight(ESpyMission.CounterEspionage) > 0 && !CCountryLists.IsFriend(state, target, owner.Tag, withAccess: false);
	}
}
