using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The commands the intelligence script makes and posts: <c>CChangeSpyPriority(actor, target, priority)</c> and
/// <c>CChangeSpyMission(actor, target, mission, priority)</c>, each taking exactly its arguments - the mission truncated,
/// a priority to the nearest - as the original's constructors do. See <c>docs/mechanics/espionage.md</c>, section 15.
/// </summary>
public static class CSpyCommandClass
{
	public static CLuaClass DefinePriority(CGameApi api)
	{
		var luaClass = new CLuaClass("CChangeSpyPriority");
		return luaClass.Constructs(call =>
		{
			Arguments(call, 3, "CChangeSpyPriority(CCountryTag const&, CCountryTag const&, int)");
			CCountryTag actor = call.Object<CCountryTag>(2, "CCountryTag");
			CCountryTag target = call.Object<CCountryTag>(3, "CCountryTag");
			return call.Return(new CLuaObject(luaClass, new CChangeSpyPriorityCommand(actor, target, call.Integer(4))));
		});
	}

	public static CLuaClass DefineMission(CGameApi api)
	{
		var luaClass = new CLuaClass("CChangeSpyMission");
		return luaClass.Constructs(call =>
		{
			Arguments(call, 4, "CChangeSpyMission(CCountryTag const&, CCountryTag const&, SpyMission, int)");
			CCountryTag actor = call.Object<CCountryTag>(2, "CCountryTag");
			CCountryTag target = call.Object<CCountryTag>(3, "CCountryTag");
			int mission = (int)call.Number(4);
			return call.Return(new CLuaObject(luaClass, new CChangeSpyMissionCommand(actor, target, mission, call.Integer(5))));
		});
	}

	/// <summary>Exactly so many arguments after the class, as luabind holds the original's constructors to; else its error.</summary>
	private static void Arguments(CLuaCall call, int count, string candidate)
	{
		if (call.ArgumentCount != count + 1)
		{
			throw new CLuaError($"No matching overload found, candidates: void __init(luabind::argument const&,{candidate})");
		}
	}
}
