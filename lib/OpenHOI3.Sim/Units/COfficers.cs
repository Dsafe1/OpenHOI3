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
	public static CFixedPoint Needed(CCountry country)
	{
		CFixedPoint needed = CFixedPoint.Zero;
		// What it holds on loan is its lender's to officer, and what it has lent still its own.
		foreach (CUnit unit in country.AllUnits().Where(unit => unit.Lender is null).Concat(country.LentUnits))
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
	public static int LandBrigadesInService(CCountry country)
	{
		int count = 0;
		foreach (CUnit unit in country.AllUnits())
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
			country.OfficersNeeded = Needed(country);
			country.LandBrigadesInService = LandBrigadesInService(country);
			country.OfficerRatio = Ratio(state, country.Officers, country.OfficersNeeded);
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
