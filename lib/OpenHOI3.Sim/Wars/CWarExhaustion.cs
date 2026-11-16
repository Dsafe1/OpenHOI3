using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// War exhaustion: what a country starts a new game with, by how long it has been at war or at peace, and its month - banked
/// by the <c>war_exhaustion</c> modifier while at war, held under <c>max_war_exhaustion</c>, and paid back whole to its
/// neutrality in the first month of peace. See <c>docs/mechanics/warexhaustion.md</c>; the readings are
/// <c>reversing/findings/FINDINGS-diplostate.md</c>, section C.3, and <c>reversing/findings/FINDINGS-leadership.md</c>,
/// section 10.
/// </summary>
public static class CWarExhaustion
{
	public const string Modifier = "war_exhaustion";
	public const string MaxModifier = "max_war_exhaustion";

	/// <summary>What a month of war adds to a new game's starting figure, and takes off it for a month of peace [exe].</summary>
	public static readonly CFixedPoint PerMonth = new(50);

	/// <summary>The most a new game starts a country at war with [exe].</summary>
	public static readonly CFixedPoint MostAtWar = CFixedPoint.FromInt(10);

	/// <summary>What a new game starts a country with on the day its last war ended, less a month's worth for each month since [exe].</summary>
	public static readonly CFixedPoint AfterPeace = CFixedPoint.FromInt(3);

	private const string Rebels = "REB";

	/// <summary>
	/// Every country's figure as a new game begins: at war, a twentieth for each month since the earliest of its wars began, at
	/// most 10; at peace, 3 less a twentieth for each month since the latest of its finished wars ended, never below nothing.
	/// Months are counted by the calendar: from October to January is three.
	/// </summary>
	public static void SetAtStart(CGameState state)
	{
		CDate now = state.Date;
		foreach (CCountry country in state.Countries)
		{
			country.WarExhaustion = AtStart(state, country, now);
		}
	}

	public static CFixedPoint AtStart(CGameState state, CCountry country, CDate now)
	{
		if (state.IsAtWar(country.Tag))
		{
			CDate began = now;
			foreach (CWar war in state.Wars)
			{
				if (war.Involves(country.Tag) && war.Started.CompareTo(began) < 0)
				{
					began = war.Started;
				}
			}
			long atWar = (Months(now) - Months(began)) * PerMonth.Raw;
			return new CFixedPoint(Math.Min(atWar, MostAtWar.Raw));
		}
		CDate ended = CDate.Zero;
		foreach (CWar war in state.FinishedWars)
		{
			if (war.Involves(country.Tag) && war.Ended.CompareTo(ended) > 0)
			{
				ended = war.Ended;
			}
		}
		long left = AfterPeace.Raw - (Months(now) - Months(ended)) * PerMonth.Raw;
		return new CFixedPoint(Math.Max(left, 0));
	}

	/// <summary>
	/// The month's pass, for every country, those that do not exist included: the rebels' figure back to nought; every other
	/// country's grown by its modifier, held between nothing and its maximum, and where it is at peace and has any, all of it
	/// added to its neutrality and the figure back to nought. It runs in the month before a new game's first hour too.
	/// Where a mod's Lua has turned on BiceLib's <c>disableWarExhaustionNeutralityReset</c>, every country's figure is put
	/// back to nought instead of grown, so nothing is ever added to a neutrality (<c>docs/mechanics/bicelib.md</c>, section 3).
	/// </summary>
	public static void RunMonthly(CGameState state)
	{
		bool zeroed = state.BiceLib.IsOn(Mods.CBiceLibSwitches.WarExhaustionNeutralityReset);
		foreach (CCountry country in state.Countries)
		{
			if (zeroed || country.Tag.Value == Rebels)
			{
				country.WarExhaustion = CFixedPoint.Zero;
				continue;
			}
			long banked = Math.Max(country.WarExhaustion.Raw + country.Modifier[Modifier].Raw, 0);
			banked = Math.Min(banked, country.Modifier[MaxModifier].Raw);
			country.WarExhaustion = new CFixedPoint(banked);
			if (!state.IsAtWar(country.Tag) && banked > 0)
			{
				CNeutrality.Change(state, country, country.WarExhaustion);
				country.WarExhaustion = CFixedPoint.Zero;
			}
		}
	}

	/// <summary>The war exhaustion effect: added, never below nothing, with no ceiling and nothing made again.</summary>
	public static void Add(CCountry country, CFixedPoint change)
	{
		country.WarExhaustion = new CFixedPoint(Math.Max(country.WarExhaustion.Raw + change.Raw, 0));
	}

	/// <summary>A date as a count of calendar months, which the original takes two of apart.</summary>
	public static long Months(CDate date) => date.Year * 12L + date.Month;
}
