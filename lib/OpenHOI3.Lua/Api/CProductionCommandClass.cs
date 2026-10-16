using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The commands the tech and production scripts make and post, and what they are made of: <c>SubUnitList</c>,
/// <c>CID</c> and <c>CNullTag</c>; the unit, building and convoy orders; the investment, leadership and lend-lease
/// shares; and the start of research. See <c>docs/mechanics/ministers.md</c>, sections 3, 5 and 7, and
/// <c>reversing/findings/FINDINGS-ministers.md</c> A.4, B.4, C.6 and D.2.
/// </summary>
public static class CProductionCommandClass
{
	private const string ListName = "SubUnitList";
	private const string LendLeaseName = "CChangeLendLeaseDistributionCommand";

	/// <summary>An empty list, to which a type is appended at the tail - nil not refused.</summary>
	public static CLuaClass DefineSubUnitList(CGameApi api)
	{
		var luaClass = new CLuaClass(ListName);
		luaClass
			.Static("Append", call =>
			{
				CLuaSubUnitList list = call.Object<CLuaSubUnitList>(1, ListName);
				list.Types.Add(call.ObjectAt(2, out _) as CSubUnitType);
				return 0;
			})
			.Method("GetSize", call => call.Return(call.Object<CLuaSubUnitList>(1, ListName).Types.Count))
			.Method("IsEmpty", call => call.Return(call.Object<CLuaSubUnitList>(1, ListName).Types.Count == 0))
			.Method("RemoveAll", call =>
			{
				call.Object<CLuaSubUnitList>(1, ListName).Types.Clear();
				return 0;
			});
		return luaClass.Constructs(call => call.Return(new CLuaObject(luaClass, new CLuaSubUnitList())));
	}

	/// <summary>The null id, which aims an order at no unit.</summary>
	public static CLuaClass DefineId(CGameApi api)
	{
		var luaClass = new CLuaClass("CID");
		return luaClass.Constructs(call => call.Return(new CLuaObject(luaClass, new CLuaNamed("CID"))));
	}

	/// <summary>The null tag <c>---</c>, which names no country.</summary>
	public static CLuaClass DefineNullTag(CGameApi api)
	{
		return new CLuaClass("CNullTag").Constructs(call => call.Return(api.Tag(CGameApi.NullTag)));
	}

