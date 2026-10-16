using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CTechnologyStatus</c>, what a country has of technology, handed over as the country it belongs to: the IC its
/// technology adds, the level it has of a technology and the year a level is due, whether a building or a unit type is
/// available to it, and the level arithmetic of a technology alone. See <c>docs/mechanics/ministers.md</c>, section 7,
/// and <c>reversing/findings/FINDINGS-ministers.md</c> A.6.
/// </summary>
public static class CTechnologyStatusClass
{
	public const string Name = "CTechnologyStatus";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetIcModifier", call => call.Return(api.Fixed(Of(call).Technology["ic_modifier"])))
			.Method("GetLevel", call => call.Return(Of(call).TechnologyLevels.GetValueOrDefault(Technology(call, 2).Name)))
			// The country is not asked: the year is the technology's own.
			.Method("GetYear", call =>
			{
				Of(call);
				return call.Return(CTechnologyClass.Year(Technology(call, 2), call.Integer(3)));
			})
			.Method("IsBuildingAvailable", call =>
				call.Return(CBuildingLines.IsAvailable(api.State, Of(call), call.Object<CBuilding>(2, CBuildingClass.Name))))
			.Method("IsUnitAvailable", call =>
				call.Return(CUnitAvailability.MayUse(api.State, Of(call), call.Object<CSubUnitType>(2, CLicenceActionClass.SubUnitName))))
			// The level arithmetic alone - no allow block, no queue.
			.Method("CanResearch", call =>
			{
				CTechnology technology = Technology(call, 2);
				int level = call.Integer(3);
				return call.Return(!(technology.IsSingleLevel && (level > 1 || level < 0)) && level <= technology.HighestLevel);
			});
	}

	private static CCountry Of(CLuaCall call) => call.Object<CCountry>(1, Name);

	private static CTechnology Technology(CLuaCall call, int argument) => call.Object<CTechnology>(argument, CTechnologyClass.Name);
}
