using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// What a raid takes off a convoy: so many transports and so many escorts, no more than
/// it has, and the hour it happened. Nothing else is charged - the cost is that the
/// convoy carries less from tomorrow, by the share of the transports it would have that
/// it no longer has - but the raider's threat, which every ship sunk spreads. See
/// <c>docs/mechanics/navalmissions.md</c>, section 8, and <c>docs/mechanics/diplomacy.md</c>, 5.3.
/// </summary>
public static class CConvoyLosses
{
	/// <summary>The sounds of a raid's hit: the convoy's owner's, and the raider's.</summary>
	public const string LostSound = "convoys_1";
	public const string HitSound = "convoys_2";

	public static void Lose(CGameState state, CConvoy convoy, int transports, int escorts, CCountry? raider = null)
	{
		int escortsLost = Math.Min(Math.Max(escorts, 0), convoy.Escorts);
		int transportsLost = Math.Min(Math.Max(transports, 0), convoy.Transports);
		convoy.Escorts -= escortsLost;
		convoy.Transports -= transportsLost;
		if (raider is not null)
		{
			CThreat.ConvoyShipsLost(state, raider, transportsLost + escortsLost);
		}
		// The convoy's owner hears its ships lost and the raider its hit, a raid that sank something being the only caller
		// (sound.md, section 7).
		state.RaiseSound(LostSound, convoy.Owner);
		if (raider is not null)
		{
			state.RaiseSound(HitSound, raider.Tag);
		}
		convoy.LastAttackHour = state.TotalHours;
	}
}
