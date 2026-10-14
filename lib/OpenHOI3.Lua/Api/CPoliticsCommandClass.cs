using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The commands the politics script makes and posts: <c>CChangeLawCommand(tag, law, group)</c>,
/// <c>CChangeMinisterCommand(tag, minister, position)</c>, <c>CToggleMobilizationCommand(tag, flag)</c> and
/// <c>CLiberateCountryCommand(liberated, liberator)</c> - the last one not built, so posting it does nothing and is
/// reported. <see cref="Post"/> is what <c>CCurrentGameState.Post</c> and <c>ai:Post</c> do with one. See
/// <c>docs/mechanics/politics.md</c>, section 12.
/// </summary>
public static class CPoliticsCommandClass
{
	private const string LawCommandName = "CChangeLawCommand";
	private const string LiberateName = "CLiberateCountryCommand";

	/// <summary>What liberating a country would need, which is not built.</summary>
	private const string LiberationUnbuilt = "liberating a country (CLiberateCountryCommand): nothing is posted";

	public static CLuaClass DefineChangeLaw(CGameApi api)
	{
		CLuaClass luaClass = new CLuaClass(LawCommandName)
			.Method("SetEnablePostMessage", call =>
			{
				call.Object<CLuaLawCommand>(1, LawCommandName).PostMessage = call.Boolean(2);
				return 0;
			});
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			CLaw law = call.Object<CLaw>(3, "CLaw");
			CLawGroup group = call.Object<CLawGroup>(4, "CLawGroup");
			return call.Return(new CLuaObject(luaClass, new CLuaLawCommand(tag, law, group, underWay: !api.State.IsBeingMade)));
		});
	}

	public static CLuaClass DefineChangeMinister(CGameApi api)
	{
		var luaClass = new CLuaClass("CChangeMinisterCommand");
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			CMinister minister = call.Object<CMinister>(3, "CMinister");
			CGovernmentPosition position = CMinisterClass.Position(call, 4);
			return call.Return(new CLuaObject(luaClass, new CChangeMinisterCommand(tag, minister.Id, position.Name)));
		});
	}

	public static CLuaClass DefineToggleMobilization(CGameApi api)
	{
		var luaClass = new CLuaClass("CToggleMobilizationCommand");
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			return call.Return(new CLuaObject(luaClass, new CToggleMobilisationCommand(tag, call.Boolean(3))));
		});
	}

	public static CLuaClass DefineLiberate(CGameApi api)
	{
		var luaClass = new CLuaClass(LiberateName);
		return luaClass.Constructs(call =>
		{
			CCountryTag liberated = call.Object<CCountryTag>(2, "CCountryTag");
			call.Object<CCountryTag>(3, "CCountryTag");
			return call.Return(new CLuaObject(luaClass, new CLuaNamed(liberated.Value)));
		});
	}

	/// <summary>A command a script posts, to the game's outbox: the law command made the simulation's first.</summary>
	public static int Post(CGameApi api, CLuaCall call, int argument)
	{
		object? posted = call.ObjectAt(argument, out CLuaClass? luaClass);
		switch (posted)
		{
			case CLuaLawCommand law:
				api.Post(law.ToCommand());
				break;
			case CCommand command:
				api.Post(command);
				break;
			default:
				if (luaClass?.Name == LiberateName)
				{
					api.ReportUnbuilt(LiberationUnbuilt);
					break;
				}
				throw new CLuaError($"No matching overload found, candidates: void Post({CommandName(luaClass)})");
		}
		return 0;
	}

	private static string CommandName(CLuaClass? luaClass) => luaClass?.Name ?? "nil";
}
