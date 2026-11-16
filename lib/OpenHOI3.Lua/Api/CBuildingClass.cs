using System;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CBuilding</c>, <c>CBuildingDataBase</c> and <c>CProvinceBuilding</c> - a province's own record of one building,
/// handed over as the pair - as the production script reaches them. See <c>docs/mechanics/ministers.md</c>, section 7,
/// and <c>reversing/findings/FINDINGS-ministers.md</c> D.1.
/// </summary>
public static class CBuildingClass
{
	public const string Name = "CBuilding";
	private const string ProvinceBuildingName = "CProvinceBuilding";

	/// <summary>A building's index: its place in its file, from one - the original's null building being nought.</summary>
	public static int IndexOf(CBuilding building) => building.Index + 1;

	public static CLuaClass DefineBuilding(CGameApi api)
	{
		return new CLuaClass(Name)
			.Operator("__eq", CLuaEquality.Same<CBuilding>(Name))
			.Method("GetIndex", call => call.Return(IndexOf(call.Object<CBuilding>(1, Name))))
			.Method("GetKey", call => call.Return(api.String(call.Object<CBuilding>(1, Name).Key)));
	}

	/// <summary>A building by its exact key, case and all; nil for an unknown one.</summary>
	public static CLuaClass DefineDataBase(CGameApi api)
	{
		return new CLuaClass("CBuildingDataBase")
			.Static("GetBuilding", call =>
			{
				string key = call.String(1);
				foreach (CBuilding building in api.State.Buildings)
				{
					if (string.Equals(building.Key, key, StringComparison.Ordinal))
					{
						return call.Return(new CLuaObject(api.Building, building));
					}
				}
				return call.Return(null);
			});
	}

	/// <summary>
	/// <c>GetMax()</c> the level built; <c>GetCurrent()</c> the level working, what bombs have left of it
	/// (<c>docs/mechanics/bombing.md</c>, section 6).
	/// </summary>
	public static CLuaClass DefineProvinceBuilding(CGameApi api)
	{
		return new CLuaClass(ProvinceBuildingName)
			.Method("GetMax", call =>
			{
				CLuaProvinceBuilding held = call.Object<CLuaProvinceBuilding>(1, ProvinceBuildingName);
				return call.Return(api.Fixed(held.Province.BuildingMaxLevel(held.Building)));
			})
			.Method("GetCurrent", call =>
			{
				CLuaProvinceBuilding held = call.Object<CLuaProvinceBuilding>(1, ProvinceBuildingName);
				return call.Return(api.Fixed(held.Province.BuildingLevel(held.Building)));
			});
	}
}
