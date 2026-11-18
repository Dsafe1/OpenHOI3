using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What standing down gives back: every brigade of every unit of the country that stands above the most it may now have
/// is cut to that most, and a share of the men it had over is its country's manpower again. See
/// <c>docs/mechanics/politics.md</c>, section 11.
/// </summary>
public static class CDemobilisation
{
	/// <summary>The share of an excess brigade's men that comes back, in the <c>country</c> defines.</summary>
	public const string Factor = "DEMOBILIZATION_FACTOR";

	/// <summary>
	/// For each brigade of each of the country's units, in their order: where its strength is above the most its owner's
	/// state now allows - the reserve's cut where it is not mobilised and not at war - the excess as a share of its
	/// <c>max_strength</c>, times its <c>build_cost_manpower</c> and <see cref="Factor"/>, each step truncated, is added to
	/// the country's manpower, never leaving it below nought; the brigade is put at that most, and its highest strength
	/// with it, lowered as well as raised.
	/// </summary>
	public static void GiveBack(CGameState state, CCountry country)
	{
		long factor = state.Defines["country", Factor].Raw;
		foreach (CUnit unit in country.AllUnits())
		{
			CCountry owner = state.Country(unit.FiguresOwner) ?? country;
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				CFixedPoint most = CUnits.MaxStrength(state, owner, brigade);
				long excess = brigade.Strength.Raw - most.Raw;
				if (excess <= 0)
				{
					continue;
				}
				long full = brigade.Stats.MaxStrength.Raw;
				long share = full == 0 ? 0 : excess * CFixedPoint.Scale / full;
				long men = share * brigade.Stats[CSubUnitStats.BuildCostManpower].Raw / CFixedPoint.Scale;
				men = men * factor / CFixedPoint.Scale;
				brigade.Strength = CFixedPoint.Max(most, CFixedPoint.Zero);
				brigade.Highest = brigade.Strength;
				country.Manpower = new CFixedPoint(Math.Max(0, country.Manpower.Raw + men));
			}
		}
	}
}
