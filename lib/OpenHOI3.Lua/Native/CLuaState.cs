using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// One Lua 5.1.4 state: the scripts' globals, the classes their objects belong to, and the only way
/// in - <see cref="Run"/> and <see cref="Call"/>, each a protected call. Strings cross as Latin-1, one
/// character a byte, as everything read from an install does here. See <c>docs/mechanics/lua.md</c>.
/// </summary>
public sealed unsafe class CLuaState : IDisposable
{
	private const string ClassKeyPrefix = "ohoi.class.";
	private const string MethodsKeyPrefix = "ohoi.methods.";
	private const string OperatorKeyPrefix = "ohoi.operator.";

	/// <summary>
	/// The operators every class's objects answer through one function each, as luabind's instances
	/// share one metatable: Lua 5.1 calls <c>__eq</c>, <c>__lt</c> and <c>__le</c> between two
	/// userdata only where both carry the same function.
	/// </summary>
	private static readonly string[] SharedOperators = { "__eq", "__lt", "__le", "__add", "__sub", "__mul", "__div" };

	private readonly List<GCHandle> _handles = new();
	private readonly List<object?> _objects = new();
	private readonly Stack<int> _free = new();
	private readonly Dictionary<string, CLuaClass> _classes = new(StringComparer.Ordinal);
	private readonly CLuaFunction _collect;
	private IntPtr _state;

	public CLuaState()
	{
		CLuaLibrary.Ensure();
		_state = CLuaNative.luaL_newstate();
		if (_state == IntPtr.Zero)
		{
			throw new CLuaException("Lua could not make a state");
		}
		_objects.Add(null); // id 0 is never used

		_collect = Collect;
		foreach (string name in SharedOperators)
		{
			PushString(_state, OperatorKeyPrefix + name);
			PushFunction(_state, call => Dispatch(call, name));
			CLuaNative.lua_rawset(_state, CLuaNative.LUA_REGISTRYINDEX);
		}
	}

	/// <summary>The classes defined so far, by name.</summary>
	public IReadOnlyDictionary<string, CLuaClass> Classes => _classes;

	/// <summary>
	/// Opens these of the standard libraries - <c>base</c>, <c>package</c>, <c>table</c>, <c>io</c>,
	/// <c>os</c>, <c>string</c>, <c>math</c>, <c>debug</c> - each as <c>luaL_openlibs</c> would.
	/// </summary>
	public void OpenLibraries(params string[] names)
	{
		foreach (string name in names)
		{
			IntPtr open = NativeLibrary.GetExport(CLuaLibrary.Handle, "luaopen_" + name);
			CLuaNative.lua_pushcclosure(_state, open, 0);
			PushString(_state, name == "base" ? "" : name);
			Protected(1, 0);
		}
	}

	/// <summary>Loads a chunk and runs it. <paramref name="name"/> is what its errors are told by.</summary>
	public void Run(byte[] chunk, string name)
	{
		Load(chunk, name);
		Protected(0, 0);
	}

	/// <summary>Loads a chunk without running it, leaving the function on the stack.</summary>
	public void Load(byte[] chunk, string name)
	{
		if (!Load(_state, chunk, name))
		{
			throw new CLuaException(PopError(_state));
		}
	}

	/// <summary>Loads a chunk, leaving the function or the message on the stack; answers whether it loaded.</summary>
	internal bool Load(IntPtr L, byte[] chunk, string name)
	{
		byte[] chunkName = Encoding.Latin1.GetBytes(name + "\0");
		int status;
		fixed (byte* buffer = chunk)
		fixed (byte* namePointer = chunkName)
		{
			status = CLuaNative.luaL_loadbuffer(L, buffer, (nuint)chunk.Length, namePointer);
		}
		return status == CLuaNative.LUA_OK;
	}

	/// <summary>
	/// Calls the global function <paramref name="name"/> with these arguments and answers its first
	/// <paramref name="results"/> results. A global that is not a function is an error.
	/// </summary>
	public object?[] Call(string name, int results, params object?[] arguments)
	{
		PushString(_state, name);
		CLuaNative.lua_rawget(_state, CLuaNative.LUA_GLOBALSINDEX);
		if ((ELuaType)CLuaNative.lua_type(_state, -1) != ELuaType.Function)
		{
			CLuaNative.lua_settop(_state, -2);
			throw new CLuaException($"there is no function '{name}'");
		}
		foreach (object? argument in arguments)
		{
			Push(_state, argument);
		}
		Protected(arguments.Length, results);
		var values = new object?[results];
		for (int i = 0; i < results; i++)
		{
			values[i] = ToValue(_state, i - results);
		}
		CLuaNative.lua_settop(_state, -results - 1);
		return values;
	}

