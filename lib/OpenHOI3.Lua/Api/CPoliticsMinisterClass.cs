using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CAIPoliticsMinister</c>, which <c>PoliticsMinister_Tick</c> is handed: its country's tag and its country's AI.
/// Whether an exile's capital is safe to liberate is not built, and answers false. See
/// <c>docs/mechanics/politics.md</c>, section 12.
/// </summary>
public static class CPoliticsMinisterClass
{
	private const string Name = "CAIPoliticsMinister";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetCountryTag", call => call.Return(api.Tag(Of(call).Tag)))
			.Method("GetOwnerAI", call => call.Return(new CLuaObject(api.Ai, new CLuaAi(Of(call)))))
			.Method("IsCapitalSafeToLiberate", call =>
			{
				CCountry country = Of(call);
				return call.Return(api.State.Country(call.Object<CCountryTag>(2, "CCountryTag")) is { } exile
					&& OpenHOI3.Sim.Wars.CLiberation.IsCapitalSafe(api.State, country, exile));
			});
	}

	private static CCountry Of(CLuaCall call) => call.Object<CLuaAi>(1, Name).Country;
}
