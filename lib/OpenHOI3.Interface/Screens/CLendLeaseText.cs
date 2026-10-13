using System;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the lend-lease window works out: a receiver's slider - its weight in thousandths of a percent - the
/// spreading that keeps the sliders at a hundred, and the IC a slider is worth; and the cancel prompt's
/// text. See <c>docs/mechanics/aid.md</c>, section 6.
/// </summary>
public static class CLendLeaseText
{
	/// <summary>A hundred percent, in thousandths.</summary>
	public const long Whole = 100_000;

	/// <summary>What the spreading leaves alone: a gap of five thousandths of a percent or less.</summary>
	private const long Slack = 5;

	/// <summary>The step spread where the gap is under a hundredth of a percent.</summary>
	private const long LeastStep = 10;

	/// <summary>
	/// Keeps the sliders summing to a hundred: what is missing - or over - added in equal parts to every
	/// slider that is free (not held, not locked, not moved this frame), a hundredth where the gap is under
	/// one; nothing where none is free or the gap is five thousandths or less. Values in thousandths.
	/// </summary>
	public static void Spread(long[] values, bool[] free)
	{
		long sum = 0;
		int count = 0;
		for (int i = 0; i < values.Length; i++)
		{
			sum += values[i];
			if (free[i])
			{
				count++;
			}
		}
		long left = Whole - sum;
		if (count == 0 || Math.Abs(left) <= Slack)
		{
			return;
		}
		long step = Math.Abs(left) < LeastStep ? Math.Sign(left) * LeastStep : left;
		for (int i = 0; i < values.Length; i++)
		{
			if (free[i])
			{
				values[i] = Math.Clamp(values[i] + step / count, 0, Whole);
			}
		}
	}

	/// <summary>
	/// The lend-lease share's IC, in thousandths: its share of the total IC, never below nought.
	/// </summary>
	public static long ShareIc(CCountry giver)
	{
		long share = giver.ProductionShares[(int)EProductionCategory.LendLease].Raw;
		return Math.Max(0, (share * giver.TotalIc * CFixedPoint.Scale) >> CFixed15.Places);
	}

	/// <summary>A row's <c>actual_IC</c>: its slider's share of the lend-lease IC, to three places, cut off.</summary>
	public static string ActualIc(CCountry giver, long slider)
	{
		long share = slider / 100;
		return CNumberText.Format(new CFixedPoint(share * ShareIc(giver) / CFixedPoint.Scale), 3);
	}

	/// <summary>The cancel prompt: <c>CANCEL_LL_PROMPT</c> with the receiver's name.</summary>
	public static string CancelPrompt(Func<string, string> localise, CCountry receiver) =>
		CTextTemplate.Fill(localise("CANCEL_LL_PROMPT"), ("TARGET", receiver.Name));
}
