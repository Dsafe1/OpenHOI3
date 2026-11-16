using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A country's variables as a script reaches them - <c>country:GetVariables()</c>, a <c>CVariables</c>, handed over as the
/// country's own table of them - with <c>GetVariable(name)</c>, and <c>CSetVariableCommand(tag, name, value)</c>, the
/// command that sets one, posted as the others are. The same variables script's <c>set_variable</c> and
/// <c>check_variable</c> keep (<c>docs/mechanics/effects.md</c>, section 3). See <c>docs/mechanics/lua.md</c>, section 4.4.
/// </summary>
public static class CVariablesClass
{
	public const string Name = "CVariables";
	private const string CommandName = "CSetVariableCommand";

	/// <summary>
	/// <c>GetVariable(name)</c>: the variable's value, nought where there is none. The original's makes a missing one, at
	/// nought, as <c>check_variable</c> does; a variable at nought is never saved and reads as one that is not there, so
	/// nothing tells the two apart, and this one changes nothing - which the trading's scripts, run side by side, need.
	/// </summary>
	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetVariable", call =>
			{
				Dictionary<string, CFixedPoint> variables = call.Object<Dictionary<string, CFixedPoint>>(1, Name);
				string name = call.Object<CLuaText>(2, CStringClass.Name).Value;
				return call.Return(api.Fixed(variables.GetValueOrDefault(name)));
			});
	}

	/// <summary><c>CSetVariableCommand(tag, name, value)</c>: exactly those three, a tag, a <c>CString</c> and a <c>CFixedPoint</c>.</summary>
	public static CLuaClass DefineCommand(CGameApi api)
	{
		var luaClass = new CLuaClass(CommandName);
		return luaClass.Constructs(call =>
		{
			if (call.ArgumentCount != 4)
			{
				throw new CLuaError("No matching overload found, candidates: void __init(luabind::argument const&,CCountryTag,"
					+ "CString const&,CFixedPoint)");
			}
			CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
			string name = call.Object<CLuaText>(3, CStringClass.Name).Value;
			CFixedPoint value = call.Object<CFixedPoint>(4, "CFixedPoint");
			return call.Return(new CLuaObject(luaClass, new CSetVariableCommand(tag, name, value)));
		});
	}
}
