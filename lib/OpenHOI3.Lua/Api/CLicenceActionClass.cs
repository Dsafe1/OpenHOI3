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
	private const string SubUnitName = "CSubUnitDefinition";

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
			.Method("GetKey", call => call.Return(api.String(Type(call).Key)));
	}

	public static CLuaClass DefineSubUnitDataBase(CGameApi api)
	{
		return new CLuaClass("CSubUnitDataBase")
			.Static("GetSubUnit", call => api.State.SubUnitTypes.TryGetValue(call.String(1), out CSubUnitType? type)
				? call.Return(new CLuaObject(api.SubUnit, type))
				: call.Return(null));
	}

	private static CLuaLicenceAction Of(CLuaCall call) => call.Object<CLuaLicenceAction>(1, ActionName);

	private static CSubUnitType Type(CLuaCall call) => call.Object<CSubUnitType>(1, SubUnitName);
}
