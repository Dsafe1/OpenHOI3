using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Intelligence;
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
///     grey         nothing to say, or the player knows the province by under 6
///     flat green   the player knows it by 6 or 7, whatever lies there
///     black        too little infrastructure to carry anything, whatever the player knows
///     red stripes  asked for more than it can pass in a day
///
/// all at half brightness where the player knows the province by under 2.
/// </summary>
public static class CSupplyMapColours
{
	/// <summary>How bright a province the player knows by under <see cref="KnownBright"/> is drawn.</summary>
	private const float Unknown = 0.5f;

	private const float Grey = 0.4f;
	private const float Bright = 0.8f;
	private const float Middling = 0.6f;
	private const float Dull = 0.5f;
	private const float Striped = 0.7f;

	/// <summary>A province with less infrastructure than this, in thousandths of a whole, carries nothing: two levels.</summary>
	private const long LeastInfrastructure = 200;

	/// <summary>The least the player has to know a province by for it to be drawn at full brightness.</summary>
	public const int KnownBright = 2;

	/// <summary>The least the player has to know a province by for its colour to say anything: below it, grey.</summary>
	public const int KnownColoured = 6;

	/// <summary>The least for its colour to be its supplies': at <see cref="KnownColoured"/> and above, below this, a flat green.</summary>
	public const int KnownFigures = 8;

	/// <summary>
	/// What the player knows of a province, 0 to 9 (<c>docs/mechanics/intelligence.md</c>) - nothing where nobody plays.
	/// The colours and the tooltip's lines both ask it, as the original's ask the player's byte of the province's
	/// intelligence; with the fog of war off it is 9 everywhere.
	/// </summary>
	public static int Intelligence(CGameState state, CProvince province, CCountryTag? player)
	{
		return player is { } tag ? CProvinceIntel.Level(state, province, tag) : 0;
	}

	public static (CColor Ground, CColor Stripes) Of(CGameState state, CProvince province, CCountryTag? player)
	{
		int known = Intelligence(state, province, player);
		float light = known >= KnownBright ? 1f : Unknown;
		float grey = Grey * light;
		var plain = new CColor(grey, grey, grey);

		if (CSupplyFigures.MostInfrastructure(state, province) < LeastInfrastructure)
		{
			var black = new CColor(0f, 0f, 0f);
			return (black, black);
		}
		if (known < KnownColoured)
		{
			return (plain, plain);
		}
		var green = new CColor(grey, Bright * light, grey);
		if (known < KnownFigures)
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
