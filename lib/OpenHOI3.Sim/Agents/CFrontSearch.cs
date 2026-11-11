using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Whether a country's agents may look for fronts in, or leave units in, an area another country holds - the original's
/// <c>AreaMayWidenFrontSearch</c>, asked by the area of operations, the stranded units' recall and the expeditions. See
/// <c>docs/mechanics/agents.md</c>, section 8.2.
/// </summary>
public static class CFrontSearch
{
	/// <summary>The provinces an area beside it must hold more than for its war to open it.</summary>
	private const int WarAreaProvinces = 5;

	/// <param name="anyUnplayed">Any holder nobody plays being enough, as the expeditions ask it.</param>
	/// <param name="ownFocus">Set where a priority province of the country's lies in the area, or beside it against an enemy.</param>
	public static bool Allows(CGameState state, CCountry country, CSupplyArea area, bool anyUnplayed, out bool ownFocus)
	{
		ownFocus = false;
		if (area.Provinces.Count == 0 || area.Controller.Value == Revolts.CRebelFactions.Rebels
			|| state.Country(area.Controller) is not { } holder)
		{
			return false;
		}
		if (!CSupplyRelations.IsSameSide(country, holder)
			&& (!CCountryLists.FightingTogether(state, country.Tag, holder.Tag) || CCountryLists.IsEnemy(state, country, holder.Tag)))
		{
			return false;
		}
		if (country.InExile)
		{
			return true;
		}
		if (country.UnitAi.RunsUnits && PriorityInOrBeside(state, country, area))
		{
			ownFocus = true;
			return true;
		}
		if (state.Played.Contains(holder.Tag))
		{
			return false;
		}
		if (anyUnplayed)
		{
			return true;
		}
		if (holder.Overlord == country.Tag || (holder.Faction.Length > 0 && holder.Faction == country.Faction
			&& Agreements.COfferRules.Leader(state, country.Faction) == country.Tag))
		{
			return true;
		}
		// An area beside it of more than five provinces held by a country it is at war with, where the side it fights on
		// is led by a country nobody plays (reversing/findings/FINDINGS-subdivide.md, section 6, step 9).
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (beside.Provinces.Count <= WarAreaProvinces || CWarSides.Between(state, country.Tag, beside.Controller) is not { } war)
			{
				continue;
			}
			var side = war.Attackers.Contains(country.Tag) ? war.Attackers : war.Defenders.Contains(country.Tag) ? war.Defenders : null;
			if (side is { Count: > 0 } && !state.Played.Contains(side[0]))
			{
				return true;
			}
		}
		return false;
	}

	private static bool PriorityInOrBeside(CGameState state, CCountry country, CSupplyArea area)
	{
		if (HoldsPriorityProvince(state, country, area))
		{
			return true;
		}
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (CCountryLists.AtWar(state, country.Tag, beside.Controller) && HoldsPriorityProvince(state, country, beside))
			{
				return true;
			}
		}
		return false;
	}

	private static bool HoldsPriorityProvince(CGameState state, CCountry country, CSupplyArea area)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (int id in country.UnitAi.PriorityProvinces)
		{
			if (ReferenceEquals(network.AreaOf(id), area))
			{
				return true;
			}
		}
		return false;
	}
}
