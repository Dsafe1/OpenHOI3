using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A province changes hands to whoever walked into it. Who that is, is not always the
/// one who walked: ground taken back from an enemy goes to the friend who owns it. See
/// <c>docs/mechanics/landcombat.md</c>, section 7.
/// </summary>
public static class CProvinceControl
{
	/// <summary>
	/// A country's unit stands in a province with no enemy beside it. Nothing changes
	/// where whoever holds the province is no enemy of that country. Otherwise it goes
	/// to the province's owner where the owner is no enemy either, and to the country
	/// itself where it is.
	///
	/// A government in exile's conquest goes to the head of its faction, where that faction
	/// has two or more members and it is not the head; and ground freed whose owner is an
	/// exile stays with whoever freed it (<c>war.md</c>, section 14).
	///
	/// The original has two more cases, each waiting for what it reads: a puppet's
	/// conquest goes to its master, and a province none of whose neighbours the country
	/// holds to a friend who holds one. Answers whether the province changed hands.
	/// </summary>
	public static bool Take(CGameState state, CProvince province, CCountryTag by)
	{
		if (province.IsSea || province.Owner is null || !CMovementRules.IsEnemy(state, province.Controller, by))
		{
			return false;
		}
		if (state.Country(by) is { InExile: true } exile)
		{
			var members = CCountryLists.FactionMembers(state, exile.Faction);
			if (members.Count >= 2 && members[0] != exile)
			{
				by = members[0].Tag;
			}
		}
		bool freedForExile = state.Country(province.Owner) is { InExile: true };
		CCountryTag to = CMovementRules.IsEnemy(state, province.Owner, by) || freedForExile ? by : province.Owner.Value;
		if (province.Controller == to)
		{
			return false;
		}
		province.Controller = to;
		return true;
	}
}
