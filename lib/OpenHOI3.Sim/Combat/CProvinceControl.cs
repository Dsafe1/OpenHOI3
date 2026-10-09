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
	/// The original has three more cases, each waiting for what it reads: a puppet's
	/// conquest goes to its master, a government in exile's to the head of its faction,
	/// and a province none of whose neighbours the country holds to a friend who holds
	/// one. Answers whether the province changed hands.
	/// </summary>
	public static bool Take(CGameState state, CProvince province, CCountryTag by)
	{
		if (province.IsSea || province.Owner is null || !CMovementRules.IsEnemy(state, province.Controller, by))
		{
			return false;
		}
		CCountryTag to = CMovementRules.IsEnemy(state, province.Owner, by) ? by : province.Owner.Value;
		if (province.Controller == to)
		{
			return false;
		}
		province.Controller = to;
		state.ControlChanges++;
		return true;
	}
}
