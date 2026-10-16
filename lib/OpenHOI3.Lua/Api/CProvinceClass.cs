using System;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Construction;
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
			})
			// The production script's (ministers.md, section 7): a province's record of a building, never nil.
			.Method("GetBuilding", call => call.Return(new CLuaObject(api.ProvinceBuilding,
				new CLuaProvinceBuilding(call.Object<CProvince>(1, name), call.Object<CBuilding>(2, CBuildingClass.Name)))))
			.Method("HasBuilding", call => call.Return(
				call.Object<CProvince>(1, name).BuildingMaxLevel(call.Object<CBuilding>(2, CBuildingClass.Name)).Raw > 0))
			.Method("GetCurrentConstructionLevel", call => call.Return(
				ConstructionLevel(api, call.Object<CProvince>(1, name), call.Object<CBuilding>(2, CBuildingClass.Name))))
			.Method("IsFrontProvince", call => call.Return(IsFront(api, call.Object<CProvince>(1, name), call.Boolean(2))));
	}

	/// <summary>
	/// The size of the first line of a building placed in the province, in its owner's queue; nought for none.
	/// </summary>
	private static int ConstructionLevel(CGameApi api, CProvince province, CBuilding building)
	{
		if (api.State.Country(province.Owner) is not { } owner)
		{
			return 0;
		}
		foreach (CConstruction line in owner.Constructions)
		{
			if (line.Building == building && line.Location == province.Id)
			{
				return line.Size;
			}
		}
		return 0;
	}

	/// <summary>
	/// Whether an owned province touches land an enemy of its holder holds - by any edge but an impassable one, a strait
	/// among them - passing over the sea, land nobody owns and land its holder holds; with <paramref name="withUnits"/>
	/// that land must hold a land unit too, anybody's. Undeclared wars are not built, so the enemy is one at war or
	/// either side the rebels.
	/// </summary>
	public static bool IsFront(CGameApi api, CProvince province, bool withUnits)
	{
		if (province.Owner is null || province.Controller is not { } controller
			|| api.State.Country(controller) is not { } holder)
		{
			return false;
		}
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince neighbour = api.State.Province(edge.To);
			if (neighbour.IsSea || neighbour.Owner is null || neighbour.Controller is not { } other || other == controller)
			{
				continue;
			}
			if (!Sim.Countries.CCountryLists.IsEnemy(api.State, holder, other))
			{
				continue;
			}
			if (!withUnits || HasLandUnit(api, neighbour))
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasLandUnit(CGameApi api, CProvince province)
	{
		foreach (Sim.Units.CUnit unit in api.State.UnitsIn(province.Id))
		{
			if (!unit.IsRemoved && Sim.Movement.CMovementRules.IsLandUnit(unit))
			{
				return true;
			}
		}
		return false;
	}

	public static CLuaClass DefineContinent(CGameApi api)
	{
		const string name = "CContinent";
		return new CLuaClass(name)
			.Operator("__eq", CLuaEquality.By<CLuaNamed>(name, (a, b) => string.Equals(a.Name, b.Name, StringComparison.Ordinal)))
			.Method("GetTag", call => call.Return(api.String(call.Object<CLuaNamed>(1, name).Name)));
	}
}
