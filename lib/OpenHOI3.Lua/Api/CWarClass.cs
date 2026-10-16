using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CWar</c>, which the countries' production files ask how long it has run: the calendar months from its start to
/// now, nought for a war with no start (<c>reversing/findings/FINDINGS-war.md</c>, the record's
/// <c>CWar::GetCurrentRunningTimeInMonths</c>).
/// </summary>
public static class CWarClass
{
	private const string Name = "CWar";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetCurrentRunningTimeInMonths", call =>
			{
				CWar war = call.Object<CWar>(1, Name);
				if (war.Started == default)
				{
					return call.Return(0);
				}
				CDate now = api.State.Date;
				return call.Return(now.Year * 12 + now.Month - (war.Started.Year * 12 + war.Started.Month));
			});
	}
}
