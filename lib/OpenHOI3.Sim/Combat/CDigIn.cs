using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// Digging in. A land unit of a country at war that stays where it is and is not
/// fighting gains a level a day, up to a cap; a unit that sets off, or whose country is
/// at peace, has none. A defender fights the better for each level. See
/// <c>docs/mechanics/landcombat.md</c>, section 4.
/// </summary>
public static class CDigIn
{
	public const string Cap = "UNIT_DIGIN_CAP";

	/// <summary>The day's digging, for every land unit of every country at war.</summary>
	public static void RunDaily(CGameState state)
	{
		CFixedPoint cap = state.Defines[CManpower.MilitarySection, Cap];
		foreach (CCountry country in state.Countries)
		{
			bool atWar = state.IsAtWar(country.Tag);
			foreach (CUnit unit in country.AllUnits())
			{
				if (!atWar)
				{
					unit.DigIn = CFixedPoint.Zero;
				}
				else if (CMovementRules.IsLandUnit(unit) && !unit.IsMoving && !unit.IsFighting && unit.DigIn < cap)
				{
					unit.DigIn += CFixedPoint.One;
				}
			}
		}
	}

	/// <summary>A unit's hour: one with somewhere to go, or of a country at peace, is dug in no longer.</summary>
	public static void RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.IsMoving || !state.IsAtWar(country.Tag))
		{
			unit.DigIn = CFixedPoint.Zero;
		}
	}
}
