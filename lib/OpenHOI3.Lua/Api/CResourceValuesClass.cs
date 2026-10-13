using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CResourceValues</c>, made by a script with <c>CResourceValues()</c> and filled for one good of
/// one country; its six fields read with <c>.</c>. See <c>docs/mechanics/lua.md</c>, section 4.2.
/// </summary>
public static class CResourceValuesClass
{
	private const string Name = "CResourceValues";

	public static CLuaClass Define(CGameApi api)
	{
		CLuaClass luaClass = new CLuaClass(Name)
			.Method("GetResourceValues", call =>
			{
				CLuaResourceValues values = call.Object<CLuaResourceValues>(1, Name);
				CCountry country = call.Object<CCountry>(2, "CCountry");
				EGoods goods = CGoodsPoolClass.Goods(call, 3);
				values.DailyExpense = CCountryGoods.AiExpense(api, country, goods);
				values.DailyHome = CLuaFloats.SingleThousandths((int)country.HomeProduced[goods].Raw);
				values.ConvoyedIn = CLuaFloats.SingleThousandths((int)country.ConvoyedIn[goods].Raw);
				values.Pool = CLuaFloats.SingleThousandths((int)api.State.Province(country.Capital).Pool[goods].Raw);
				values.DailyIncome = CLuaFloats.SingleThousandths((int)CCountryGoods.Income(api, country, goods));
				values.DailyBalance = CLuaFloats.SingleThousandths((int)CCountryGoods.Balance(api, country, goods));
				return 0;
			})
			.Field("vDailyExpense", call => call.Return(call.Object<CLuaResourceValues>(1, Name).DailyExpense))
			.Field("vDailyHome", call => call.Return(call.Object<CLuaResourceValues>(1, Name).DailyHome))
			.Field("vConvoyedIn", call => call.Return(call.Object<CLuaResourceValues>(1, Name).ConvoyedIn))
			.Field("vPool", call => call.Return(call.Object<CLuaResourceValues>(1, Name).Pool))
			.Field("vDailyIncome", call => call.Return(call.Object<CLuaResourceValues>(1, Name).DailyIncome))
			.Field("vDailyBalance", call => call.Return(call.Object<CLuaResourceValues>(1, Name).DailyBalance));
		return luaClass.Constructs(call => call.Return(new CLuaObject(luaClass, new CLuaResourceValues())));
	}
}
