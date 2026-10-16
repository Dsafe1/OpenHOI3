using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A province's record of one building, as a script is handed it - the original's <c>CProvinceBuilding</c>, which
/// every province holds for every building.
/// </summary>
public sealed class CLuaProvinceBuilding
{
	public CLuaProvinceBuilding(CProvince province, CBuilding building)
	{
		Province = province;
		Building = building;
	}

	public CProvince Province { get; }

	public CBuilding Building { get; }
}
