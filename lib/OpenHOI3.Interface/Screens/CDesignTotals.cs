using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The figures of a division being designed, out of its brigades as they would be built:
/// most add up, organisation and softness are averaged, the speed is the slowest
/// brigade's, and piercing and armour the best. See <c>docs/mechanics/builders.md</c>,
/// section 4.
/// </summary>
public sealed class CDesignTotals
{
	private static readonly (string Element, string Statistic, int Decimals)[] Sums =
	{
		("cw_value", "combat_width", 0), ("sa_value", "soft_attack", 0), ("ha_value", "hard_attack", 0),
		("aa_value", "air_attack", 0), ("def_value", "defensiveness", 0), ("tou_value", "toughness", 0),
		("ad_value", "air_defence", 0), ("sup_value", "suppression", 0), ("sc_value", "supply_consumption", 2),
		("fc_value", "fuel_consumption", 2),
	};

	private readonly Dictionary<string, long> _sums = new(StringComparer.OrdinalIgnoreCase);

	public CDesignTotals(IReadOnlyList<CSubUnit> brigades)
	{
		Count = brigades.Count;
		long strength = 0;
		long organisation = 0;
		long softness = 0;
		long speed = -CFixedPoint.Scale;
		long piercing = 0;
		long armour = 0;
		foreach (CSubUnit brigade in brigades)
		{
			strength += brigade.Stats["max_strength"].Raw;
			organisation += brigade.Stats["default_organisation"].Raw;
			softness += brigade.Stats["softness"].Raw;
			foreach ((string _, string statistic, int _) in Sums)
			{
				_sums[statistic] = _sums.GetValueOrDefault(statistic) + brigade.Stats[statistic].Raw;
			}

			long own = brigade.Stats["maximum_speed"].Raw;
			if (speed < 0 || own < speed)
			{
				speed = own;
			}
			piercing = Math.Max(piercing, brigade.Stats["ap_attack"].Raw);
			armour = Math.Max(armour, brigade.Stats["armor_value"].Raw);
		}

		Strength = new CFixedPoint(strength);
		// An average is the sum over the count, cut.
		Organisation = new CFixedPoint(Count == 0 ? organisation : organisation / Count);
		Softness = new CFixedPoint(Count == 0 ? softness : softness / Count);
		Speed = new CFixedPoint(Math.Max(0, speed));
		Piercing = new CFixedPoint(piercing);
		Armour = new CFixedPoint(armour);
	}

	/// <summary>How many brigades.</summary>
	public int Count { get; }

	public CFixedPoint Strength { get; }

	public CFixedPoint Organisation { get; }

	public CFixedPoint Softness { get; }

	/// <summary>The slowest brigade's; nothing for a design of none.</summary>
	public CFixedPoint Speed { get; }

	public CFixedPoint Piercing { get; }

	public CFixedPoint Armour { get; }

	/// <summary>What all the brigades have of a statistic that is added up.</summary>
	public CFixedPoint Sum(string statistic)
	{
		return new CFixedPoint(_sums.GetValueOrDefault(statistic));
	}

	/// <summary>
	/// Every figure the designer shows of the design, by the element it is written in.
	/// </summary>
	public IEnumerable<(string Element, string Text)> Figures()
	{
		yield return ("size_value", Count.ToString(CultureInfo.InvariantCulture));
		yield return ("str_value", CBuilderText.Men(Strength));
		yield return ("org_value", CNumberText.Format(Organisation, 0));
		foreach ((string element, string statistic, int decimals) in Sums)
		{
			yield return (element, CNumberText.Format(Sum(statistic), decimals));
		}
		yield return ("sof_value", CNumberText.Format(Softness * 100, 0) + "%");
		yield return ("speed_value", CBuilderText.ExactSpeed(Speed));
		yield return ("piercing_value", CNumberText.Format(Piercing, 0));
		yield return ("armored_value", CNumberText.Format(Armour, 0));
	}

	/// <summary>The most brigades the country may put in a division, as the designer writes it.</summary>
	public static string Allowed(int most)
	{
		return most.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>The combined arms bonus as a whole percentage.</summary>
	public static string CombinedArms(CFixedPoint bonus)
	{
		return CNumberText.Format(bonus * 100, 0) + "%";
	}
}
