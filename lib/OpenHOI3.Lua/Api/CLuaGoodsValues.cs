using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A <c>CGoodsValues</c>: one block of a route's terms a day, each good a single-precision number, in its
/// own order - money first, supplies last. A copy: writing to it changes nothing. See
/// <c>docs/mechanics/trade.md</c>, section 9.
/// </summary>
public sealed class CLuaGoodsValues
{
	public CLuaGoodsValues(System.Func<EGoods, long> amount)
	{
		Money = Single(amount(EGoods.Money));
		Fuel = Single(amount(EGoods.Fuel));
		CrudeOil = Single(amount(EGoods.CrudeOil));
		Metal = Single(amount(EGoods.Metal));
		Energy = Single(amount(EGoods.Energy));
		RareMaterials = Single(amount(EGoods.RareMaterials));
		Supplies = Single(amount(EGoods.Supplies));
	}

	public float Money { get; }

	public float Fuel { get; }

	public float CrudeOil { get; }

	public float Metal { get; }

	public float Energy { get; }

	public float RareMaterials { get; }

	public float Supplies { get; }

	/// <summary>Thousandths made a float, divided by 1000 in double precision and made a float again.</summary>
	private static float Single(long thousandths) => (float)((double)(float)thousandths / 1000.0);
}
