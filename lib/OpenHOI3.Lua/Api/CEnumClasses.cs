using System.Collections.Generic;
using OpenHOI3.Lua.Native;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The globals that are nothing yet but the original's numbers: <c>SpyMission</c>, read as the base
/// game's <c>autoexec.lua</c> loads, and the enums of classes not otherwise built. The values are the
/// original's registration (<c>reversing/findings/FINDINGS-luaapi.md</c>).
/// </summary>
public static class CEnumClasses
{
	public static IEnumerable<CLuaClass> Define()
	{
		yield return new CLuaClass("SpyMission")
			.Constant("SPYMISSION_COUNTER_ESPIONAGE", 0)
			.Constant("SPYMISSION_MILITARY", 1)
			.Constant("SPYMISSION_TECH", 2)
			.Constant("SPYMISSION_BOOST_RULING_PARTY", 3)
			.Constant("SPYMISSION_BOOST_OUR_PARTY", 4)
			.Constant("SPYMISSION_LOWER_NATIONAL_UNITY", 5)
			.Constant("SPYMISSION_INCREASE_THREAT", 6)
			.Constant("SPYMISSION_RAISE_NATIONAL_UNITY", 7)
			.Constant("SPYMISSION_COVERT_OPS", 8)
			.Constant("SPYMISSION_MAX", 9);
		yield return new CLuaClass("CDiplomaticAction")
			.Constant("PROPOSE", 0)
			.Constant("DECLINE", 1)
			.Constant("ACCEPT", 2);
		// CDistributionSetting's constants are CDistributionSettingClass's, with its methods.
		yield return new CLuaClass("CRule")
			.Constant("_RULE_NONE_", 0)
			.Constant("_RULE_LIMITED_WAR_", 1)
			.Constant("_RULE_ALLIANCE_GUARANTEE_", 2)
			.Constant("_RULE_FREE_RESOURCE_GIFTS_", 3);
	}
}
