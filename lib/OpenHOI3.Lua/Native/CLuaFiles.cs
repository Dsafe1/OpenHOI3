using System;
using System.IO;
using System.Text;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// The global <c>OpenHOI3</c>, which the original does not have: a script that finds it knows it runs under this engine,
/// and its <c>Files</c> is the folder structure the original's scripts reach through a native module this engine cannot
/// load. <c>OpenHOI3.Files[path]</c> is the names of the files in a folder - its subfolders left out, in the order the
/// file system lists them, as LuaFileSystem's <c>lfs.dir</c> gives them on Windows - or nil where there is no such folder.
/// A relative path is taken against the working folder, as the original's are; a folder is listed the first time it is
/// asked for and kept. <c>docs/mechanics/mods.md</c>, section 6.
/// </summary>
public static class CLuaFiles
{
	/// <summary>What separates two names in the one string the listing crosses into Lua as; no file name holds it.</summary>
	private const char Separator = '\n';

	private const string Maker =
		"local list = __ohoi_listfiles __ohoi_listfiles = nil " +
		"OpenHOI3 = { Files = setmetatable({}, { __index = function(files, path) " +
		"if type(path) ~= 'string' then return nil end " +
		"local joined = list(path) " +
		"if joined == nil then return nil end " +
		"local names = {} " +
		"for name in string.gmatch(joined, '[^\\n]+') do names[#names + 1] = name end " +
		"rawset(files, path, names) " +
		"return names end }) }";

	/// <summary>
	/// Puts <c>OpenHOI3</c> in the state. <paramref name="workingFolder"/> answers the folder a relative path is taken
	/// against when a folder is first asked for - the process's working folder in a game, a fixture's in a test.
	/// </summary>
	public static void Install(CLuaState lua, Func<string> workingFolder)
	{
		lua.SetGlobal("__ohoi_listfiles", (CLuaFunction)(call => List(call, workingFolder)));
		lua.Run(Encoding.Latin1.GetBytes(Maker), "=OpenHOI3");
	}

	/// <summary>The files of a folder joined by the separator, or nil where it is not a folder or cannot be read.</summary>
	private static int List(CLuaCall call, Func<string> workingFolder)
	{
		string path = call.String(1);
		string folder = Path.Combine(workingFolder(), path.Replace('\\', Path.DirectorySeparatorChar));
		if (!Directory.Exists(folder))
		{
			return call.Return(null);
		}
		try
		{
			var names = new StringBuilder();
			// The file system's own order, unsorted, as lfs.dir walks it.
			foreach (string file in Directory.EnumerateFiles(folder))
			{
				if (names.Length > 0)
				{
					names.Append(Separator);
				}
				names.Append(Path.GetFileName(file));
			}
			return call.Return(names.ToString());
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return call.Return(null);
		}
	}
}
