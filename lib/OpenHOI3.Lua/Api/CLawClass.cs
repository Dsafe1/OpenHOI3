using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CLaw</c>, <c>CLawGroup</c> and <c>CLawDataBase</c>: the laws numbered across their groups from 1, the null law 0,
/// and the groups with the null group <c>noLawgroup</c> first, as the original's law database holds them. A law is
/// <c>ValidFor</c> a country where its <c>allow</c> holds. See <c>docs/mechanics/politics.md</c>, section 12.
/// </summary>
public static class CLawClass
{
	private const string LawName = "CLaw";
	private const string GroupName = "CLawGroup";

	/// <summary>The null law, number 0, which a group of no law answers.</summary>
	public static readonly CLaw NullLaw = new("noLawgroup", new CModifier("noLaw"));

	/// <summary>The null group, number 0, which the database lists first.</summary>
	public static readonly CLawGroup NullGroup = new("noLawgroup", 0);

	public static CLuaClass DefineLaw(CGameApi api)
	{
		return new CLuaClass(LawName)
			.Operator("__eq", CLuaEquality.Same<CLaw>(LawName))
			.Method("GetIndex", call => call.Return(Law(call).Index))
			.Method("GetKey", call => call.Return(api.String(Law(call).Name)))
			.Method("GetGroup", call => call.Return(new CLuaObject(api.LawGroup, GroupOf(api.State, Law(call)))))
			.Method("ValidFor", call =>
			{
				CLaw law = Law(call);
				CCountry? country = api.CountryOf(call, 2);
				return call.Return(country is not null && CLaws.IsAllowed(api.State, country, law));
			});
	}

	public static CLuaClass DefineGroup(CGameApi api)
	{
		return new CLuaClass(GroupName)
			.Operator("__eq", CLuaEquality.Same<CLawGroup>(GroupName))
			.Method("GetIndex", call => call.Return(call.Object<CLawGroup>(1, GroupName).Index))
			.Method("GetKey", call => call.Return(api.String(call.Object<CLawGroup>(1, GroupName).Name)));
	}

	public static CLuaClass DefineDataBase(CGameApi api)
	{
		return new CLuaClass("CLawDataBase")
			.Static("GetGroups", call =>
			{
				var groups = new List<object?> { new CLuaObject(api.LawGroup, NullGroup) };
				foreach (CLawGroup group in api.State.LawGroups)
				{
					groups.Add(new CLuaObject(api.LawGroup, group));
				}
				return call.ReturnIterator(groups);
			})
			.Static("GetNumberOfLaws", call => call.Return(Laws(api.State).Count))
			// Unchecked in the original; past the end, nil here.
			.Static("GetLaw", call =>
			{
				List<CLaw> laws = Laws(api.State);
				int index = call.Integer(1);
				return call.Return(index >= 0 && index < laws.Count ? new CLuaObject(api.Law, laws[index]) : null);
			});
	}

	/// <summary>A country's law of a group: the null law for the null group or a group it has none of.</summary>
	public static CLaw LawOf(CCountry country, CLawGroup group)
	{
		return country.Laws.TryGetValue(group.Name, out CLaw? law) ? law : NullLaw;
	}

	/// <summary>Every law by its number, the null law first.</summary>
	private static List<CLaw> Laws(CGameState state)
	{
		var laws = new List<CLaw> { NullLaw };
		foreach (CLawGroup group in state.LawGroups)
		{
			laws.AddRange(group.Laws);
		}
		return laws;
	}

	private static CLawGroup GroupOf(CGameState state, CLaw law)
	{
		return state.LawGroup(law.Group) ?? NullGroup;
	}

	private static CLaw Law(CLuaCall call) => call.Object<CLaw>(1, LawName);
}
