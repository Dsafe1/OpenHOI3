using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CModifier</c>, a country's global modifier - <c>country:GetGlobalModifier()</c>, handed over as the country it belongs
/// to - read by a kind's number with <c>GetValue</c>, and the 82 constants that name the numbers (<see cref="CModifierTypes"/>).
/// See <c>docs/mechanics/lua.md</c>, section 4.7, and <c>docs/mechanics/trade.md</c>, section 9. <c>CTechnologyStatus</c> is
/// <see cref="CTechnologyStatusClass"/>.
/// </summary>
public static class CCountryModifiersClass
{
	private const string ModifierName = "CModifier";

	/// <summary>The original's number for the global IC modifier.</summary>
	public const int GlobalIc = 17;

	public static CLuaClass DefineModifier(CGameApi api)
	{
		CLuaClass luaClass = new CLuaClass(ModifierName)
			.Method("GetValue", call =>
			{
				CCountry country = call.Object<CCountry>(1, ModifierName);
				int number = call.Integer(2);
				if (CModifierTypes.KeyOf(number) is not { } key)
				{
					api.ReportUnbuilt($"CModifier:GetValue of the kind numbered {number}: no kind of the original's is known by it, so 0");
					return call.Return(api.Fixed(CFixedPoint.Zero));
				}
				return call.Return(api.Fixed(country.Modifier[key]));
			});
		for (int number = 0; number < CModifierTypes.All.Length; number++)
		{
			if (CModifierTypes.All[number].Constant is { } constant)
			{
				luaClass.Constant(constant, number);
			}
		}
		return luaClass;
	}
}
