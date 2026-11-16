using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CFaction</c>, held by its tag - <c>axis</c> - with the empty tag standing for the null faction
/// every country outside one holds: <c>==</c> on the faction, <c>IsValid</c> false for the null one,
/// its tag and its leader, the first of its members.
/// </summary>
public static class CFactionClass
{
	private const string Name = "CFaction";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Operator("__eq", CLuaEquality.Same<CLuaNamed>(Name))
			.Method("IsValid", call => call.Return(call.Object<CLuaNamed>(1, Name).Name.Length > 0))
			.Method("GetTag", call => call.Return(api.String(call.Object<CLuaNamed>(1, Name).Name)))
			.Method("GetFactionLeader", call => call.Return(api.Tag(Leader(api, call.Object<CLuaNamed>(1, Name).Name))))
			.Method("GetNumberOfMembers", call =>
				call.Return(CCountryLists.FactionMembers(api.State, call.Object<CLuaNamed>(1, Name).Name).Count))
			// Its members' tags in its own order, the leader first (lua.md, section 4).
			.Method("GetMembers", call =>
			{
				var members = new List<object?>();
				foreach (Sim.Countries.CCountry member in CCountryLists.FactionMembers(api.State, call.Object<CLuaNamed>(1, Name).Name))
				{
					members.Add(api.Tag(member.Tag));
				}
				return call.ReturnIterator(members);
			});
	}

	/// <summary>A faction's leader, its first member; the null tag for a faction with none.</summary>
	public static CCountryTag Leader(CGameApi api, string faction)
	{
		List<Sim.Countries.CCountry> members = CCountryLists.FactionMembers(api.State, faction);
		return members.Count > 0 ? members[0].Tag : CGameApi.NullTag;
	}
}
