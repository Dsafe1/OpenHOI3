using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The provinces a strategy means to take and to hold. See <c>docs/mechanics/aistrategy.md</c>, section 5.
/// </summary>
public static class CStrategyProvinces
{
	private const int ConquerWeight = 50;
	private const int CapitalWeight = 200;
	private const int ResourceWeight = 50;
	private const int AirBaseWeight = 20;

	/// <summary>
	/// Every land province, in the map's order, somebody owns and the country does not hold, where the country is military
	/// or at war with its owner, and its owner is the country or its area touches land the country holds, and it has a
	/// strategic resource: 50.
	/// </summary>
	public static void Conquer(CGameState state, CCountry country, CAiStrategy strategy)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CProvince province in state.Provinces)
		{
			if (province.IsSea || province.Owner is not { } owner || province.Controller == country.Tag)
			{
				continue;
			}
			if (!strategy.IsMilitarist && !CCountryLists.IsEnemy(state, country, owner))
			{
				continue;
			}
			if (owner != country.Tag && !Touches(network.AreaOf(province.Id), country.Tag))
			{
				continue;
			}
			if (province.StrategicResource.Length > 0)
			{
				AddConquer(state, country, strategy, province.Id, ConquerWeight);
			}
		}
	}

	/// <summary>
	/// The original's <c>CAIStrategy::AddConquerProv</c>, which everything that adds to the list goes through: nothing
	/// for a province the country owns itself - so its own land held by an enemy is never on it, whatever the rule that
	/// asked (aistrategy.md, section 5) - else the weight added and the list cut to its cap.
	/// </summary>
	public static void AddConquer(CGameState state, CCountry country, CAiStrategy strategy, int province, int weight)
	{
		if (province > 0 && province < state.Provinces.Count && state.Province(province).Owner == country.Tag)
		{
			return;
		}
		strategy.ConquerProvinces.Add(province, weight, ConquerCap(strategy));
	}

	/// <summary>
	/// The capital, 200, where the country holds it - the original's adder takes no province another holds, so a
	/// government in exile has no capital on its list; then each province the country holds, 50 for a strategic resource and
	/// 20 for an air base built.
	/// </summary>
	public static void Defend(CGameState state, CCountry country, CAiStrategy strategy)
	{
		if (country.Capital > 0 && country.Capital < state.Provinces.Count && state.Province(country.Capital).Controller == country.Tag)
		{
			strategy.DefendProvinces.Add(country.Capital, CapitalWeight, DefendCap);
		}
		// In the order of its list of the provinces it controls (aistrategy.md, section 5).
		foreach (CProvince province in CCountryLists.Controlled(state, country.Tag))
		{
			int weight = province.StrategicResource.Length > 0 ? ResourceWeight : 0;
			if (CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) > CFixedPoint.Zero)
			{
				weight += AirBaseWeight;
			}
			if (weight != 0)
			{
				strategy.DefendProvinces.Add(province.Id, weight, DefendCap);
			}
		}
	}

	/// <summary>How many provinces it means to take: 40, or 60 for a military personality.</summary>
	public static int ConquerCap(CAiStrategy strategy) => strategy.IsMilitarist ? 60 : 40;

	/// <summary>How many it means to hold.</summary>
	public const int DefendCap = 1000;

	private static bool Touches(CSupplyArea? area, CCountryTag tag)
	{
		if (area is null)
		{
			return false;
		}
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (beside.Controller == tag)
			{
				return true;
			}
		}
		return false;
	}
}
