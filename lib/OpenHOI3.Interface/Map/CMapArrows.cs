using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// Which of the original's arrows a selected unit's way is drawn with. See
/// <c>docs/mechanics/movement.md</c>, section 7.
/// </summary>
public static class CMapArrows
{
	/// <summary>
	/// The name of the arrow type for a unit with somewhere to go: the retreat's for a
	/// unit falling back, the landing's for a land unit going ashore from the sea on
	/// anybody's ground (<c>transport.md</c>, section 12), the attack's where an enemy of
	/// its country holds the next province of its way - whether or not anybody stands
	/// there - the redeployment's under a strategic redeployment, and the march's otherwise.
	/// </summary>
	public static string TypeOf(CGameState state, CUnit unit)
	{
		if (unit.IsRetreating)
		{
			return CArrowTypes.Retreat;
		}
		if (Sim.Transport.CLanding.IsLanding(state, unit))
		{
			return CArrowTypes.Invasion;
		}
		if (unit.Path.Count > 0 && unit.Path[0] > 0 && unit.Path[0] < state.Provinces.Count
			&& state.Province(unit.Path[0]).Controller is { } holder
			&& CMovementRules.IsEnemy(state, unit.Owner, holder))
		{
			return CArrowTypes.Attack;
		}
		if (unit.Order == EUnitOrder.StrategicRedeployment)
		{
			return CArrowTypes.Strategic;
		}
		return CArrowTypes.Move;
	}
}
