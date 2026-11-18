using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// The end of a theatre build or refresh: the friends told. For a country holding land, every other country it fights a
/// war beside and is not at war with - the rebels never - that holds land itself has each of its agents bound to a theatre
/// headquarters marked to set its area again where any theatre of ours is connected to that agent's theatre: the two share
/// an area, or our headquarters can reach theirs by land. Its next hour is then a full pass. The original's
/// <c>CAIStrategy_TellFriendsOfTheatres</c>, <c>CEU3AI_MarkAgentsNearTheatres</c> and <c>CTheatre_IsConnectedTo</c>; its list
/// of those countries, <c>non_hostile_countries</c>, is made once a day and is asked here as it stands
/// (<c>reversing/findings/FINDINGS-liveread.md</c>, section 83; <c>docs/mechanics/theatres.md</c>, section 8).
/// </summary>
public static class CTheatreFriends
{
	public static void Tell(CGameState state, CCountry country)
	{
		if (state.ControlledCount(country.Tag) < 1)
		{
			return;
		}
		foreach (CCountry other in state.Countries)
		{
			if (other.Tag == country.Tag || !IsNonHostile(state, country, other) || state.ControlledCount(other.Tag) <= 0)
			{
				continue;
			}
			foreach (CAgent agent in other.UnitAi.Agents)
			{
				if (agent.Unit is not { Kind: EUnitKind.Theatre } || CAgents.TheatreOf(state, other, agent) is not { } theirs)
				{
					continue;
				}
				foreach (CTheatre ours in country.Strategy.Theatres)
				{
					if (IsConnected(state, country, ours, theirs))
					{
						agent.AreaDirty = true;
					}
				}
			}
		}
	}

	/// <summary>The original's membership of <c>non_hostile_countries</c>, for another country: fighting beside it, not at war with it.</summary>
	public static bool IsNonHostile(CGameState state, CCountry country, CCountry other)
	{
		return CCountryLists.FightingTogether(state, country.Tag, other.Tag) && !CCountryLists.AtWar(state, country.Tag, other.Tag)
			&& country.Tag.Value != Revolts.CRebelFactions.Rebels && other.Tag.Value != Revolts.CRebelFactions.Rebels;
	}

	/// <summary>
	/// The original's <c>CTheatre_IsConnectedTo</c>: an area the two share, or else our headquarters able to reach the province
	/// theirs stands in by land.
	/// </summary>
	private static bool IsConnected(CGameState state, CCountry country, CTheatre ours, CTheatre theirs)
	{
		foreach (CSupplyArea area in ours.Areas)
		{
			if (theirs.Areas.Contains(area))
			{
				return true;
			}
		}
		return CTheatres.Headquarters(state, ours) is { } mine && CTheatres.Headquarters(state, theirs) is { Location: > 0 } other
			&& CAiMoves.ReachesByLand(state, country, mine, other.Location);
	}
}
