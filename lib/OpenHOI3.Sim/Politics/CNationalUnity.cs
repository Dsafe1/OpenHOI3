using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// National unity's day: a thirtieth of a monthly figure - the strategic-warfare impacts and the
/// <c>national_unity</c> modifier, floored at -1.0 - slowed by the square root of the distance from 50 when it
/// moves away from it, and changed through <see cref="Change"/>. See <c>docs/mechanics/politics.md</c>, section 6.
/// </summary>
public static class CNationalUnity
{
	public const string Modifier = "national_unity";
	public const string Effect = "national_unity_effect";

	public const long Least = 1000;
	public const long Most = 100000;

	/// <summary>Every country, every day, right after its modifier is made again.</summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			CStrategicWarfareImpacts.Prune(state, country.StrategicWarfare);
			RunDay(country, country.StrategicWarfare);
		}
	}

	private static void RunDay(CCountry country, CStrategicWarfare warfare)
	{
		long impacts = warfare.ConvoyImpact + warfare.AlliesImpact + warfare.BombingImpact;
		long month = Fp15(impacts) + Math.Max(Fp15(country.Modifier[Modifier].Raw), -32768);
		long day = month / 30;
		long u = country.NationalUnity.Raw;
		if (u < 49000 && day < 0)
		{
			day /= IntegerSqrt(50 - u / 1000);
		}
		if (u > 51000 && day > 0)
		{
			day /= IntegerSqrt(u / 1000 - 50);
		}
		// An arithmetic shift, so a negative change floors.
		long delta = (day * 1000) >> 15;
		Change(country, delta);
	}

	/// <summary>Changes national unity by an amount scaled by <c>national_unity_effect</c>, held to 1..100.</summary>
	public static void Change(CCountry country, long delta)
	{
		long scaled = delta * (1000 + country.Modifier[Effect].Raw) / 1000;
		country.NationalUnity = new CFixedPoint(Math.Clamp(country.NationalUnity.Raw + scaled, Least, Most));
	}

	/// <summary>Thousandths to the original's fifteen binary places, truncated.</summary>
	private static long Fp15(long thousandths)
	{
		return thousandths * 32768 / 1000;
	}

	/// <summary>The square root, floored; at least 1, so a division by it is safe.</summary>
	private static long IntegerSqrt(long value)
	{
		return value <= 1 ? 1 : (long)Math.Sqrt(value);
	}
}