	/// <summary>Sets a global to a value - a function, an object, a number, a string, or nil.</summary>
	public void SetGlobal(string name, object? value)
	{
		PushString(_state, name);
		Push(_state, value);
		CLuaNative.lua_rawset(_state, CLuaNative.LUA_GLOBALSINDEX);
	}

	/// <summary>A global's value, read without its metatable, as <see cref="ToValue"/> turns it.</summary>
	public object? GetGlobal(string name)
	{
		PushString(_state, name);
		CLuaNative.lua_rawget(_state, CLuaNative.LUA_GLOBALSINDEX);
		object? value = ToValue(_state, -1);
		CLuaNative.lua_settop(_state, -2);
		return value;
	}

	/// <summary>
	/// Makes the metatable a class's objects carry - its methods under <c>__index</c>, with its fields
	/// where it has any; the shared operators and its own <c>__tostring</c>; a <c>__gc</c> that forgets
	/// the object once no script holds it - and the global of its name, with its static functions,
	/// its constants and its constructor.
	/// </summary>
	public void DefineClass(CLuaClass luaClass)
	{
		_classes[luaClass.Name] = luaClass;

		// The methods, kept in the registry so that an __index function can find them too.
		PushString(_state, MethodsKeyPrefix + luaClass.Name);
		CLuaNative.lua_createtable(_state, 0, luaClass.Methods.Count);
		foreach ((string method, CLuaFunction function) in luaClass.Methods)
		{
			PushString(_state, method);
			PushFunction(_state, function);
			CLuaNative.lua_rawset(_state, -3);
		}
		CLuaNative.lua_rawset(_state, CLuaNative.LUA_REGISTRYINDEX);

		PushString(_state, ClassKeyPrefix + luaClass.Name);
		CLuaNative.lua_createtable(_state, 0, 4 + SharedOperators.Length);

		PushString(_state, "__index");
		if (luaClass.Fields.Count == 0)
		{
			PushString(_state, MethodsKeyPrefix + luaClass.Name);
			CLuaNative.lua_rawget(_state, CLuaNative.LUA_REGISTRYINDEX);
		}
		else
		{
			PushFunction(_state, call => Index(call, luaClass));
		}
		CLuaNative.lua_rawset(_state, -3);

		foreach (string name in SharedOperators)
		{
			PushString(_state, name);
			PushString(_state, OperatorKeyPrefix + name);
			CLuaNative.lua_rawget(_state, CLuaNative.LUA_REGISTRYINDEX);
			CLuaNative.lua_rawset(_state, -3);
		}
		if (luaClass.Operators.TryGetValue("__tostring", out CLuaFunction? text))
		{
			PushString(_state, "__tostring");
			PushFunction(_state, text);
			CLuaNative.lua_rawset(_state, -3);
		}

		PushString(_state, "__gc");
		PushFunction(_state, _collect);
		CLuaNative.lua_rawset(_state, -3);

		PushString(_state, "__name");
		PushString(_state, luaClass.Name);
		CLuaNative.lua_rawset(_state, -3);

		CLuaNative.lua_rawset(_state, CLuaNative.LUA_REGISTRYINDEX);

		DefineGlobal(luaClass);
	}

	/// <summary>The class's global: a table of its static functions and constants, called to make one.</summary>
	private void DefineGlobal(CLuaClass luaClass)
	{
		PushString(_state, luaClass.Name);
		CLuaNative.lua_createtable(_state, 0, luaClass.Statics.Count + luaClass.Constants.Count);
		foreach ((string name, CLuaFunction function) in luaClass.Statics)
		{
			PushString(_state, name);
			PushFunction(_state, function);
			CLuaNative.lua_rawset(_state, -3);
		}
		foreach ((string name, int value) in luaClass.Constants)
		{
			PushString(_state, name);
			CLuaNative.lua_pushinteger(_state, value);
			CLuaNative.lua_rawset(_state, -3);
		}
		if (luaClass.Constructor is { } constructor)
		{
			CLuaNative.lua_createtable(_state, 0, 1);
			PushString(_state, "__call");
			PushFunction(_state, constructor);
			CLuaNative.lua_rawset(_state, -3);
			CLuaNative.lua_setmetatable(_state, -2);
		}
		CLuaNative.lua_rawset(_state, CLuaNative.LUA_GLOBALSINDEX);
	}

