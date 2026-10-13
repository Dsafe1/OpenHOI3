using System.Linq;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Aid;

/// <summary>
/// Lend-lease between two countries: who may and by which way, beginning and ending it, and the hour's
/// look at every giving - ended where it may no longer be, and its effectiveness set where it may: 1000
/// over land, the receiver's convoy's efficiency by sea. See <c>docs/mechanics/aid.md</c>, section 3.
/// </summary>
public static class CLendLease
{
	public const string NeutralityLimitDefine = "LEND_LEASE_NEUTRALITY_LIMIT";

	/// <summary>Why lend-lease may not go from a giver to a receiver, in the original's order; none where it may.</summary>
	public enum ERefusal
	{
		None,
		InExile,
		GiverNeutrality,
		ReceiverAtPeace,
		AtWarWithEachOther,
		NoWay,
	}

	/// <summary>The first reason lend-lease may not go from the giver to the receiver, or none.</summary>
	public static ERefusal Refusal(CGameState state, CCountry giver, CCountry receiver)
	{
		if (Agreements.COfferRules.InExile())
		{
			return ERefusal.InExile;
		}
		if (giver.EffectiveNeutrality > state.Defines[Agreements.COfferRules.Section, NeutralityLimitDefine])
		{
			return ERefusal.GiverNeutrality;
		}
		if (!state.IsAtWar(receiver.Tag))
		{
			return ERefusal.ReceiverAtPeace;
		}
		if (CCountryLists.AtWar(state, giver.Tag, receiver.Tag))
		{
			return ERefusal.AtWarWithEachOther;
		}
		return Way(state, giver, receiver) == ELendLeaseRoute.Refused ? ERefusal.NoWay : ERefusal.None;
	}

	/// <summary>
	/// The way, past the refusals: over land where they are neighbours or the giver holds land beside the
	/// receiver's; at once where their capitals are on one continent; by sea where both have a harbour.
	/// </summary>
	public static ELendLeaseRoute Way(CGameState state, CCountry giver, CCountry receiver)
	{
		CCountryFacts facts = CCountryFacts.Of(state);
		if (facts.AreNeighbours(giver.Tag, receiver.Tag) || facts.AreControllerNeighbours(receiver.Tag, giver.Tag))
		{
			return ELendLeaseRoute.Neighbour;
		}
		if (giver.Capital > 0 && receiver.Capital > 0
			&& state.Province(giver.Capital).Continent is { Length: > 0 } continent
			&& continent == state.Province(receiver.Capital).Continent)
		{
			return ELendLeaseRoute.Teleport;
		}
		return facts.HasHarbour(giver.Tag) && facts.HasHarbour(receiver.Tag) ? ELendLeaseRoute.Convoy : ELendLeaseRoute.Refused;
	}

	/// <summary>Whether the giver gives the receiver lend-lease.</summary>
	public static bool Gives(CCountry giver, CCountry receiver) => giver.LendLeaseGiving.Exists(g => g.Receiver == receiver.Tag);

	/// <summary>Begins it: the receiver at weight 500 and effectiveness nought, and the giver among the receiver's.</summary>
	public static void Add(CCountry giver, CCountry receiver)
	{
		if (Gives(giver, receiver))
		{
			return;
		}
		giver.LendLeaseGiving.Add(new CLendLeaseGift(receiver.Tag));
		if (!receiver.LendLeaseReceived.Exists(r => r.Giver == giver.Tag))
		{
			receiver.LendLeaseReceived.Add(new CLendLeaseReceipt(giver.Tag));
		}
	}

	/// <summary>Ends it, and scraps the receiver's lend-lease convoy from the giver, its ships back in the receiver's pool.</summary>
	public static void Remove(CCountry giver, CCountry receiver)
	{
		giver.LendLeaseGiving.RemoveAll(g => g.Receiver == receiver.Tag);
		// What the giver had sent and was not yet IC comes off the receiver's income, never below nought.
		foreach (CLendLeaseReceipt receipt in receiver.LendLeaseReceived.Where(r => r.Giver == giver.Tag))
		{
			receiver.LendLeaseIncome = System.Math.Max(0, receiver.LendLeaseIncome - receipt.Amount);
		}
		receiver.LendLeaseReceived.RemoveAll(r => r.Giver == giver.Tag);
		foreach (CConvoy convoy in receiver.Convoys.Where(c => c.IsLendLease && c.LendLeaseFrom == giver.Tag).ToArray())
		{
			CConvoyMaker.Remove(receiver, convoy);
		}
	}

	/// <summary>
	/// The hour's look, for each giver of each receiver: ended - the receiver told the giver "revoked" it -
	/// where it may no longer be; else its effectiveness, 1000 over land, the receiver's convoy's efficiency
	/// by sea, the convoy made where it has none, and nought where no convoy can be made.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		CSupplyNetwork? network = null;
		foreach (CCountry receiver in state.Countries)
		{
			foreach (CLendLeaseReceipt receipt in receiver.LendLeaseReceived.ToArray())
			{
				if (state.Country(receipt.Giver) is not { } giver
					|| giver.LendLeaseGiving.Find(g => g.Receiver == receiver.Tag) is not { } gift)
				{
					continue;
				}
				if (Refusal(state, giver, receiver) != ERefusal.None)
				{
					state.Outbox.Add(new Commands.CRemoveLendLeaseCommand(giver.Tag, receiver.Tag));
					continue;
				}
				if (Way(state, giver, receiver) != ELendLeaseRoute.Convoy)
				{
					gift.Effectiveness = CFixedPoint.Scale;
					continue;
				}
				network ??= CSupplyNetworks.Refresh(state);
				CConvoy? convoy = receiver.Convoys.Find(c => c.IsLendLease && c.LendLeaseFrom == giver.Tag);
				if (convoy is null && ConvoyEnds(state, network, giver, receiver) is (int start, int end))
				{
					convoy = CConvoyMaker.CreateLendLease(state, receiver, giver.Tag, start, end);
				}
				gift.Effectiveness = convoy is null ? 0 : CConvoyRules.Efficiency(state, convoy);
			}
		}
	}

	/// <summary>
	/// A lend-lease convoy's two harbours: the receiver's capital area's first, and the giver's harbour in its
	/// capital's area nearest it. None where either is wanting.
	/// </summary>
	public static (int Start, int End)? ConvoyEnds(CGameState state, CSupplyNetwork network, CCountry giver, CCountry receiver)
	{
		if (network.AreaOf(receiver.Capital) is not { } home || home.Ports.Count == 0
			|| network.AreaOf(giver.Capital) is not { } theirs)
		{
			return null;
		}
		int end = home.Ports[0];
		int start = CConvoyRules.NearestPort(state, theirs, end);
		if (start == 0 || !CConvoyRules.IsHarbour(state, state.Province(start)) || !CConvoyRules.IsHarbour(state, state.Province(end)))
		{
			return null;
		}
		return (start, end);
	}
}
