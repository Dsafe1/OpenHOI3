using System;
using System.IO;
using System.Text;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Rules;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// What the global <c>OpenHOI3</c> says of the rules, against <c>docs/enhanced.md</c>, <i>The shape</i>:
/// <c>Enhanced</c> false and no fix in a faithful game, true with each fix that is on in an enhanced one.
/// </summary>
public sealed class LuaRulesTests
{
	private static CLuaState Open(CRules rules)
	{
		var lua = new CLuaState();
		lua.OpenLibraries("base", "package", "table", "string", "math");
		CLuaFiles.Install(lua, Path.GetTempPath);
		CLuaRules.Install(lua, rules);
		return lua;
	}

	private static object? Ask(CLuaState lua, string expression)
	{
		lua.Run(Encoding.Latin1.GetBytes("function __ask() return " + expression + " end"), "=test");
		return lua.Call("__ask", 1)[0];
	}

	[Fact]
	public void AFaithfulGameSaysSoAndHasNoFix()
	{
		using CLuaState lua = Open(new CRules());
		Assert.Equal(false, Ask(lua, "OpenHOI3.Enhanced"));
		Assert.Equal(true, Ask(lua, "next(OpenHOI3.Fixes) == nil"));
		Assert.Equal(true, Ask(lua, "type(OpenHOI3.Files) == 'table'"));
	}

	[Fact]
	public void AnEnhancedGameNamesTheFixesThatAreOn()
	{
		using CLuaState all = Open(new CRules(Array.Empty<EFix>()));
		Assert.Equal(true, Ask(all, "OpenHOI3.Enhanced"));
		Assert.Equal(true, Ask(all, "OpenHOI3.Fixes.minister_tech_decay"));

		using CLuaState without = Open(new CRules(new[] { EFix.MinisterTechDecay }));
		Assert.Equal(true, Ask(without, "OpenHOI3.Enhanced"));
		Assert.Equal(true, Ask(without, "OpenHOI3.Fixes.minister_tech_decay == nil"));
	}
}
