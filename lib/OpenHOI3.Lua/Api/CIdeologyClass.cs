using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CIdeology</c>, held by its name, and <c>CIdeologyGroup</c>, a corner of the alignment triangle:
/// an ideology's group and name, a group's name and faction, and <c>==</c> between two groups.
/// </summary>
public static class CIdeologyClass
{
	public static CLuaClass DefineIdeology(CGameApi api)
	{
		const string name = "CIdeology";
		return new CLuaClass(name)
			.Method("GetKey", call => call.Return(api.String(call.Object<CLuaNamed>(1, name).Name)))
			.Method("GetGroup", call =>
			{
				string ideology = call.Object<CLuaNamed>(1, name).Name;
				foreach (CIdeologyGroup group in api.State.IdeologyGroups)
				{
					if (group.Ideologies.Contains(ideology))
					{
						return call.Return(new CLuaObject(api.IdeologyGroup, group));
					}
				}
				return call.Return(null);
			});
	}

	public static CLuaClass DefineGroup(CGameApi api)
	{
		const string name = "CIdeologyGroup";
		return new CLuaClass(name)
			.Operator("__eq", CLuaEquality.Same<CIdeologyGroup>(name))
			.Method("GetKey", call => call.Return(api.String(call.Object<CIdeologyGroup>(1, name).Name)))
			.Method("GetFaction", call =>
			{
				string faction = call.Object<CIdeologyGroup>(1, name).Faction;
				return call.Return(faction.Length > 0 ? api.FactionNamed(faction) : null);
			});
	}
}
