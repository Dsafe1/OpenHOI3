using System;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CProvince</c> - its continent, owner, controller, number and how many units stand in it - and <c>CContinent</c>, held by its
/// name, two of which are equal when their names are.
/// </summary>
public static class CProvinceClass
{
	public static CLuaClass DefineProvince(CGameApi api)
	{
		const string name = "CProvince";
		return new CLuaClass(name)
			.Method("GetProvinceID", call => call.Return(call.Object<CProvince>(1, name).Id))
			.Method("GetOwner", call => call.Return(api.Tag(call.Object<CProvince>(1, name).Owner)))
			.Method("GetController", call => call.Return(api.Tag(call.Object<CProvince>(1, name).Controller)))
			// Every unit standing in the province, whoever owns it: the count of its own list (lua.md, section 6).
			.Method("GetNumberOfUnits", call => call.Return(api.State.UnitsIn(call.Object<CProvince>(1, name).Id).Count))
			.Method("GetContinent", call =>
			{
				string continent = call.Object<CProvince>(1, name).Continent;
				return call.Return(continent.Length > 0 ? api.ContinentNamed(continent) : null);
			});
	}

	public static CLuaClass DefineContinent(CGameApi api)
	{
		const string name = "CContinent";
		return new CLuaClass(name)
			.Operator("__eq", CLuaEquality.By<CLuaNamed>(name, (a, b) => string.Equals(a.Name, b.Name, StringComparison.Ordinal)))
			.Method("GetTag", call => call.Return(api.String(call.Object<CLuaNamed>(1, name).Name)));
	}
}
