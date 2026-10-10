using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The two colours of a province in the supply map mode: its ground and its stripes.
/// By supplies alone, never by fuel. See <c>docs/mechanics/supplymap.md</c>, section 2:
///
///     green        it holds what was asked of it today
///     red          asked for more than it holds, with nothing there or coming through,
///                  and units that need more than lies there
///     blue-green   it holds supplies and nothing was asked of it
///     yellow       asked for more than it holds, with something there or coming through
///     grey         nothing to say, or the player does not know
///     black        too little infrastructure to carry anything
///     red stripes  asked for more than it can pass in a day
/// </summary>
public static class CSupplyMapColours
{
	/// <summary>How bright a province the player knows nothing of is drawn.</summary>
	private const float Unknown = 0.5f;

	private const float Grey = 0.4f;
	private const float Bright = 0.8f;
	private const float Middling = 0.6f;
	private const float Dull = 0.5f;
	private const float Striped = 0.7f;

	/// <summary>A province with less infrastructure than this, in thousandths of a whole, carries nothing: two levels.</summary>
	private const long LeastInfrastructure = 200;

	/// <summary>
	/// What the player knows of a province, on the original's scale: everything of
	/// ground it holds, and here nothing of any other - there is no intelligence yet.
	/// </summary>
	public static int Intelligence(CProvince province, CCountryTag? player)
	{
		return player is not null && province.Controller == player ? 9 : 0;
	}

	public static (CColor Ground, CColor Stripes) Of(CGameState state, CProvince province, CCountryTag? player)
	{
		int known = Intelligence(province, player);
		float light = known >= 2 ? 1f : Unknown;
		float grey = Grey * light;
		var plain = new CColor(grey, grey, grey);

		if (CSupplyFigures.MostInfrastructure(state, province) < LeastInfrastructure)
		{
			var black = new CColor(0f, 0f, 0f);
			return (black, black);
		}
		if (known < 6)
		{
			return (plain, plain);
		}
		var green = new CColor(grey, Bright * light, grey);
		if (known < 8)
		{
			return (green, green);
		}

		long lying = province.Pool[EGoods.Supplies].Raw;
		long layThere = province.LastPool[EGoods.Supplies].Raw;
		long passed = province.SupplyThroughput[EGoods.Supplies].Raw;
		long asked = province.SupplyDrawn[EGoods.Supplies].Raw;
		long need = province.SupplyNeed[EGoods.Supplies].Raw;

		CColor ground = plain;
		if (lying >= asked && asked > 0)
		{
			ground = green;
		}
		else if (lying < asked && asked > 0 && layThere <= 0 && passed == 0 && need > 0 && need > lying)
		{
			ground = new CColor(Middling * light, grey, grey);
		}
		else if (lying > 0 && asked == 0)
		{
			ground = new CColor(grey, Middling * light, Middling * light);
		}
		else if (asked > 0 && (lying > 0 || passed > 0 || layThere > 0))
		{
			// Brighter where its roads are worse than they were built.
			float yellow = CSupplyFigures.MostInfrastructure(state, province) > CSupplyFigures.EffectiveInfrastructure(province)
				? Bright
				: Dull;
			ground = new CColor(yellow * light, yellow * light, grey);
		}

		CColor stripes = asked > CSupplyFigures.Capacity(state, province).Raw
			? new CColor(Striped * light, grey, grey)
			: ground;
		return (ground, stripes);
	}
}
