using System;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface;

/// <summary>
/// What a slider of the production or technology screen writes into its window: its
/// title, the amount it holds and what it needs. See
/// <c>docs/mechanics/sliders.md</c>, section 2.
/// </summary>
public static class CSliderText
{
	public const string NeedKey = "SLIDER_NEED";

	/// <summary>The key of each IC slider's title.</summary>
	public static string TitleKey(EProductionCategory category)
	{
		return category switch
		{
			EProductionCategory.LendLease => "DISTRIBUTE_LENDLEASE",
			EProductionCategory.ConsumerGoods => "DISTRIBUTE_CONSUMER_GOODS",
			EProductionCategory.Production => "DISTRIBUTE_PRODUCTION",
			EProductionCategory.Supplies => "DISTRIBUTE_SUPPLY",
			EProductionCategory.Reinforcement => "DISTRIBUTE_REINFORCE",
			_ => "DISTRIBUTE_UPGRADE",
		};
	}

	/// <summary>The key of each leadership slider's title.</summary>
	public static string TitleKey(ELeadershipCategory category)
	{
		return category switch
		{
			ELeadershipCategory.Officers => "DISTRIBUTE_NCO",
			ELeadershipCategory.Diplomacy => "DISTRIBUTE_DIPLOMACY",
			ELeadershipCategory.Espionage => "DISTRIBUTE_ESPIONAGE",
			_ => "DISTRIBUTE_RESEARCH",
		};
	}

	/// <summary>
	/// The amount a slider holds, to two decimals, red when it is less than the need and
	/// white when not - compared in hundredths, as they are written.
	/// </summary>
	public static string Value(CFixedPoint amount, CFixedPoint need)
	{
		char colour = need.Raw / 10 > Hundredths(amount) ? CColouredText.Red : 'W';
		return CColouredText.In(colour) + Value(amount);
	}

	/// <summary>The amount a slider holds where nothing is needed of it: no colour of its own.</summary>
	public static string Value(CFixedPoint amount)
	{
		return CNumberText.Format(new CFixedPoint(Hundredths(amount) * 10), 2);
	}

	/// <summary>
	/// An amount to the nearest hundredth. A share is cut off when it is stored, so the
	/// amount it gives is a hair under the amount it was set to: 6.898 where 6.90 was
	/// meant. The original prints the amount from a float and adds a term nobody has
	/// read before it compares; rounding is this project's reading of both.
	/// </summary>
	private static long Hundredths(CFixedPoint amount)
	{
		return (amount.Raw + 5) / 10;
	}

	/// <summary>The need line: the words of <c>SLIDER_NEED</c> and the need to two decimals.</summary>
	public static string Need(Func<string, string> localise, CFixedPoint need)
	{
		return localise(NeedKey) + CNumberText.Format(need, 2);
	}
}
