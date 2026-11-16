using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CLicenceTechnologyAction</c>, an offer to buy a licence a script makes with
/// <c>CLicenceTechnologyAction(buyer, seller)</c> or is asked to answer; <c>CSubUnitDefinition</c>, a unit
/// type, with its own build figures; and <c>CSubUnitDataBase.GetSubUnit(name)</c>. The getter is spelled
/// <c>GetParalell</c> and the setter <c>SetParallel</c>, the game's names. See
/// <c>docs/mechanics/aid.md</c>, section 4.
/// </summary>
public static class CLicenceActionClass
{
	private const string ActionName = "CLicenceTechnologyAction";
	public const string SubUnitName = "CSubUnitDefinition";

	public static CLuaClass DefineAction(CGameApi api)
	{
		CLuaClass luaClass = new CLuaClass(ActionName)
			.Method("GetSubunit", call => Of(call).UnitType.Length > 0 && api.State.SubUnitTypes.TryGetValue(Of(call).UnitType, out CSubUnitType? type)
				? call.Return(new CLuaObject(api.SubUnit, type))
				: call.Return(null))
			.Method("SetSubunit", call =>
			{
				Of(call).UnitType = call.TypeOf(2) == ELuaType.Nil ? "" : call.Object<CSubUnitType>(2, SubUnitName).Key;
				return 0;
			})
			.Method("GetSerial", call => call.Return(Of(call).Serial))
			.Method("SetSerial", call =>
			{
				Of(call).Serial = (int)call.Number(2);
				return 0;
			})
			.Method("GetParalell", call => call.Return(Of(call).Parallel))
			.Method("SetParallel", call =>
			{
				Of(call).Parallel = (int)call.Number(2);
				return 0;
			})
			.Method("GetMoney", call => call.Return(api.Fixed(Of(call).Money)))
			.Method("SetMoney", call =>
			{
				Of(call).Money = call.Object<CFixedPoint>(2, "CFixedPoint");
				return 0;
			})
			.Method("SetValue", call =>
			{
				Of(call).Value = call.Boolean(2);
				return 0;
			})
			.Method("IsValid", call => call.Return(COfferRules.IsValid(api.State, Of(call).Offer)))
			.Method("IsSelectable", call => call.Return(COfferRules.IsSelectable(api.State, Of(call).Offer)))
			.Method("GetAIAcceptance", call => call.Return(CPactPasses.Score(api.State, Of(call).Offer, api.Scripts)))
			.Method("GetActor", call => call.Return(api.Tag(Of(call).Actor)))
			.Method("GetRecipient", call => call.Return(api.Tag(Of(call).Recipient)))
			.Method("GetValue", call => call.Return(Of(call).Value));
		return luaClass.Constructs(call =>
		{
			CCountryTag actor = call.Object<CCountryTag>(2, "CCountryTag");
			CCountryTag recipient = call.Object<CCountryTag>(3, "CCountryTag");
			return call.Return(new CLuaObject(luaClass, new CLuaLicenceAction(actor, recipient, api.State.TotalHours)));
		});
	}

	/// <summary>A unit type's own build figures - without any country's technology - and whether it is a capital ship.</summary>
	public static CLuaClass DefineSubUnit(CGameApi api)
	{
		return new CLuaClass(SubUnitName)
			.Operator("__eq", CLuaEquality.Same<CSubUnitType>(SubUnitName))
			.Method("GetBuildCostIC", call => call.Return(api.Fixed(Type(call).Stats[CSubUnitStats.BuildCostIc])))
			.Method("GetBuildCostMP", call => call.Return(api.Fixed(Type(call).Stats[CSubUnitStats.BuildCostManpower])))
			.Method("GetBuildTime", call => call.Return(api.Fixed(Type(call).Stats[CSubUnitStats.BuildTime])))
			.Method("IsCapitalShip", call => call.Return(Type(call).Flag("capital")))
			// Its transport flag, the type's own (lua.md, section 4.6).
			.Method("IsTransport", call => call.Return(Type(call).Flag(Sim.Combat.CShipCensus.TransportFlag)))
			.Method("GetKey", call => call.Return(api.String(Type(call).Key)))
			// The production script's (ministers.md, section 7).
			.Method("GetIndex", call => call.Return(IndexOf(api, Type(call))))
			.Method("IsRegiment", call => call.Return(Type(call).Branch == EUnitBranch.Land))
			.Method("IsCag", call => call.Return(Type(call).Flag("is_cag")))
			.Method("IsValid", call =>
			{
				Type(call);
				return call.Return(true);
			});
	}

	public static CLuaClass DefineSubUnitDataBase(CGameApi api)
	{
		return new CLuaClass("CSubUnitDataBase")
			.Static("GetSubUnit", call => api.State.SubUnitTypes.TryGetValue(call.String(1), out CSubUnitType? type)
				? call.Return(new CLuaObject(api.SubUnit, type))
				: call.Return(null))
			// Every type, in the order their files were read.
			.Static("GetSubUnitList", call =>
			{
				var types = new List<object?>();
				foreach (CSubUnitType type in api.State.SubUnitTypes.Values)
				{
					types.Add(new CLuaObject(api.SubUnit, type));
				}
				return call.ReturnIterator(types);
			});
	}

	/// <summary>A type's index: its place among the types in the order their files were read (ours: the original's loader order was not read).</summary>
	public static int IndexOf(CGameApi api, CSubUnitType type)
	{
		int index = 0;
		foreach (CSubUnitType each in api.State.SubUnitTypes.Values)
		{
			if (ReferenceEquals(each, type))
			{
				return index;
			}
			index++;
		}
		return -1;
	}

	/// <summary>Counts by type as the original's array of them: one entry a type, by its index.</summary>
	public static CLuaArray ByTypeIndex(CGameApi api, Dictionary<CSubUnitType, int> counts)
	{
		var values = new List<object?>();
		foreach (CSubUnitType type in api.State.SubUnitTypes.Values)
		{
			values.Add(counts.GetValueOrDefault(type));
		}
		return new CLuaArray(values);
	}

	private static CLuaLicenceAction Of(CLuaCall call) => call.Object<CLuaLicenceAction>(1, ActionName);

	private static CSubUnitType Type(CLuaCall call) => call.Object<CSubUnitType>(1, SubUnitName);
}