	/// <summary>
	/// An operator between two values, at least one an object of ours: the first operand's class's
	/// operator if it has one, else the second's, as luabind looks one up.
	/// </summary>
	private int Dispatch(CLuaCall call, string name)
	{
		for (int argument = 1; argument <= 2; argument++)
		{
			if (ToObject(call.Pointer, argument, out CLuaClass? luaClass) is not null && luaClass is not null
				&& luaClass.Operators.TryGetValue(name, out CLuaFunction? function))
			{
				return function(call);
			}
		}
		throw new CLuaError("No such operator defined");
	}

	/// <summary>The <c>__index</c> of a class with fields: a method of that name, else the field's value.</summary>
	private int Index(CLuaCall call, CLuaClass luaClass)
	{
		IntPtr L = call.Pointer;
		PushString(L, MethodsKeyPrefix + luaClass.Name);
		CLuaNative.lua_rawget(L, CLuaNative.LUA_REGISTRYINDEX);
		CLuaNative.lua_pushvalue(L, 2);
		CLuaNative.lua_rawget(L, -2);
		if ((ELuaType)CLuaNative.lua_type(L, -1) != ELuaType.Nil)
		{
			return 1;
		}
		CLuaNative.lua_settop(L, 2);
		if (call.TypeOf(2) == ELuaType.String && luaClass.Fields.TryGetValue(call.String(2), out CLuaFunction? field))
		{
			return field(call);
		}
		CLuaNative.lua_pushnil(L);
		return 1;
	}

	/// <summary>
	/// Pushes a function a generic <c>for</c> walks: the values in turn, then nil. A nil among them
	/// ends the walk there.
	/// </summary>
	internal void PushIterator(IntPtr L, IReadOnlyList<object?> values)
	{
		CLuaNative.lua_createtable(L, values.Count, 0);
		for (int i = 0; i < values.Count; i++)
		{
			Push(L, values[i]);
			CLuaNative.lua_rawseti(L, -2, i + 1);
		}
		CLuaNative.ohoi_pushiterator(L);
	}

	public void Dispose()
	{
		if (_state != IntPtr.Zero)
		{
			CLuaNative.lua_close(_state);
			_state = IntPtr.Zero;
		}
		foreach (GCHandle handle in _handles)
		{
			handle.Free();
		}
		_handles.Clear();
		_objects.Clear();
		_free.Clear();
	}

	/// <summary>How many objects scripts hold at the moment - for tests.</summary>
	public int HeldObjects => _objects.Count - 1 - _free.Count;

	internal IntPtr Handle => _state;

	internal void Push(IntPtr L, object? value)
	{
		switch (value)
		{
			case null:
				CLuaNative.lua_pushnil(L);
				break;
			case bool b:
				CLuaNative.lua_pushboolean(L, b ? 1 : 0);
				break;
			case int i:
				CLuaNative.lua_pushnumber(L, i);
				break;
			case double d:
				CLuaNative.lua_pushnumber(L, d);
				break;
			case float f:
				CLuaNative.lua_pushnumber(L, f);
				break;
			case string s:
				PushString(L, s);
				break;
			case CLuaFunction function:
				PushFunction(L, function);
				break;
			case CLuaObject luaObject:
				PushObject(L, luaObject);
				break;
			default:
				throw new ArgumentException($"a {value.GetType().Name} cannot be handed to Lua");
		}
	}

	/// <summary>
	/// A Lua value as C#: nil null, a boolean a bool, a number a double, a string a string, an object
	/// of ours the object; anything else the type's name.
	/// </summary>
	internal object? ToValue(IntPtr L, int index)
	{
		switch ((ELuaType)CLuaNative.lua_type(L, index))
		{
			case ELuaType.None:
			case ELuaType.Nil:
				return null;
			case ELuaType.Boolean:
				return CLuaNative.lua_toboolean(L, index) != 0;
			case ELuaType.Number:
				return CLuaNative.lua_tonumber(L, index);
			case ELuaType.String:
				return ToString(L, index);
			case ELuaType.Userdata:
				return ToObject(L, index, out _);
			default:
				return (ELuaType)CLuaNative.lua_type(L, index);
		}
	}

