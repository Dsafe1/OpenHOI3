using System;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// A coup in another country, which spends the instigator's spies there and twenty covert-ops points, and fires
/// <c>on_coup_success</c> or <c>on_coup_fail</c> for the target by a roll on the seats the instigator's party group
/// holds in its cabinet. See <c>docs/mechanics/espionage.md</c>, section 11.
/// </summary>
public static class CCoup
{
	public const string Succeeded = "on_coup_success";
	public const string Failed = "on_coup_fail";

	/// <summary>The target's cabinet seats held by parties of the instigator's ruling party's group.</summary>
	public static int Seats(CGameState state, CCountry instigator, CCountry target)
	{
		if (state.Ideology(instigator.RulingIdeology)?.Group is not { } group)
		{
			return 0;
		}
		int seats = 0;
		foreach (CIdeology ideology in state.Ideologies)
		{
			if (ideology.Group == group)
			{
				seats += CCabinet.Seats(target, ideology.Index);
			}
		}
		return seats;
	}

	/// <summary>
	/// Whether one may be tried, as the screen asks it: the two ruling groups differ, the target's government is
	/// fractured, and the instigator's group holds a seat in it. The screen asks the spies and the points besides.
	/// </summary>
	public static bool CanAttempt(CGameState state, CCountry instigator, CCountry target)
	{
		return state.Ideology(instigator.RulingIdeology)?.Group != state.Ideology(target.RulingIdeology)?.Group
			&& CCabinet.IsFractured(state, target) && Seats(state, instigator, target) > 0;
	}

	/// <summary>
	/// Its chance in thousandths: a fifth for each seat, times what national unity leaves of a hundred; nought where the
	/// target leads a real faction, a third where it is any other member of one.
	/// </summary>
	public static long Chance(CGameState state, CCountry instigator, CCountry target)
	{
		long chance = Seats(state, instigator, target) * 1000L * state.Defines["country", "COUP_CHANCE_PER_POSITION"].Raw / 1000
			* (1000 - target.NationalUnity.Raw * 1000 / 100000) / 1000;
		if (COfferRules.RealFaction(target))
		{
			chance = COfferRules.Leader(state, target.Faction) == target.Tag ? 0 : chance / 3;
		}
		return chance;
	}

	/// <summary>The attempt: nothing re-checked, the spies and the points spent, and the roll.</summary>
	public static void Perform(CGameState state, CCountry instigator, CCountry target)
	{
		CSpyPresence presence = CGameState.Presence(instigator, target);
		presence.Level = 0;
		presence.CovertOpsPoints = Math.Max(presence.CovertOpsPoints - state.Defines["country", "COUP_COVERT_OPS_COST"].Raw, 0);
		bool succeeds = state.Random.Next() % 1000 < Chance(state, instigator, target);
		COnActions.Fire(state, succeeds ? Succeeded : Failed, CEventScope.Start(target.Tag, fromCountry: instigator.Tag));
	}
}
