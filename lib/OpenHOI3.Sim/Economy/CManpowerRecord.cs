using System.Collections.Generic;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// What an enhanced game keeps of a country's manpower for the top bar's tooltip to say (after BiceLib's
/// <c>activateManpowerBreakdown</c>; <c>docs/mechanics/topbar.md</c>, section 14): the need for reinforcement by land,
/// air and sea as the day's pass counted it, what troop rotation cost on the last whole day, and thirty days of what
/// attrition cost and of what came back from casualties. **Nothing in the game reads it**: it is in an enhanced save,
/// so that a loaded game says what the saved one said, and not in the state's hash.
/// </summary>
public sealed class CManpowerRecord
{
	/// <summary>How many days of attrition and of trickleback are kept, and averaged.</summary>
	public const int Days = 30;

	/// <summary>
	/// The three tallies and their days are kept this many times finer than thousandths of manpower: a day's rotation
	/// takes two thousandths of a brigade's strength, whose price cut to a thousandth of manpower would be nothing.
	/// </summary>
	public const long Fine = 1000;

	private const long DaysInAYear = 365;
	private const long MonthsInAYear = 12;

	/// <summary>The part of the need for reinforcement that is brigades', in thousandths; with the two below, all of it.</summary>
	public long NeedLand { get; set; }

	public long NeedAir { get; set; }

	public long NeedNavy { get; set; }

	/// <summary>What rotation, attrition and trickleback have come to since the day was last closed, in millionths of manpower (<see cref="Fine"/>).</summary>
	public long RotationToday { get; set; }

	public long AttritionToday { get; set; }

	public long TricklebackToday { get; set; }

	/// <summary>What rotation cost on the last whole day; none before a day has been closed.</summary>
	public long? RotationLastDay { get; set; }

	/// <summary>What attrition cost on each of the last days, the oldest first.</summary>
	public List<long> AttritionDays { get; } = new();

	/// <summary>What came back from casualties on each of the last days, the oldest first.</summary>
	public List<long> TricklebackDays { get; } = new();

	/// <summary>The need is counted afresh by each reinforcement pass.</summary>
	public void StartNeed()
	{
		NeedLand = 0;
		NeedAir = 0;
		NeedNavy = 0;
	}

	public void AddNeed(EUnitBranch branch, long needed)
	{
		switch (branch)
		{
			case EUnitBranch.Air:
				NeedAir += needed;
				break;
			case EUnitBranch.Naval:
				NeedNavy += needed;
				break;
			default:
				NeedLand += needed;
				break;
		}
	}

	/// <summary>A country's day ends where its reinforcement begins: what has gathered becomes the last day's.</summary>
	public void CloseDay()
	{
		RotationLastDay = RotationToday;
		RotationToday = 0;
		Push(AttritionDays, AttritionToday);
		AttritionToday = 0;
		Push(TricklebackDays, TricklebackToday);
		TricklebackToday = 0;
	}

	private static void Push(List<long> days, long day)
	{
		days.Add(day);
		if (days.Count > Days)
		{
			days.RemoveAt(0);
		}
	}

	/// <summary>
	/// The last day's rotation over a month, a twelfth of a year of 365 days, in thousandths of manpower: a rate, so
	/// one day says it.
	/// </summary>
	public long? RotationAMonth => RotationLastDay is { } day ? day * DaysInAYear / MonthsInAYear / Fine : null;

	/// <summary>The average day's attrition over the days kept, over a month, in thousandths of manpower; none before a day has been closed.</summary>
	public long? AttritionAMonth => AMonth(AttritionDays);

	public long? TricklebackAMonth => AMonth(TricklebackDays);

	private static long? AMonth(List<long> days)
	{
		if (days.Count == 0)
		{
			return null;
		}
		long sum = 0;
		foreach (long day in days)
		{
			sum += day;
		}
		return sum * DaysInAYear / MonthsInAYear / days.Count / Fine;
	}

	/// <summary>
	/// What strength taken off a brigade costs in manpower to put back: the strength times the brigade's own
	/// <c>build_cost_manpower</c> over its full strength, as reinforcement prices it, in millionths of manpower
	/// (<see cref="Fine"/>); nothing where a figure is nought.
	/// </summary>
	public static long Price(CSubUnit subUnit, long strength)
	{
		long manpower = subUnit.Stats[CSubUnitStats.BuildCostManpower].Raw;
		long full = subUnit.Stats[CSubUnitType.MaxStrength].Raw;
		return strength <= 0 || manpower <= 0 || full <= 0 ? 0 : strength * manpower * Fine / full;
	}

	/// <summary>Whether there is anything to keep.</summary>
	public bool IsEmpty => NeedLand == 0 && NeedAir == 0 && NeedNavy == 0 && RotationToday == 0 && AttritionToday == 0
		&& TricklebackToday == 0 && RotationLastDay is null && AttritionDays.Count == 0 && TricklebackDays.Count == 0;
}
