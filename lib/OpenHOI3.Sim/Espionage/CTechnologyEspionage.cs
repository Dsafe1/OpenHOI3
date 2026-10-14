using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// Stealing technology: a first roll on the spies, a second on how far ahead the target is across the whole list of
/// technologies, and a pick weighted to small gaps and easy technologies, which then gives the actor the target's
/// level outright. See <c>docs/mechanics/espionage.md</c>, section 8.
/// </summary>
public static class CTechnologyEspionage
{
	public static void Run(CGameState state, CSpyPresence presence, CCountry actor, CCountry target)
	{
		long share = presence.Share(ESpyMission.Technology);
		long percent = CSpyMissions.Effect(presence, state.Defines["country", "TECH_ESPIONAGE_BASE_CHANCE"].Raw, ESpyMission.Technology)
			/ 1000;
		if (state.Random.Next() % 100 > percent)
		{
			return;
		}

		// The original's list begins with a null technology, which weighs nothing and counts in the mean.
		var technologies = new List<CTechnology?> { null };
		technologies.AddRange(state.Technologies.Values);
		var weights = new long[technologies.Count];
		long sum = 0;
		for (int i = 1; i < technologies.Count; i++)
		{
			CTechnology technology = technologies[i]!;
			int theirs = target.TechnologyLevels.GetValueOrDefault(technology.Name);
			int ours = actor.TechnologyLevels.GetValueOrDefault(technology.Name);
			if (!technology.Stealable || theirs <= ours)
			{
				continue;
			}
			weights[i] = 10000 / (theirs - ours) / Math.Max(technology.Difficulty, 1);
			sum += weights[i];
		}

		long mean = sum * 1000 / ((long)technologies.Count * 10000);
		long chance = mean * state.Defines["country", "TECH_ESPIONAGE_TECH_PICK_FACTOR"].Raw / 1000 * share / 1000;
		if (state.Random.Next() % 1000 >= chance)
		{
			return;
		}

		int picked = Pick(weights, state.Random.Next());
		if (picked <= 0)
		{
			return;
		}
		CTechnology stolen = technologies[picked]!;
		int level = target.TechnologyLevels.GetValueOrDefault(stolen.Name);
		actor.ResearchProgress[stolen.Name] = CFixed15.Zero;
		if (level > actor.TechnologyLevels.GetValueOrDefault(stolen.Name))
		{
			// However many levels that is, and with no maximum level, allow or prerequisite asked.
			actor.TechnologyLevels[stolen.Name] = level;
			CCountryModifiers.RebuildTechnology(state, actor);
		}
		actor.ResearchQueue.Remove(stolen.Name);
		CSpyMessages.TechnologyStolen(state, actor, target, stolen, level);
	}

	/// <summary>The first place whose running total of the positive weights exceeds the draw over their total; -1 for none.</summary>
	public static int Pick(long[] weights, int draw)
	{
		long total = 0;
		foreach (long weight in weights)
		{
			if (weight > 0)
			{
				total += weight;
			}
		}
		if (total <= 0)
		{
			return -1;
		}
		long at = draw % total;
		long running = 0;
		for (int i = 0; i < weights.Length; i++)
		{
			if (weights[i] > 0)
			{
				running += weights[i];
				if (running > at)
				{
					return i;
				}
			}
		}
		return -1;
	}
}
