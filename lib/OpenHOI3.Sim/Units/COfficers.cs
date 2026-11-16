using System.Linq;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The officer ratio: how many of the officers a country's units call for it has. See
/// <c>docs/mechanics/organisation.md</c>, section 1.
/// </summary>
public static class COfficers
{
	/// <summary>The define the ratio is never above.</summary>
	public const string MostRatio = "MAX_OFFICERS";

	/// <summary>
	/// The officers a country's units call for: each brigade's, ship's and wing's own
	/// figure, whatever state it is in - those of a unit finished and not yet placed
	/// among them.
	/// </summary>
	public static CFixedPoint Needed(CCountry country, CGameState? state = null)
	{
		CFixedPoint needed = CFixedPoint.Zero;
		foreach (CUnit unit in Counted(country, state))
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				needed += subUnit.Stats[CSubUnitType.Officers];
			}
		}
		foreach (CUnit unit in country.Deployments)
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				needed += subUnit.Stats[CSubUnitType.Officers];
			}
		}
		return needed;
	}

	/// <summary>
	/// What it has over what is called for, and never above the define. A country whose
	/// units call for none has all it needs.
	/// </summary>
	public static CFixedPoint Ratio(CGameState state, CFixedPoint officers, CFixedPoint needed)
	{
		if (needed.Raw <= 0)
		{
			return CFixedPoint.One;
		}
		long ratio = officers.Raw * CFixedPoint.Scale / needed.Raw;
		long most = state.Defines[CManpower.MilitarySection, MostRatio].Raw;
		return new CFixedPoint(ratio > most ? most : ratio);
	}

	/// <summary>
	/// The brigades of its land units - those finished and not yet placed among them - but a reserve's while the country
	/// is not mobilised.
	/// </summary>
	public static int LandBrigadesInService(CCountry country, CGameState? state = null)
	{
		int count = 0;
		foreach (CUnit unit in Counted(country, state))
		{
			count += InService(country, unit);
		}
		foreach (CUnit unit in country.Deployments)
		{
			count += InService(country, unit);
		}
		return count;
	}

	/// <summary>Counts every country's need again and works out its ratio, and its land brigades in service with them.</summary>
	public static void Update(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.OfficersNeeded = Needed(country, state);
			country.LandBrigadesInService = LandBrigadesInService(country, state);
			country.OfficerRatio = Ratio(state, country.Officers, country.OfficersNeeded);
		}
	}

	/// <summary>
	/// The units a country's count takes, the original's <c>CCountry::RecountUnitTotals</c>: those in its own list that are not
	/// on loan to it, and those it has lent to another member of its own faction, which stand in that member's list - so a
	/// host counts nothing it holds on loan, and a unit lent outside the lender's faction is counted by neither. The United
	/// States, lending the Phillippine Force to the Philippines at a new game's third hour, needs 300 officers fewer until its
	/// three new armies' headquarters bring the need back; Japan, having lent 52 units to Manchukuo and Mengkukuo by July
	/// 1941, counts their 18,090 (reversing/findings/FINDINGS-liveread.md, sections 79 and 107).
	/// </summary>
	private static System.Collections.Generic.IEnumerable<CUnit> Counted(CCountry country, CGameState? state)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Lender is null)
			{
				yield return unit;
			}
		}
		if (state is null || country.Faction.Length == 0)
		{
			yield break;
		}
		foreach (CCountry member in state.Countries)
		{
			if (member == country || member.Faction != country.Faction)
			{
				continue;
			}
			foreach (CUnit unit in member.AllUnits())
			{
				if (unit.Lender == country.Tag)
				{
					yield return unit;
				}
			}
		}
	}

	private static int InService(CCountry country, CUnit unit)
	{
		if (!Theatres.CTheatres.IsLand(unit))
		{
			return 0;
		}
		int count = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (country.Mobilised || !subUnit.IsReserve)
			{
				count++;
			}
		}
		return count;
	}
}
