using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The colour of a province while the air orders window is up: the window's own, not a
/// map mode's. See <c>docs/mechanics/fleetsandair.md</c>, section 4:
///
///     none        out of the unit's range
///     pale cyan   on the mission's list
///     green       sea, or land the unit's country holds
///     red         land of a country it is at war with
///     grey        other land
/// </summary>
public static class CAirAreaColours
{
	private static readonly CColor Own = Lift(0.4f, 0.7f, 0.4f);
	private static readonly CColor Enemy = Lift(0.7f, 0.4f, 0.4f);
	private static readonly CColor Other = Lift(0.4f, 0.4f, 0.4f);
	private static readonly CColor Listed = new(0.5f, 0.8f, 0.8f);

	/// <summary>The original brightens the three colours of the unit's reach by a fifth each.</summary>
	private static CColor Lift(float red, float green, float blue)
	{
		const double Brighter = 0.20000000298023224;
		return new CColor((float)(red + Brighter), (float)(green + Brighter), (float)(blue + Brighter));
	}

	/// <param name="listed">The provinces of the mission being made.</param>
	/// <returns>The province's colour, or null where the unit cannot reach it.</returns>
	public static CColor? Of(CGameState state, CUnit unit, EUnitOrder order, IReadOnlySet<int> listed,
		CProvince province)
	{
		if (province.Id == 0 || !CUnitOrders.IsInRange(state, unit, order, province))
		{
			return null;
		}
		if (listed.Contains(province.Id))
		{
			return Listed;
		}
		if (province.IsSea || province.Controller == unit.Owner)
		{
			return Own;
		}
		return CMovementRules.IsEnemy(state, province.Controller, unit.Owner) ? Enemy : Other;
	}
}
