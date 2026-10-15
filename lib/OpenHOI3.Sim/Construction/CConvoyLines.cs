using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// The build queue's convoy lines: what an item of transports or escorts costs a day and how long it takes, what a
/// finished one gives, and how many ships the queue still holds. See <c>docs/mechanics/convoylines.md</c>.
/// </summary>
public static class CConvoyLines
{
	public const string Section = "economy";
	public const string ConvoyCostDefine = "CONVOY_BUILD_COST";
	public const string ConvoyTimeDefine = "CONVOY_BUILD_TIME";
	public const string EscortCostDefine = "ESCORT_BUILD_COST";
	public const string EscortTimeDefine = "ESCORT_BUILD_TIME";
	public const string SizeDefine = "CONVOY_CONSTRUCTION_SIZE";

	/// <summary>The technology effects that change the four figures, a share more or less of each.</summary>
	public const string ConvoyCostEffect = "convoy_build_cost";
	public const string ConvoyTimeEffect = "convoy_build_time";
	public const string EscortCostEffect = "escort_build_cost";
	public const string EscortTimeEffect = "escort_build_time";

	/// <summary>No figure is ever taken below a hundredth of a unit.</summary>
	private const long Least = 10;

	/// <summary>An item's cost a day: the define, and the country's technology's share more or less of it.</summary>
	public static CFixedPoint Cost(CGameState state, CCountry country, bool escorts)
	{
		return Figure(state, country, escorts ? EscortCostDefine : ConvoyCostDefine, escorts ? EscortCostEffect : ConvoyCostEffect);
	}

	/// <summary>An item's time in thousandths of a day, before it is cut to whole days.</summary>
	public static CFixedPoint Time(CGameState state, CCountry country, bool escorts)
	{
		return Figure(state, country, escorts ? EscortTimeDefine : ConvoyTimeDefine, escorts ? EscortTimeEffect : ConvoyTimeEffect);
	}

	/// <summary>A line as the order makes it: priced now, and never again.</summary>
	public static CConstruction NewLine(CGameState state, CCountry country, bool escorts, int amount)
	{
		long time = Time(state, country, escorts).Raw;
		return new CConstruction(country.Tag)
		{
			IsConvoy = true,
			IsEscorts = escorts,
			Size = amount,
			Cost = Cost(state, country, escorts),
			Duration = new CFixedPoint(time / CFixedPoint.Scale * CFixedPoint.Scale),
		};
	}

	/// <summary>The ships one finished item gives: the define, cut to a whole number.</summary>
	public static int ShipsPerItem(CGameState state)
	{
		return (int)(state.Defines[Section, SizeDefine].Raw / CFixedPoint.Scale);
	}

	/// <summary>
	/// An item finished: its ships to the country's free escorts or transports, what was paid past its time kept for the
	/// next, and one fewer to come. Answers whether the line is done with. Nothing asks whether the country still holds
	/// land, and nothing else happens.
	/// </summary>
	public static bool Finish(CGameState state, CCountry country, CConstruction line)
	{
		int ships = ShipsPerItem(state);
		if (line.IsEscorts)
		{
			country.Escorts += ships;
		}
		else
		{
			country.Transports += ships;
		}
		line.Progress -= CFixedPoint.Min(line.Duration, line.Progress);
		line.Size--;
		return line.Size <= 0;
	}

	/// <summary>The ships of one kind the country's queue still has to give: each convoy line's size times an item's.</summary>
	public static int BeingBuilt(CGameState state, CCountry country, bool escorts)
	{
		int ships = ShipsPerItem(state);
		int total = 0;
		foreach (CConstruction line in country.Constructions)
		{
			if (line.IsConvoy && line.IsEscorts == escorts)
			{
				total += line.Size * ships;
			}
		}
		return total;
	}

	private static CFixedPoint Figure(CGameState state, CCountry country, string define, string effect)
	{
		long value = state.Defines[Section, define].Raw * (CFixedPoint.Scale + country.Technology[effect].Raw) / CFixedPoint.Scale;
		return new CFixedPoint(Math.Max(value, Least));
	}
}
