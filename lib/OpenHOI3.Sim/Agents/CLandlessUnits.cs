using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A country with no land and no government in exile hands its units to the other members of its faction every hour,
/// before its hour takes them out of the game: a fleet or an air unit to the faction's leader, a land unit to the member
/// holding the province it stands in - or to the leader where a country of no faction or another holds it. The
/// original's <c>CEU3AI_SendLandlessUnitsOnExpedition</c>, which its AI pass runs in place of everything else for such a
/// country. See <c>docs/mechanics/expeditions.md</c>, section 6.
/// </summary>
public static class CLandlessUnits
{
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		foreach (CCountry country in state.Countries)
		{
			// No test of a person, here or in the pass before it (inferred, a played country's not seen in play).
			if (!country.AiPaused && CMovement.IsLandless(state, country))
			{
				Run(state, country);
			}
		}
	}

	public static void Run(CGameState state, CCountry country)
	{
		List<CCountry> members = CCountryLists.FactionMembers(state, country.Faction);
		if (members.Count == 0)
		{
			return;
		}
		CCountry leader = members[0];
		// Every unit at every level but a theatre, in the country's own order; one sent with its superior's tree is no
		// longer the country's when its own command comes to be run, and is refused.
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.Kind == EUnitKind.Theatre)
			{
				continue;
			}
			foreach (CCountry member in members)
			{
				if (member == country || member.InExile)
				{
					continue;
				}
				if (Receives(state, country, unit, member, member == leader))
				{
					state.Outbox.Add(new CSendExpeditionCommand(country.Tag, unit.Id, member.Tag));
				}
			}
		}
	}

	/// <summary>Whether the member is sent the unit: the leader its fleets and air units, and each member the land units on its ground.</summary>
	private static bool Receives(CGameState state, CCountry country, CUnit unit, CCountry member, bool isLeader)
	{
		bool land = CMovementRules.IsLandUnit(unit);
		if (!land)
		{
			return isLeader;
		}
		CCountryTag? holder = unit.Location > 0 ? state.Province(unit.Location).Controller : null;
		if (holder == member.Tag)
		{
			return true;
		}
		if (!isLeader)
		{
			return false;
		}
		// The leader also takes those on ground a country outside the faction holds, or nobody.
		return holder is not { } tag || state.Country(tag) is not { } holding || holding.Faction != country.Faction;
	}
}
