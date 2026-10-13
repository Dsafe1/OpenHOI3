using OpenHOI3.Lua.Native;

namespace OpenHOI3.Lua.Api;

/// <summary><c>CGoodsValues</c>, a block of a route's terms with its seven fields read with <c>.</c>.</summary>
public static class CGoodsValuesClass
{
	private const string Name = "CGoodsValues";

	public static CLuaClass Define()
	{
		return new CLuaClass(Name)
			.Field("vMoney", call => call.Return(Of(call).Money))
			.Field("vFuel", call => call.Return(Of(call).Fuel))
			.Field("vCrudeOil", call => call.Return(Of(call).CrudeOil))
			.Field("vMetal", call => call.Return(Of(call).Metal))
			.Field("vEnergy", call => call.Return(Of(call).Energy))
			.Field("vRareMaterials", call => call.Return(Of(call).RareMaterials))
			.Field("vSupplies", call => call.Return(Of(call).Supplies));
	}

	private static CLuaGoodsValues Of(CLuaCall call) => call.Object<CLuaGoodsValues>(1, Name);
}
