using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CGoodsPool</c>, a province's goods - a country's stockpile is its capital's - with the goods
/// numbered as the original's <c>GoodsCategory</c>, which are also its constants.
/// </summary>
public static class CGoodsPoolClass
{
	private const string Name = "CGoodsPool";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("Get", call => call.Return(api.Fixed(call.Object<CGoodsPool>(1, Name)[Goods(call, 2)])))
			.Method("GetFloat", call => call.Return(CLuaFloats.Thousandths(call.Object<CGoodsPool>(1, Name)[Goods(call, 2)].Raw)))
			.Constant("_SUPPLIES_", 0)
			.Constant("_FUEL_", 1)
			.Constant("_MONEY_", 2)
			.Constant("_CRUDE_OIL_", 3)
			.Constant("_METAL_", 4)
			.Constant("_ENERGY_", 5)
			.Constant("_RARE_MATERIALS_", 6)
			.Constant("_GC_NUMOF_", CGoodsPool.Count);
	}

	/// <summary>A goods argument: one of the seven, in the original's order, which is the simulation's.</summary>
	public static EGoods Goods(CLuaCall call, int argument)
	{
		int goods = call.Integer(argument);
		if (goods < 0 || goods >= CGoodsPool.Count)
		{
			throw new CLuaError($"bad argument #{argument} (no goods numbered {goods})");
		}
		return (EGoods)goods;
	}
}
