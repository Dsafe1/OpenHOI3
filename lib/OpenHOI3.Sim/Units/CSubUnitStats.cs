using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The figures of a brigade, a ship or a wing: its plain statistics by the key the files
/// give them, and what terrain, rivers, forts and the night do to it.
///
/// A unit type has one of these, a technology has one for every type it improves, and
/// a brigade has its own: its type's, with each of its technologies added as many times
/// as the brigade's level of it. See <c>docs/mechanics/units.md</c>, section 2.
/// </summary>
public sealed class CSubUnitStats
{
	public const string BuildCostIc = "build_cost_ic";
	public const string BuildCostManpower = "build_cost_manpower";
	public const string BuildTime = "build_time";
	public const string Toughness = "toughness";
	public const string RepairCostMultiplier = "repair_cost_multiplier";

	/// <summary>The least a brigade can cost in IC or in manpower, whatever technology takes off.</summary>
	private static readonly CFixedPoint LeastCost = new(100);

	/// <summary>The plain statistics. One nothing gives reads as zero.</summary>
	public CModifier Values { get; } = new();

	/// <summary>
	/// What each named circumstance does - a terrain, <c>river</c>, <c>fort</c>,
	/// <c>night</c>, <c>amphibious</c> - as its attack, defence, movement and attrition.
	/// </summary>
	public Dictionary<string, CModifier> Adjusters { get; } = new(StringComparer.OrdinalIgnoreCase);

	public CFixedPoint this[string statistic] => Values[statistic];

	/// <summary>A statistic by its key, for what asks it every hour (<see cref="CModifierKey"/>).</summary>
	public CFixedPoint this[CModifierKey statistic] => Values[statistic];

	/// <summary>Makes this a copy of another.</summary>
	public void CopyFrom(CSubUnitStats other)
	{
		Values.Clear();
		Values.Add(other.Values);
		Adjusters.Clear();
		foreach ((string name, CModifier adjuster) in other.Adjusters)
		{
			var copy = new CModifier(name);
			copy.Add(adjuster);
			Adjusters[name] = copy;
		}
	}

	/// <summary>
	/// Adds another's figures to these, <paramref name="times"/> over. Two things about
	/// it are the original's and look like slips: toughness is added twice, and the
	/// repair cost multiplier is not added at all. And afterwards the two costs are not
	/// left under a tenth, nor the time to build under a day.
	/// </summary>
	public void Add(CSubUnitStats other, int times)
	{
		foreach ((string statistic, CFixedPoint value) in other.Values.Values)
		{
			if (statistic.Equals(RepairCostMultiplier, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			int count = statistic.Equals(Toughness, StringComparison.OrdinalIgnoreCase) ? 2 : 1;
			Values.Add(statistic, value * ((long)times * count));
		}
		foreach ((string name, CModifier adjuster) in other.Adjusters)
		{
			if (!Adjusters.TryGetValue(name, out CModifier? mine))
			{
				Adjusters[name] = mine = new CModifier(name);
			}
			foreach ((string kind, CFixedPoint value) in adjuster.Values)
			{
				mine.Add(kind, value * times);
			}
		}

		Values.Set(BuildCostIc, CFixedPoint.Max(Values[BuildCostIc], LeastCost));
		Values.Set(BuildCostManpower, CFixedPoint.Max(Values[BuildCostManpower], LeastCost));
		Values.Set(BuildTime, CFixedPoint.Max(Values[BuildTime], CFixedPoint.One));
	}
}
