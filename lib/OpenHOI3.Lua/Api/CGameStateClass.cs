using System;
using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The two globals with nothing but static functions: <c>CCurrentGameState</c> - a faction by name,
/// the factions, a province, the date, whether a person plays a country - and
/// <c>CCountryDataBase.GetTag</c>. See <c>docs/mechanics/lua.md</c>, section 4.
/// </summary>
public static class CGameStateClass
{
	public static CLuaClass DefineGameState(CGameApi api)
	{
		return new CLuaClass("CCurrentGameState")
			.Static("GetFaction", call =>
			{
				string name = call.String(1);
				foreach (CIdeologyGroup group in api.State.IdeologyGroups)
				{
					if (string.Equals(group.Faction, name, StringComparison.Ordinal) && name.Length > 0)
					{
						return call.Return(api.FactionNamed(name));
					}
				}
				return call.Return(api.FactionNamed(""));
			})
			.Static("GetFactions", call =>
			{
				var factions = new List<object?>();
				foreach (CIdeologyGroup group in api.State.IdeologyGroups)
				{
					if (group.Faction.Length > 0)
					{
						factions.Add(api.FactionNamed(group.Faction));
					}
				}
				return call.ReturnIterator(factions);
			})
			.Static("GetProvince", call => call.Return(new CLuaObject(api.Province, api.State.Province(call.Integer(1)))))
			.Static("GetCurrentDate", call => call.Return(new CLuaObject(api.Date, api.State.Date)))
			.Static("IsPlayer", call => call.Return(api.State.Played.Contains(call.Object<CCountryTag>(1, "CCountryTag"))));
	}

	public static CLuaClass DefineCountryDataBase(CGameApi api)
	{
		return new CLuaClass("CCountryDataBase")
			.Static("GetTag", call =>
			{
				var tag = new CCountryTag(call.String(1));
				return call.Return(api.Tag(api.State.Country(tag) is null ? CGameApi.NullTag : tag));
			});
	}
}
