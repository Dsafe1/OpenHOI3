using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// Finds the interpreter <see cref="CLuaInterpreter"/> chooses - LuaJIT built from <c>native/luajit</c>, or Lua 5.1.4
/// from <c>native/lua</c> - by a name nothing else on a machine has, and beside this assembly first: inside Godot the
/// assembly's folder is not searched, and a <c>lua51.dll</c> of another build on the PATH (Lua for Windows puts a 32-bit
/// one there) would load in its place. Every import names <see cref="Name"/>, whichever file answers it.
/// </summary>
internal static class CLuaLibrary
{
	public const string Name = "openhoi3_lua";

	private static readonly object Lock = new();
	private static IntPtr _handle;

	static CLuaLibrary()
	{
		NativeLibrary.SetDllImportResolver(typeof(CLuaLibrary).Assembly, Resolve);
	}

	/// <summary>Makes sure the resolver is in place; call before the first call into the library.</summary>
	public static void Ensure()
	{
	}

	public static IntPtr Handle
	{
		get
		{
			lock (Lock)
			{
				if (_handle == IntPtr.Zero)
				{
					string file = CLuaInterpreter.FileName;
					CLuaInterpreter.MarkLoaded();
					// Beside the assembly; inside Godot an assembly has no location, and the folder the
					// runtime was started from is the build's.
					foreach (string folder in new[] { Path.GetDirectoryName(typeof(CLuaLibrary).Assembly.Location) ?? "", AppContext.BaseDirectory })
					{
						string beside = Path.Combine(folder, file + ".dll");
						if (folder.Length > 0 && File.Exists(beside))
						{
							_handle = NativeLibrary.Load(beside);
							return _handle;
						}
					}
					_handle = NativeLibrary.Load(file, typeof(CLuaLibrary).Assembly, null);
				}
				return _handle;
			}
		}
	}

	private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
	{
		return name == Name ? Handle : IntPtr.Zero;
	}
}
