using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The grants of intelligence on units: a new one for a country replaces the one it had, whatever the levels, and each
/// is counted down a day and goes at nought. Military espionage gives them (<c>docs/mechanics/espionage.md</c>, section
/// 7); what a country knows will read them.
/// </summary>
public static class CIntelGrants
{
	public static void Grant(CUnit unit, CCountryTag country, int days, int level)
	{
		foreach (CIntelGrant grant in unit.IntelGrants)
		{
			if (grant.Country == country)
			{
				grant.Days = days;
				grant.Level = level;
				return;
			}
		}
		unit.IntelGrants.Add(new CIntelGrant(country, days, level));
	}

	/// <summary>
	/// Every unit's day: each grant one day less, and gone at nought. Ours that it is daily: the original counts them in a
	/// unit's update the fact base takes to be daily.
	/// </summary>
	public static void CountDown(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IntelGrants.Count == 0)
				{
					continue;
				}
				foreach (CIntelGrant grant in unit.IntelGrants)
				{
					grant.Days--;
				}
				unit.IntelGrants.RemoveAll(grant => grant.Days <= 0);
			}
		}
	}
}
