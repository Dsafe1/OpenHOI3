using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Aid;

/// <summary>
/// The day's repayment of debts, at the end of each country's production and before its trade: for each
/// country it owes, a figure a day worked out from the debt where none is held, paid where the debtor is at
/// peace and has more money than it, else set to nought for the day. **The debt is never lowered**, so a
/// debt is repaid for ever. Only a country owning provinces repays. See <c>docs/mechanics/aid.md</c>,
/// section 1.
/// </summary>
public static class CDebts
{
	public const string YearsDefine = "YEARS_TO_REPAY_DEBT";

	public static void RepayDaily(CGameState state)
	{
		CCountryFacts facts = CCountryFacts.Of(state);
		foreach (CCountry debtor in state.Countries)
		{
			if (facts.OwnedCount(debtor.Tag) == 0 || state.Stockpile(debtor) is not { } pool)
			{
				continue;
			}
			foreach (CCountry creditor in state.Countries)
			{
				if (creditor == debtor)
				{
					continue;
				}
				CDiplomacyStatus status = CGameState.Status(debtor, creditor);
				long repay = DailyRepay(state, status);
				if (repay <= 0)
				{
					continue;
				}
				if (state.IsAtWar(debtor.Tag) || pool[EGoods.Money].Raw <= repay)
				{
					status.DailyRepay = 0;
					continue;
				}
				var paid = new CFixedPoint(repay);
				pool[EGoods.Money] -= paid;
				if (state.Stockpile(creditor) is { } theirs)
				{
					theirs[EGoods.Money] += paid;
				}
				debtor.RepaidAway += paid;
				creditor.IncomeFromDebt += paid;
			}
		}
	}

	/// <summary>
	/// The figure a day: the status's own, or where that is nought or less and there is a debt,
	/// <c>debt * 1000 / (YEARS_TO_REPAY_DEBT * 365000 / 1000)</c>, truncated - -1 where that is no days.
	/// </summary>
	public static long DailyRepay(CGameState state, CDiplomacyStatus status)
	{
		if (status.Debt > 0 && status.DailyRepay <= 0)
		{
			long days = state.Defines[Agreements.COfferRules.Section, YearsDefine].Raw * 365000 / 1000;
			status.DailyRepay = days == 0 ? -1 : status.Debt * 1000 / days;
		}
		return status.DailyRepay;
	}
}
