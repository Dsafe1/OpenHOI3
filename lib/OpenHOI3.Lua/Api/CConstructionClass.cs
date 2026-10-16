using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A line of the build queue as the production script reads it: <c>CConstruction</c> - whether it is military, what one
/// of it costs a day and how many are left of its run - and the same line as <c>CMilitaryConstruction</c>, classed by its
/// first brigade alone, with its brigades as <c>CBrigadeConstructionDefinition</c>. See
/// <c>docs/mechanics/ministers.md</c>, section 7, and <c>reversing/findings/FINDINGS-ministers.md</c> C.4.
/// </summary>
public static class CConstructionClass
{
	private const string Name = "CConstruction";
	private const string MilitaryName = "CMilitaryConstruction";
	private const string BrigadeName = "CBrigadeConstructionDefinition";

	public static CLuaClass DefineConstruction(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("IsMilitary", call => call.Return(Of(call, Name).IsUnit))
			.Method("GetMilitary", call =>
			{
				CConstruction line = Of(call, Name);
				return call.Return(line.IsUnit ? new CLuaObject(api.MilitaryConstruction, line) : null);
			})
			// One of the line a day, not times its run.
			.Method("GetCost", call => call.Return(api.Fixed(Of(call, Name).Cost)))
			.Method("GetSize", call => call.Return(Of(call, Name).Size));
	}

	public static CLuaClass DefineMilitary(CGameApi api)
	{
		return new CLuaClass(MilitaryName)
			.Method("IsLand", call => call.Return(FirstBranch(call) == EUnitBranch.Land))
			.Method("IsAir", call => call.Return(FirstBranch(call) == EUnitBranch.Air))
			.Method("IsNaval", call => call.Return(FirstBranch(call) == EUnitBranch.Naval))
			.Method("GetBrigades", call =>
			{
				var brigades = new List<object?>();
				foreach (CBrigadeOrder order in Of(call, MilitaryName).Brigades)
				{
					brigades.Add(new CLuaObject(api.BrigadeConstruction, order));
				}
				return call.ReturnIterator(brigades);
			});
	}

	public static CLuaClass DefineBrigade(CGameApi api)
	{
		return new CLuaClass(BrigadeName)
			.Method("GetType", call => call.Return(new CLuaObject(api.SubUnit, call.Object<CBrigadeOrder>(1, BrigadeName).Type)));
	}

	/// <summary>The branch of a line's first brigade, or none for a line of none.</summary>
	private static EUnitBranch? FirstBranch(CLuaCall call)
	{
		CConstruction line = Of(call, MilitaryName);
		return line.Brigades.Count > 0 ? line.Brigades[0].Type.Branch : null;
	}

	private static CConstruction Of(CLuaCall call, string name) => call.Object<CConstruction>(1, name);
}
