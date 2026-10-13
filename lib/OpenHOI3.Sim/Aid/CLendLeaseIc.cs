using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Aid;

/// <summary>
/// Lend-lease's IC: the giver's share of the day, first of its six, sent to its receivers in the order the
/// giving began, each paid only where the stockpile can pay for its whole amount; and the receiver turning
/// the whole IC of what it holds into IC that needs no resources, after its own cap, the fraction kept. See
/// <c>docs/mechanics/aid.md</c>, section 3.
/// </summary>
public static class CLendLeaseIc
{
	/// <summary>
	/// The giver's day: the share's IC in thousandths, <c>floor(share x TotalIc x 1000 / 32768)</c>, and for
	/// each receiver in turn its weight's part of it, times its effectiveness, both truncated - sent where the
	/// stockpile can still pay for it, nothing otherwise.
	/// </summary>
	public static void Give(CGameState state, CCountry giver, CGoodsPool stockpile, Action<CFixedPoint> charge)
	{
		if (giver.LendLeaseGiving.Count == 0)
		{
			return;
		}
		long total = giver.ProductionShares[(int)EProductionCategory.LendLease].Raw * giver.TotalIc;
		long thousandths = (total * CFixedPoint.Scale) >> CFixed15.Places;
		long weights = 0;
		foreach (CLendLeaseGift gift in giver.LendLeaseGiving)
		{
			weights += gift.Weight;
		}
		foreach (CLendLeaseGift gift in giver.LendLeaseGiving.ToArray())
		{
			long share = weights == 0 ? 0 : gift.Weight * CFixedPoint.Scale / weights;
			long amount = thousandths * share / CFixedPoint.Scale;
			amount = amount * gift.Effectiveness / CFixedPoint.Scale;
			if (amount > CProduction.SupportedIc(stockpile).Raw)
			{
				continue;
			}
			charge(new CFixedPoint(amount));
			if (state.Country(gift.Receiver) is { } receiver
				&& receiver.LendLeaseReceived.Find(r => r.Giver == giver.Tag) is { } receipt)
			{
				receipt.Amount += amount;
				receiver.LendLeaseIncome += amount;
			}
		}
	}

	/// <summary>
	/// The receiver's day, after its resource cap: the whole IC of its income added to its total and kept as
	/// its lend-lease IC, taken out of its givers' receipts in order, the fraction left with the latest.
	/// </summary>
	public static void Receive(CCountry receiver)
	{
		long whole = receiver.LendLeaseIncome / CFixedPoint.Scale;
		receiver.TotalIc += (int)whole;
		receiver.LendLeaseIc = (int)whole;
		if (receiver.LendLeaseReceived.Count == 0)
		{
			receiver.LendLeaseIncome = 0;
		}
		long left = whole * CFixedPoint.Scale;
		foreach (CLendLeaseReceipt receipt in receiver.LendLeaseReceived)
		{
			if (left <= 0)
			{
				break;
			}
			left -= receipt.Amount;
			if (left < 0)
			{
				receipt.Amount = -left;
				break;
			}
			receipt.Amount = 0;
		}
		receiver.LendLeaseIncome -= whole * CFixedPoint.Scale;
	}
}
