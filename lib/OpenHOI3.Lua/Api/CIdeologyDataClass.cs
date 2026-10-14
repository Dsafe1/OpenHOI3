using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CIdeologyData</c>, a country's figures for each party as <c>AccessIdeologyPopularity</c> hands them over:
/// <c>GetValue(ideology)</c>, the party's figure, unchecked - nought for a party the figures have no place for. See
/// <c>docs/mechanics/espionage.md</c>, section 15.
/// </summary>
public static class CIdeologyDataClass
{
	private const string Name = "CIdeologyData";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetValue", call =>
			{
				CPartyFigures figures = call.Object<CPartyFigures>(1, Name);
				string ideology = call.Object<CLuaNamed>(2, "CIdeology").Name;
				int index = api.State.Ideology(ideology)?.Index ?? 0;
				return call.Return(api.Fixed(new CFixedPoint(figures[index])));
			});
	}
}
