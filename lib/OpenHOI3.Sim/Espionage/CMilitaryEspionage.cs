using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// Military espionage: a roll for every unit of the target's, and for each one found a grant of 2 to 9 for
/// <c>SPY_MILITARY_INTEL_TIME_BASE</c> of its days. See <c>docs/mechanics/espionage.md</c>, section 7.
/// </summary>
public static class CMilitaryEspionage
{
	/// <summary>What the second roll's share is multiplied by: <c>floor(2000.5f)</c> in the original.</summary>
	private const long Spread = 2000;

	public static void Run(CGameState state, CSpyPresence presence, CCountry actor, CCountry target)
	{
		int days = (int)(state.Defines["country", "SPY_MILITARY_INTEL_TIME_BASE"].Raw / 1000);
		long share = presence.Share(ESpyMission.Military);
		long chance = state.Defines["country", "MILITARY_ESPIONAGE_DETECTION_CHANCE"].Raw * 10000 / 1000 * presence.Level / 1000
			* share / 1000;
		long land = 1000 + actor.Modifier["offmap_land_intel"].Raw;
		long naval = 1000 + actor.Modifier["offmap_naval_intel"].Raw;
		long atBase = state.Defines["country", "MILITARY_ESPIONAGE_AT_BASE_CHANCE"].Raw;

		foreach (CUnit unit in target.UnitsInOrder())
		{
			long c = chance;
			switch (unit.Kind)
			{
				case EUnitKind.Air:
					// No offmap modifier for an air unit.
					if (CAirMissions.IsAtOwnBase(unit))
					{
						c = atBase * chance / 1000;
					}
					break;
				case EUnitKind.Navy:
					c = naval * chance / 1000;
					if (CAirMissions.IsAtOwnBase(unit))
					{
						c = c * atBase / 1000;
					}
					break;
				default:
					c = land * chance / 1000;
					break;
			}
			// Made whole by the processor, to the nearest and halves to even, where everything else truncates.
			long percent = (long)Math.Round((float)c / 1000.0, MidpointRounding.ToEven);
			if (state.Random.Next() % 1000 >= percent)
			{
				continue;
			}
			long spread = Math.Min(state.Random.Next() % 1000 * (Spread * share / 1000) / 1000, 1000);
			int level = (int)(spread * 7000 / 1000 / 1000 + 2);
			CIntelGrants.Grant(unit, actor.Tag, days, level);
		}
	}
}
