using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CAIEspionageMinister</c>, which <c>IntelligenceMinister_Tick</c> is handed: its country's tag and its country's
/// AI. See <c>docs/mechanics/espionage.md</c>, section 15.
/// </summary>
public static class CIntelligenceMinisterClass
{
	private const string Name = "CAIEspionageMinister";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetCountryTag", call => call.Return(api.Tag(Of(call).Tag)))
			.Method("GetOwnerAI", call => call.Return(new CLuaObject(api.Ai, new CLuaAi(Of(call)))));
	}

	private static CCountry Of(CLuaCall call) => call.Object<CLuaAi>(1, Name).Country;
}
