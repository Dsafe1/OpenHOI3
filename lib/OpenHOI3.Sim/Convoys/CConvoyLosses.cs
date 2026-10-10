using System;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// What a raid takes off a convoy: so many transports and so many escorts, no more than
/// it has, and the hour it happened. Nothing else is charged - the cost is that the
/// convoy carries less from tomorrow, by the share of the transports it would have that
/// it no longer has. See <c>docs/mechanics/navalmissions.md</c>, section 8.
/// </summary>
public static class CConvoyLosses
{
	public static void Lose(CGameState state, CConvoy convoy, int transports, int escorts)
	{
		convoy.Escorts -= Math.Min(Math.Max(escorts, 0), convoy.Escorts);
		convoy.Transports -= Math.Min(Math.Max(transports, 0), convoy.Transports);
		convoy.LastAttackHour = state.TotalHours;
	}
}