	/// <summary>The object of ours at that index, and its class; null where it is something else.</summary>
	internal object? ToObject(IntPtr L, int index, out CLuaClass? luaClass)
	{
		luaClass = null;
		if ((ELuaType)CLuaNative.lua_type(L, index) != ELuaType.Userdata || CLuaNative.lua_getmetatable(L, index) == 0)
		{
			return null;
		}
		PushString(L, "__name");
		CLuaNative.lua_rawget(L, -2);
		string? className = (ELuaType)CLuaNative.lua_type(L, -1) == ELuaType.String ? ToString(L, -1) : null;
		CLuaNative.lua_settop(L, -3);
		if (className is null || !_classes.TryGetValue(className, out luaClass))
		{
			return null;
		}
		int id = *(int*)CLuaNative.lua_touserdata(L, index);
		return id > 0 && id < _objects.Count ? _objects[id] : null;
	}

	internal static string ToString(IntPtr L, int index)
	{
		nuint length;
		byte* text = CLuaNative.lua_tolstring(L, index, &length);
		return text is null ? "" : Encoding.Latin1.GetString(text, (int)length);
	}

	internal static void PushString(IntPtr L, string text)
	{
		byte[] bytes = Encoding.Latin1.GetBytes(text);
		fixed (byte* pointer = bytes)
		{
			CLuaNative.lua_pushlstring(L, pointer, (nuint)bytes.Length);
		}
	}

	internal void PushFunction(IntPtr L, CLuaFunction function)
	{
		GCHandle handle = GCHandle.Alloc(new CLuaBinding(this, function));
		_handles.Add(handle);
		CLuaNative.ohoi_pushfunction(L, &Enter, GCHandle.ToIntPtr(handle));
	}

	/// <summary>
	/// A new userdata for the object every time, as luabind pushes one: the same object handed over
	/// twice is two values.
	/// </summary>
	private void PushObject(IntPtr L, CLuaObject luaObject)
	{
		if (!_classes.ContainsKey(luaObject.Class.Name))
		{
			throw new ArgumentException($"the class '{luaObject.Class.Name}' is not defined");
		}
		int id = _free.Count > 0 ? _free.Pop() : _objects.Count;
		if (id == _objects.Count)
		{
			_objects.Add(luaObject.Target);
		}
		else
		{
			_objects[id] = luaObject.Target;
		}

		*(int*)CLuaNative.lua_newuserdata(L, sizeof(int)) = id;
		PushString(L, ClassKeyPrefix + luaObject.Class.Name);
		CLuaNative.lua_rawget(L, CLuaNative.LUA_REGISTRYINDEX);
		CLuaNative.lua_setmetatable(L, -2);
	}

	/// <summary>The <c>__gc</c> of every class: the object's id is free again.</summary>
	private int Collect(CLuaCall call)
	{
		int id = *(int*)CLuaNative.lua_touserdata(call.Pointer, 1);
		if (id > 0 && id < _objects.Count && _objects[id] is not null)
		{
			_objects[id] = null;
			_free.Push(id);
		}
		return 0;
	}

	private void Protected(int arguments, int results)
	{
		if (CLuaNative.lua_pcall(_state, arguments, results, 0) != CLuaNative.LUA_OK)
		{
			throw new CLuaException(PopError(_state));
		}
	}

	private static string PopError(IntPtr L)
	{
		string message = (ELuaType)CLuaNative.lua_type(L, -1) == ELuaType.String
			? ToString(L, -1)
			: "an error that is not a string";
		CLuaNative.lua_settop(L, -2);
		return message;
	}

	/// <summary>Where every function written in C# is entered from Lua; never lets an exception out.</summary>
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	private static int Enter(IntPtr L, IntPtr context)
	{
		var binding = (CLuaBinding)GCHandle.FromIntPtr(context).Target!;
		try
		{
			return binding.Function(new CLuaCall(binding.State, L));
		}
		catch (CLuaError e)
		{
			PushString(L, e.Message);
			return -1;
		}
		catch (Exception e)
		{
			PushString(L, $"internal error in OpenHOI3: {e.GetType().Name}: {e.Message}");
			return -1;
		}
	}
}
