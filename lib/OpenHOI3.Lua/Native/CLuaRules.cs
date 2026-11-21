using System.Linq;
using System.Text;
using OpenHOI3.Sim.Rules;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// What the global <c>OpenHOI3</c> (<see cref="CLuaFiles"/>) says of the rules: <c>OpenHOI3.Enhanced</c>, true where
/// the folders make an enhanced game and false where not, and <c>OpenHOI3.Fixes</c>, a table with <c>true</c> under the
/// name of each fix that is on - empty in a faithful game. So a mod's script made for both engines asks
/// <c>OpenHOI3 ~= nil and OpenHOI3.Enhanced</c> before it leans on the enhanced mode, and
/// <c>OpenHOI3.Fixes.minister_tech_decay</c> before it does by script what a fix already does.
/// <c>docs/enhanced.md</c>, <i>The shape</i>.
/// </summary>
public static class CLuaRules
{
	/// <summary>Sets the two fields; <see cref="CLuaFiles.Install"/> comes first, making the table.</summary>
	public static void Install(CLuaState lua, CRules rules)
	{
		// A fix's name is lower case letters and underscores, so it stands in the chunk as it is.
		string fixes = string.Join(", ", rules.Fixes.Select(fix => CFixes.Name(fix) + " = true"));
		lua.Run(Encoding.Latin1.GetBytes(
			$"OpenHOI3.Enhanced = {(rules.Enhanced ? "true" : "false")} OpenHOI3.Fixes = {{ {fixes} }}"), "=OpenHOI3");
	}
}
