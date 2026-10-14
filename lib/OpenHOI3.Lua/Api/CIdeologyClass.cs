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
				// The null ideology's group is the null group: the rebels' politics minister reads it.
				return call.Return(ideology == NullIdeology ? new CLuaObject(api.IdeologyGroup, NullGroup) : null);
			});
	}

	/// <summary>
	/// The null ideology, a country's with no ruling party - the rebels' - as the save writes it. Ours: what the
	/// original's null ideology and its group answer was not read; here its group is one of no name.
	/// </summary>
	public const string NullIdeology = "noIdeology";

	/// <summary>The null ideology's group.</summary>
	public static readonly CIdeologyGroup NullGroup = new("", 0, 0);

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
