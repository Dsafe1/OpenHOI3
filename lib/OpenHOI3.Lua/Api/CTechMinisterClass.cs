using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CAITechMinister</c>, which <c>TechMinister_Tick</c> is handed: its country's tag and AI, and whether it may start a
/// technology - a real one, allowed now and not queued already. The monthly re-plan's two arrays are not built: nothing in
/// the original reads them but these two getters, which no file of Their Finest Hour calls. See
/// <c>docs/mechanics/ministers.md</c>, section 2.
/// </summary>
public static class CTechMinisterClass
{
	private const string Name = "CAITechMinister";

	private const string ModifiersUnbuilt = "the tech minister's monthly re-plan (GetTechModifers, GetFolderModifers): nil";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetCountryTag", call => call.Return(api.Tag(Of(call).Tag)))
			.Method("GetOwnerAI", call => call.Return(new CLuaObject(api.Ai, new CLuaAi(Of(call)))))
			.Method("CanResearch", call =>
			{
				CCountry country = Of(call);
				CTechnology technology = call.Object<CTechnology>(2, CTechnologyClass.Name);
				return call.Return(CResearch.MayResearchNext(api.State, country, technology)
					&& !country.ResearchQueue.Contains(technology.Name));
			})
			.Method("GetTechModifers", call =>
			{
				Of(call);
				api.ReportUnbuilt(ModifiersUnbuilt);
				return call.Return(null);
			})
			.Method("GetFolderModifers", call =>
			{
				Of(call);
				api.ReportUnbuilt(ModifiersUnbuilt);
				return call.Return(null);
			});
	}

	private static CCountry Of(CLuaCall call) => call.Object<CLuaAi>(1, Name).Country;
}
