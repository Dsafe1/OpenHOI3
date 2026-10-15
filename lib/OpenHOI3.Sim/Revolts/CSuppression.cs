using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// How much suppression stands in and around a province: what holds its revolt risk down. See
/// <c>docs/mechanics/revolt.md</c>, section 2.
/// </summary>
public static class CSuppression
{
	public const string Kind = "suppression";

	/// <summary>
	/// What the units standing in one province deliver, in thousandths: for each unit whose owner is a friend of the
	/// province's holder, its brigades' strength over their most, times the sum of its brigades' suppression,
	/// truncated; and the whole a tenth of that, truncated.
	/// </summary>
	public static long In(CGameState state, CProvince province)
	{
		if (province.Controller is not { } holder)
		{
			return 0;
		}
		long sum = 0;
		foreach (CUnit unit in state.UnitsIn(province.Id))
		{
			if (state.Country(unit.Owner) is not { } owner || !CCountryLists.IsFriend(state, owner, holder, withAccess: true))
			{
				continue;
			}
			long strength = 0;
			long most = 0;
			long suppression = 0;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				strength += subUnit.Strength.Raw;
				most += CUnits.MaxStrength(state, owner, subUnit).Raw;
				suppression += subUnit.Stats[Kind].Raw;
			}
			long ratio = most == 0 ? -1 : strength * CFixedPoint.Scale / most;
			sum += ratio * suppression / CFixedPoint.Scale;
		}
		return sum * CFixedPoint.Scale / 10000;
	}

	/// <summary>
	/// The province's whole figure, the day's: its own units' and, over every province within two edges - which is
	/// itself among them - each land province's under the same holder. So its own units count twice.
	/// </summary>
	/// <param name="known">
	/// Each province's own figure as already worked out, by id, -1 where not yet: what the day's pass keeps so as not to
	/// count a province's units again for every province near it. Nothing moves a unit during the pass, and the pass
	/// forgets a province's figure where its holder changes.
	/// </param>
	public static long Near(CGameState state, CProvince province, long[]? known = null)
	{
		long total = Known(state, province, known);
		foreach (int id in CNearProvinces.Of(state, province))
		{
			CProvince near = state.Province(id);
			if (near.Id > 0 && !near.IsSea && near.Controller == province.Controller)
			{
				total += Known(state, near, known);
			}
		}
		return total;
	}

	private static long Known(CGameState state, CProvince province, long[]? known)
	{
		if (known is null)
		{
			return In(state, province);
		}
		if (known[province.Id] < 0)
		{
			known[province.Id] = In(state, province);
		}
		return known[province.Id];
	}
}
