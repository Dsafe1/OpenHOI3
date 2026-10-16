using System;
using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CTechnology</c>, <c>CTechnologyFolder</c> - held by its key, two equal where the keys are - and
/// <c>CTechnologyDataBase</c>, as the tech minister's script reaches them. See <c>docs/mechanics/ministers.md</c>,
/// section 7, and <c>reversing/findings/FINDINGS-ministers.md</c> A.6.
/// </summary>
public static class CTechnologyClass
{
	public const string Name = "CTechnology";
	private const string FolderName = "CTechnologyFolder";

	public static CLuaClass DefineTechnology(CGameApi api)
	{
		return new CLuaClass(Name)
			.Operator("__eq", CLuaEquality.Same<CTechnology>(Name))
			.Method("GetKey", call => call.Return(api.String(Of(call).Name)))
			.Method("GetFolder", call => call.Return(new CLuaObject(api.TechnologyFolder, new CLuaNamed(Of(call).Folder))))
			.Method("IsValid", call =>
			{
				Of(call);
				return call.Return(true);
			})
			.Method("GetIndex", call => call.Return(IndexOf(api, Of(call))));
	}

	public static CLuaClass DefineFolder(CGameApi api)
	{
		return new CLuaClass(FolderName)
			.Operator("__eq", CLuaEquality.By<CLuaNamed>(FolderName, (a, b) => string.Equals(a.Name, b.Name, StringComparison.Ordinal)))
			.Method("GetKey", call => call.Return(api.String(call.Object<CLuaNamed>(1, FolderName).Name)));
	}

	/// <summary>
	/// The database: every technology in the order its files were read - by file name, each in file order - which is the
	/// order the script's random picks are made over (ours: the original's null technology first is left out, the
	/// script passing it over); a technology by its exact key, or nil; and the latest year any is due.
	/// </summary>
	public static CLuaClass DefineDataBase(CGameApi api)
	{
		return new CLuaClass("CTechnologyDataBase")
			.Static("GetTechnologies", call =>
			{
				var technologies = new List<object?>();
				foreach (CTechnology technology in api.State.Technologies.Values)
				{
					technologies.Add(new CLuaObject(api.Technology, technology));
				}
				return call.ReturnIterator(technologies);
			})
			.Static("GetTechnology", call => api.State.Technologies.TryGetValue(call.String(1), out CTechnology? technology)
				&& string.Equals(technology.Name, call.String(1), StringComparison.Ordinal)
					? call.Return(new CLuaObject(api.Technology, technology))
					: call.Return(null))
			.Static("GetLatestTechYear", call => call.Return(LatestYear(api)));
	}

	/// <summary>The year a level of a technology is due: its start year to level 1, its first offset at 2, and then its additional offset a level.</summary>
	public static int Year(CTechnology technology, int level)
	{
		if (level <= 1)
		{
			return technology.StartYear;
		}
		if (level == 2)
		{
			return technology.FirstOffset;
		}
		return technology.FirstOffset + (level - 2) * technology.AdditionalOffset;
	}

	/// <summary>The end date's year, or the latest any technology's highest level is due, whichever is later.</summary>
	private static int LatestYear(CGameApi api)
	{
		int latest = api.State.EndDate.Year;
		foreach (CTechnology technology in api.State.Technologies.Values)
		{
			latest = Math.Max(latest, Year(technology, technology.HighestLevel));
		}
		return latest;
	}

	/// <summary>A technology's place in the database, from one - the original's null technology being nought.</summary>
	private static int IndexOf(CGameApi api, CTechnology technology)
	{
		int index = 1;
		foreach (CTechnology each in api.State.Technologies.Values)
		{
			if (ReferenceEquals(each, technology))
			{
				return index;
			}
			index++;
		}
		return 0;
	}

	public static CTechnology Of(CLuaCall call) => call.Object<CTechnology>(1, Name);
}
