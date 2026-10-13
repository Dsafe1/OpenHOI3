using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CModifier</c>, a country's global modifier, read by its kind's number - of which the scripts ask
/// only <c>_MODIFIER_GLOBAL_IC_</c>; and <c>CTechnologyStatus</c>, what its technology adds, of which they
/// ask only the IC. Both are handed over as the country they belong to. See <c>docs/mechanics/trade.md</c>,
/// section 9.
/// </summary>
public static class CCountryModifiersClass
{
	private const string ModifierName = "CModifier";
	private const string TechnologyName = "CTechnologyStatus";

	/// <summary>The original's number for the global IC modifier.</summary>
	public const int GlobalIc = 17;

	public static CLuaClass DefineModifier(CGameApi api)
	{
		return new CLuaClass(ModifierName)
			.Method("GetValue", call =>
			{
				CCountry country = call.Object<CCountry>(1, ModifierName);
				int kind = call.Integer(2);
				if (kind != GlobalIc)
				{
					api.ReportUnbuilt($"CModifier:GetValue of the kind numbered {kind}: only the global IC's is known, so 0");
					return call.Return(api.Fixed(CFixedPoint.Zero));
				}
				return call.Return(api.Fixed(country.Modifier["global_ic"]));
			})
			.Constant("_MODIFIER_GLOBAL_IC_", GlobalIc);
	}

	public static CLuaClass DefineTechnology(CGameApi api)
	{
		return new CLuaClass(TechnologyName)
			.Method("GetIcModifier", call => call.Return(api.Fixed(call.Object<CCountry>(1, TechnologyName).Technology["ic_modifier"])));
	}
}
