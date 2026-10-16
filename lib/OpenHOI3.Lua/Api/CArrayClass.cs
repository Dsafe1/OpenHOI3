using OpenHOI3.Lua.Native;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The original's arrays: <c>GetAt(i)</c> and <c>SetAt(i, value)</c> from nought, and <c>GetSize()</c>. The original
/// checks no bounds; here an index out of range is a Lua error (ours), which no script of the base game makes. See
/// <c>reversing/findings/FINDINGS-ministers.md</c> C.2.
/// </summary>
public static class CArrayClass
{
	public static CLuaClass Define(string name)
	{
		return new CLuaClass(name)
			.Method("GetSize", call => call.Return(Of(call, name).Values.Count))
			.Method("GetAt", call => call.Return(Of(call, name).Values[Index(call, name)]))
			.Method("SetAt", call =>
			{
				CLuaArray array = Of(call, name);
				object? held = call.ObjectAt(3, out CLuaClass? luaClass);
				array.Values[Index(call, name)] = held is not null && luaClass is not null
					? new CLuaObject(luaClass, held)
					: call.Value(3);
				return 0;
			});
	}

	private static int Index(CLuaCall call, string name)
	{
		int index = call.Integer(2);
		if (index < 0 || index >= Of(call, name).Values.Count)
		{
			throw new CLuaError($"bad argument #2 to '{name}' (index {index} out of range)");
		}
		return index;
	}

	private static CLuaArray Of(CLuaCall call, string name) => call.Object<CLuaArray>(1, name);
}
