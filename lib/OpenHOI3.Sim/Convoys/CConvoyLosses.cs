using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// What a raid takes off a convoy: so many transports and so many escorts, no more than
/// it has, and the hour it happened. Nothing else is charged - the cost is that the
/// convoy carries less from tomorrow, by the share of the transports it would have that
/// it no longer has - but the raider's threat, which every ship sunk spreads. The convoy's
/// owner is told what it lost and the raider's country what it sank. See
/// <c>docs/mechanics/navalmissions.md</c>, sections 8 and 8.2, and <c>docs/mechanics/diplomacy.md</c>, 5.3.
/// </summary>
public static class CConvoyLosses
{
	/// <summary>The sounds of a raid's hit: the convoy's owner's, and the raider's.</summary>
	public const string LostSound = "convoys_1";
	public const string HitSound = "convoys_2";

	/// <summary>What a lend-lease convoy's own efficiency loses for all its transports sunk, in the <c>economy</c> defines.</summary>
	public const string LendLeaseImpact = "LL_CONVOY_EFF_IMPACT";

	/// <param name="province">The sea province of the raid, which the two messages name; none raises no message.</param>
	public static void Lose(CGameState state, CConvoy convoy, int transports, int escorts, CCountry? raider = null,
		int province = 0)
	{
		int escortsLost = Math.Min(Math.Max(escorts, 0), convoy.Escorts);
		int transportsLost = Math.Min(Math.Max(transports, 0), convoy.Transports);
		// A lend-lease convoy loses LL_CONVOY_EFF_IMPACT times the share of its transports sunk (aid.md, section 3).
		if (convoy.IsLendLease && transportsLost > 0 && convoy.Transports > 0)
		{
			long share = transportsLost * (long)CFixedPoint.Scale / convoy.Transports;
			long impact = state.Defines[Economy.CResources.EconomySection, LendLeaseImpact].Raw;
			convoy.LendLeaseEfficiency = Math.Max(0, convoy.LendLeaseEfficiency - impact * share / CFixedPoint.Scale);
		}
		convoy.Escorts -= escortsLost;
		convoy.Transports -= transportsLost;
		if (raider is not null)
		{
			CThreat.ConvoyShipsLost(state, raider, transportsLost + escortsLost);
		}
		// The convoy's owner hears its ships lost and the raider its hit, a raid that sank something being the only caller
		// (sound.md, section 7) - and each is told it with the counts as taken off.
		state.RaiseSound(LostSound, convoy.Owner);
		if (raider is not null)
		{
			state.RaiseSound(HitSound, raider.Tag);
			if (province > 0)
			{
				foreach ((string type, CCountryTag told) in new[]
				{
					(CConvoyRaidMessage.Lost, convoy.Owner),
					(CConvoyRaidMessage.Sank, raider.Tag),
				})
				{
					state.RaiseConvoyRaidMessage(new CConvoyRaidMessage(type, told, province, convoy.Owner, raider.Tag,
						convoy.Start, convoy.End) { Transports = transportsLost, Escorts = escortsLost });
				}
			}
		}
		convoy.LastAttackHour = state.TotalHours;
	}
}
