using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The methods of <c>CCountry</c> that BlackICE's scripts reach and the base game's do not: its variables, its units, its
/// shares, its free spies, its allies and its provinces, whether it is a subject, and its cabinet. See
/// <c>docs/mechanics/lua.md</c>, sections 4.4 to 4.6, and <c>reversing/findings/FINDINGS-luaapi.md</c>, section 11.
/// </summary>
public static class CCountryHoldingsMethods
{
	private const string Name = "CCountry";

	public static CLuaClass Add(CLuaClass luaClass, CGameApi api)
	{
		return luaClass
			.Method("GetVariables", call => call.Return(new CLuaObject(api.Variables, Of(call).Variables)))
			.Method("GetUnits", call => call.Return(new CLuaObject(api.UnitList, new CLuaUnitList(Of(call)))))
			.Method("GetProductionDistributionAt", call =>
				call.Return(new CLuaObject(api.DistributionSetting, CDistributionSettingClass.At(call, Of(call), leadership: false))))
			.Method("GetLeadershipDistributionAt", call =>
				call.Return(new CLuaObject(api.DistributionSetting, CDistributionSettingClass.At(call, Of(call), leadership: true))))
			// The spies bought and not placed, whole, cut toward nought.
			.Method("GetNumberOfFreeSpies", call => call.Return((int)(Of(call).FreeSpies.Raw / CFixedPoint.Scale)))
			.Method("GetNumOfAllies", call => call.Return(CCountryLists.Allies(api.State, Of(call)).Count))
			// The numbers of the provinces it owns, in the provinces' order.
			.Method("GetOwnedProvinces", call =>
			{
				CCountryTag tag = Of(call).Tag;
				var owned = new List<object?>();
				foreach (CProvince province in api.State.Provinces)
				{
					if (province.Owner == tag)
					{
						owned.Add(province.Id);
					}
				}
				return call.ReturnIterator(owned);
			})
			// The same byte as IsPuppet: it has an overlord.
			.Method("IsSubject", call => call.Return(Of(call).Overlord is not null))
			// Who holds each post, by the post's number: the null post's null minister first, then each post's holder or the
			// null minister where nobody holds it.
			.Method("GetMinisters", call =>
			{
				CCountry country = Of(call);
				var ministers = new List<object?> { new CLuaObject(api.Minister, CMinisterClass.NullMinister) };
				foreach (Sim.Politics.CGovernmentPosition position in api.State.GovernmentPositions)
				{
					ministers.Add(new CLuaObject(api.Minister, CMinisterClass.HolderOf(country, position)));
				}
				return call.ReturnIterator(ministers);
			});
	}

	private static CCountry Of(CLuaCall call) => call.Object<CCountry>(1, Name);
}
