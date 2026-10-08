using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the windows a unit is ordered from write, as the original writes it: the order
/// panel all four share, and the few ways of writing a number that are theirs. See
/// <c>docs/mechanics/builders.md</c>, sections 3 and 6.
/// </summary>
public static class CBuilderText
{
	/// <summary>The fewest and the most a count of the order panel may be.</summary>
	public const int FewestOrdered = 1;
	public const int MostOrdered = 99;

	/// <summary>What a held button adds each time, and what it adds with the modifier key down.</summary>
	public const int Step = 1;
	public const int LongStep = 10;

	/// <summary>How long a held button waits before it repeats, and between repeats after.</summary>
	public const double FirstRepeatSeconds = 0.3;
	public const double RepeatSeconds = 0.1;

	/// <summary>A build time longer than this many days is written in months.</summary>
	private const long MostDaysShown = 365;

	private const long DaysPerMonth = 30;

	/// <summary>A slow speed with more of a fraction than this is written to two decimals.</summary>
	private const long SpeedFraction = 90;
	private const long SlowSpeed = 2 * CFixedPoint.Scale;

	private static readonly string[] Suffixes = { "", "k", "M", "G" };

	/// <summary>
	/// A count after a step: one more or less, or ten, never under one. The buttons
	/// themselves stop it at the top.
	/// </summary>
	public static int Stepped(int count, int direction, bool longStep)
	{
		return Math.Max(FewestOrdered, count + direction * (longStep ? LongStep : Step));
	}

	public static bool MayStepDown(int count)
	{
		return count > FewestOrdered;
	}

	public static bool MayStepUp(int count)
	{
		return count < MostOrdered;
	}

	/// <summary>What the order costs a day: one unit's IC for each line it makes, to two decimals.</summary>
	public static string IcCost(CFixedPoint daily, int parallel)
	{
		return CNumberText.Format(Total(daily, parallel), 2);
	}

	/// <summary>The men the order takes: one unit's for every unit of every line, to two decimals.</summary>
	public static string ManpowerCost(CFixedPoint one, int serial, int parallel)
	{
		return CNumberText.Format(Total(one, serial * parallel), 2);
	}

	/// <summary>
	/// How long a line of the order takes: one unit's days for each unit of it, in days
	/// up to a year and in months of thirty days beyond.
	/// </summary>
	public static string BuildTime(Func<string, string> localise, CFixedPoint duration, int serial)
	{
		long days = Total(duration, serial).Raw / CFixedPoint.Scale;
		return days > MostDaysShown
			? (days / DaysPerMonth).ToString(CultureInfo.InvariantCulture) + " " + localise("MONTHS")
			: days.ToString(CultureInfo.InvariantCulture) + " " + localise("DAYS");
	}

	/// <summary>
	/// The light: by what the production share has left when the queue as it stands is
	/// paid, against what the order would add.
	/// </summary>
	/// <param name="cost">What the order costs a day, every line of it.</param>
	public static EBuilderLight Light(CCountry country, CFixedPoint cost)
	{
		long spare = CProduction.Given(country, EProductionCategory.Production).Raw - CBuildQueue.QueueCost(country).Raw;
		return spare - cost.Raw >= 0 ? EBuilderLight.Green
			: spare > 0 ? EBuilderLight.Orange
			: EBuilderLight.Red;
	}

	/// <summary>A figure times a count, the way the original multiplies them.</summary>
	public static CFixedPoint Total(CFixedPoint one, int count)
	{
		return new CFixedPoint(one.Raw * ((long)count * CFixedPoint.Scale) / CFixedPoint.Scale);
	}

	/// <summary>The practical one of a type gives when it is built, with a plus before it.</summary>
	public static string OnCompletion(CSubUnitType type)
	{
		return "+" + CNumberText.Trimmed(type.Stats[CBuildQueue.CompletionSize]);
	}

	/// <summary>The men one brigade, ship or wing costs a country.</summary>
	public static CFixedPoint ManpowerOf(CCountry country, CSubUnit subUnit, bool isReserve)
	{
		return CBuildQueue.ManpowerOf(country, subUnit, isReserve);
	}

	/// <summary>
	/// A figure that is written whole, and from a thousand up in thousands, millions or
	/// thousands of millions with a letter after it - cut, not rounded. 3000 is <c>3k</c>.
	/// </summary>
	public static string WithSuffix(CFixedPoint value)
	{
		long whole = value.Raw / CFixedPoint.Scale;
		long magnitude = Math.Abs(whole);
		if (magnitude < 1000)
		{
			return CNumberText.Format(value, 0);
		}
		int suffix = 0;
		long unit = 1;
		for (long left = magnitude; left > 999 && suffix < Suffixes.Length - 1; left /= 1000)
		{
			unit *= 1000;
			suffix++;
		}
		return (whole < 0 ? "-" : "") + (magnitude / unit).ToString(CultureInfo.InvariantCulture) + Suffixes[suffix];
	}

	/// <summary>
	/// A strength as men: a hundred to the point, plain under a thousand, with a comma
	/// under a hundred thousand, in thousands with a K under two million, and in
	/// millions to two decimals with an M beyond.
	/// </summary>
	public static string Men(CFixedPoint strength)
	{
		long men = strength.Raw * 100 / CFixedPoint.Scale;
		if (men < 1000)
		{
			return men.ToString(CultureInfo.InvariantCulture);
		}
		if (men < 100000)
		{
			return (men / 1000).ToString(CultureInfo.InvariantCulture) + ","
				+ (men % 1000).ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');
		}
		if (men < 2000000)
		{
			return (men / 1000).ToString(CultureInfo.InvariantCulture) + "K";
		}
		// Millions of men, as thousandths of a million.
		return CNumberText.Format(new CFixedPoint(strength.Raw / 10000), 2) + "M";
	}

	/// <summary>A speed in a list: whole, unless it is under two and has more than a small fraction, when it has two decimals.</summary>
	public static string ListSpeed(CFixedPoint speed)
	{
		long fraction = Math.Abs(speed.Raw - RoundToWhole(speed.Raw) * CFixedPoint.Scale);
		return CNumberText.Format(speed, speed.Raw < SlowSpeed && fraction > SpeedFraction ? 2 : 0);
	}

	/// <summary>A division's speed: whole when it is a whole number, and to two decimals when it is not.</summary>
	public static string ExactSpeed(CFixedPoint speed)
	{
		long fraction = Math.Abs(speed.Raw - RoundToWhole(speed.Raw) * CFixedPoint.Scale);
		return CNumberText.Format(speed, fraction < 1 ? 0 : 2);
	}

	/// <summary>The nearest whole number to a figure in thousandths, a half going to the even one.</summary>
	private static long RoundToWhole(long thousandths)
	{
		return (long)Math.Round(thousandths / (double)CFixedPoint.Scale, MidpointRounding.ToEven);
	}
}
