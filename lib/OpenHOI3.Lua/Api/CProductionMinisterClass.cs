using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CAIProductionMinister</c>, which <c>ProductionMinister_Tick</c> is handed: its country's tag and AI, the ships of
/// transports and of escorts its queue is still to deliver, its kept desperation, and <c>PrioritizeBuildQueue</c>. See
/// <c>docs/mechanics/ministers.md</c>, section 4.
/// </summary>
public static class CProductionMinisterClass
{
	private const string Name = "CAIProductionMinister";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetCountryTag", call => call.Return(api.Tag(Of(call).Tag)))
			// Every minister's, from the original's CAIAgent (lua.md, section 6).
			.Method("GetCountry", call => call.Return(api.Of(Of(call))))
			.Method("GetOwnerAI", call => call.Return(new CLuaObject(api.Ai, new CLuaAi(Of(call)))))
			.Method("CountTransportsUnderConstruction", call =>
				call.Return(CProductionAi.ShipsUnderConstruction(api.State, Of(call), escorts: false)))
			.Method("CountEscortsUnderConstruction", call =>
				call.Return(CProductionAi.ShipsUnderConstruction(api.State, Of(call), escorts: true)))
			// The minister's own copy, a float - as the original keeps it.
			.Method("GetDesperation", call => call.Return((double)(float)(Of(call).MinisterDesperation / 1000.0)))
			.Method("PrioritizeBuildQueue", call =>
			{
				CProductionAi.PrioritizeBuildQueue(api.State, Of(call));
				return 0;
			});
	}

	private static CCountry Of(CLuaCall call) => call.Object<CLuaAi>(1, Name).Country;
}
