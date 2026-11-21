using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The Allies' Baltic ban: a country nobody plays, of the United Kingdom's real faction, keeps its fleets' raids, patrols
/// and intercepts out of the Baltic's waters while no great power of that faction holds the Baltic's blocker - the
/// original's <c>CAIUnit_IsBalticBarredToAllies</c>. See <c>docs/mechanics/moving.md</c>, section 14.
/// </summary>
public static class CBalticBan
{
	/// <summary>Whose faction the ban is for [exe].</summary>
	private const string Britain = "ENG";

	/// <summary>The defines' key of the land province that holds the Baltic shut or open [file].</summary>
	private const string Blocker = "BALTIC_BLOCKER";

	/// <summary>The region whose waters are barred, matched by exactly this name [exe; map/region.txt].</summary>
	private const string Region = "baltic_sea_region";

	public static bool IsBarred(CGameState state, CCountry country, int province)
	{
		if (country.Faction.Length == 0 || state.Played.Contains(country.Tag)
			|| state.Country(new CCountryTag(Britain)) is not { } britain || britain.Faction != country.Faction)
		{
			return false;
		}
		if (!state.Defines.Has(Movement.CMovementRules.MapSection, Blocker))
		{
			return false;
		}
		long blocker = state.Defines[Movement.CMovementRules.MapSection, Blocker].Raw / CFixedPoint.Scale;
		if (blocker <= 0 || blocker >= state.Provinces.Count || state.Country(state.Province((int)blocker).Controller) is not { } holder)
		{
			return false;
		}
		// The Baltic is open while a great power of the faction, owning land, holds its blocker.
		if (CCountryLists.OwnedCount(state, holder.Tag) > 0 && holder.Faction == country.Faction
			&& Agreements.COfferRules.IsMajor(holder))
		{
			return false;
		}
		if (province <= 0 || province >= state.Provinces.Count)
		{
			return false;
		}
		foreach (int index in state.Province(province).Regions)
		{
			if (index >= 0 && index < state.Regions.Count && state.Regions[index].Name == Region)
			{
				return true;
			}
		}
		return false;
	}
}