	/// <summary>
	/// <c>CConstructUnitCommand(tag, list, province, count, reserve, builder, target)</c>: one line of <c>count</c>
	/// units of the list's brigades, one after another. The province is thrown away, the null builder is the country
	/// and the null target aims at nothing - as the script always passes them. A list holding nil is refused.
	/// </summary>
	public static CLuaClass DefineConstructUnit(CGameApi api)
	{
		var luaClass = new CLuaClass("CConstructUnitCommand");
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			CLuaSubUnitList list = call.Object<CLuaSubUnitList>(3, ListName);
			call.Integer(4);
			int count = call.Integer(5);
			bool reserve = Strict(call, 6);
			call.Object<CCountryTag>(7, "CCountryTag");
			call.Object<CLuaNamed>(8, "CID");
			var types = new List<string>();
			foreach (CSubUnitType? type in list.Types)
			{
				types.Add(type?.Key ?? "");
			}
			return call.Return(new CLuaObject(luaClass, new CConstructUnitCommand(tag, types, count, reserve)));
		});
	}

	/// <summary>
	/// <c>CConstructBuildingCommand(tag, building, province, count)</c> - a nil building accepted, and refused - and its
	/// <c>IsValid</c>, the province view's test, which the script asks before posting.
	/// </summary>
	public static CLuaClass DefineConstructBuilding(CGameApi api)
	{
		var luaClass = new CLuaClass("CConstructBuildingCommand");
		luaClass.Method("IsValid", call => call.Return(call.Object<CConstructBuildingCommand>(1, luaClass.Name).MayExecute(api.State)));
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			string building = call.ObjectAt(3, out _) is CBuilding known ? known.Key : "";
			int province = call.Integer(4);
			int count = call.Integer(5);
			return call.Return(new CLuaObject(luaClass, new CConstructBuildingCommand(tag, building, province, count)));
		});
	}

	/// <summary><c>CConstructConvoyCommand(tag, escort, count)</c>: the second a Lua boolean, true for escorts.</summary>
	public static CLuaClass DefineConstructConvoy(CGameApi api)
	{
		var luaClass = new CLuaClass("CConstructConvoyCommand");
		luaClass.Method("IsValid", call =>
		{
			call.Object<CConstructConvoyCommand>(1, luaClass.Name);
			return call.Return(true);
		});
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			bool escorts = Strict(call, 3);
			int count = call.Integer(4);
			return call.Return(new CLuaObject(luaClass, new CConstructConvoyCommand(tag, escorts, count)));
		});
	}

	/// <summary><c>CChangeInvestmentCommand(tag, lendLease, consumer, production, supply, reinforce, upgrade)</c>.</summary>
	public static CLuaClass DefineChangeInvestment(CGameApi api)
	{
		var luaClass = new CLuaClass("CChangeInvestmentCommand");
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			return call.Return(new CLuaObject(luaClass, new CChangeInvestmentCommand(tag, CLuaShares.Normalised(Numbers(call, 3, 6)))));
		});
	}

	/// <summary><c>CChangeLeadershipCommand(tag, officers, diplomacy, espionage, research)</c>.</summary>
	public static CLuaClass DefineChangeLeadership(CGameApi api)
	{
		var luaClass = new CLuaClass("CChangeLeadershipCommand");
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			return call.Return(new CLuaObject(luaClass, new CChangeLeadershipCommand(tag, CLuaShares.Normalised(Numbers(call, 3, 4)))));
		});
	}

	/// <summary><c>CStartResearchCommand(tag, technology)</c>.</summary>
	public static CLuaClass DefineStartResearch(CGameApi api)
	{
		var luaClass = new CLuaClass("CStartResearchCommand");
		return luaClass.Constructs(call =>
		{
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			CTechnology technology = call.Object<CTechnology>(3, CTechnologyClass.Name);
			return call.Return(new CLuaObject(luaClass, new CStartResearchCommand(tag, technology.Name)));
		});
	}

	/// <summary>
	/// <c>CChangeLendLeaseDistributionCommand(tag)</c> and its <c>SetData(tags, values)</c>: each receiver's weight, as
	/// given.
	/// </summary>
	public static CLuaClass DefineLendLeaseDistribution(CGameApi api)
	{
		var luaClass = new CLuaClass(LendLeaseName);
		luaClass.Method("SetData", call =>
		{
			CLuaLendLeaseDistribution command = call.Object<CLuaLendLeaseDistribution>(1, LendLeaseName);
			CLuaArray tags = call.Object<CLuaArray>(2, "CArrayCountryTag");
			CLuaArray values = call.Object<CLuaArray>(3, "CArrayFix");
			command.Weights.Clear();
			for (int i = 0; i < tags.Values.Count && i < values.Values.Count; i++)
			{
				if (tags.Values[i] is CLuaObject { Target: CCountryTag receiver }
					&& values.Values[i] is CLuaObject { Target: CFixedPoint weight })
				{
					command.Weights.Add((receiver, weight.Raw));
				}
			}
			return 0;
		});
		return luaClass.Constructs(call =>
			call.Return(new CLuaObject(luaClass, new CLuaLendLeaseDistribution(call.Object<CCountryTag>(2, "CCountryTag")))));
	}

	/// <summary>Arguments as luabind hands them to <c>float</c> parameters.</summary>
	private static double[] Numbers(CLuaCall call, int first, int count)
	{
		var numbers = new double[count];
		for (int i = 0; i < count; i++)
		{
			numbers[i] = call.Number(first + i);
		}
		return numbers;
	}

	/// <summary>A <c>bool</c> parameter, which only a Lua boolean matches - a number matches no overload.</summary>
	private static bool Strict(CLuaCall call, int argument)
	{
		if (call.TypeOf(argument) != ELuaType.Boolean)
		{
			throw new CLuaError($"No matching overload found: argument #{argument} must be a boolean");
		}
		return call.Boolean(argument);
	}
}
