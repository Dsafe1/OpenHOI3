using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// The manpower a country has to spare once every brigade is refilled, which the production script asks for as
/// <c>HasExtraManpowerLeft</c> (above nought). Worked out at the end of every country's day and after every unit order.
/// See <c>docs/mechanics/ministers.md</c>, section 7, and <c>reversing/findings/FINDINGS-ministers.md</c> C.5.
/// </summary>
public static class CSpareManpower
{
	private const string GlobalManpowerModifier = "global_manpower_modifier";
	private const string TechnologyManpower = "manpower_gain";

	/// <summary>A quarter of a brigade's build manpower, charged for every brigade on the map: the original's own figure.</summary>
	private const long Headroom = 250;

	/// <summary>Every country's, at the end of its day.</summary>
	public static void UpdateDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			Update(state, country);
		}
	}

	/// <summary>
	/// The pool, plus half a year of its manpower before its multipliers, less what every reinforcing brigade lacks and a
	/// quarter of its build manpower, less the men of every reserve line's brigades at the strength they are built at -
	/// each once, not times the line's size. Thousandths, every division truncating.
	/// </summary>
	public static void Update(CGameState state, CCountry country)
	{
		const long scale = CFixedPoint.Scale;
		long spare = country.Manpower.Raw;
		long years = state.Defines[CManpower.MilitarySection, CManpower.MaxManpower].Raw;
		long divisor = scale + country.Modifier[GlobalManpowerModifier].Raw + country.Technology[TechnologyManpower].Raw;
		if (divisor != 0 && years != 0)
		{
			spare += country.MaxManpower.Raw * scale / divisor * scale / years * scale / 2000;
		}
		foreach (CUnit unit in country.AllUnits())
		{
			if (!unit.MayReinforce)
			{
				continue;
			}
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				long most = brigade.Stats[CSubUnitType.MaxStrength].Raw;
				long shortOf = most == 0 ? -1 : (most - brigade.Strength.Raw) * scale / most;
				spare -= (shortOf + Headroom) * brigade.Type.Stats[CSubUnitStats.BuildCostManpower].Raw / scale;
			}
		}
		long reserves = scale + country.Modifier[CUnits.ReservesPenaltySize].Raw;
		foreach (CConstruction line in country.Constructions)
		{
			if (!line.IsUnit || !line.IsReserve)
			{
				continue;
			}
			foreach (CBrigadeOrder order in line.Brigades)
			{
				long most = order.Type.Stats[CSubUnitType.MaxStrength].Raw;
				if (most == 0)
				{
					continue;
				}
				spare -= reserves * most / scale * order.Type.Stats[CSubUnitStats.BuildCostManpower].Raw / scale * scale / most;
			}
		}
		country.SpareManpower = new CFixedPoint(spare);
	}
}
