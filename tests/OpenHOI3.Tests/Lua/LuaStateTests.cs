using System;
using System.Text;
using OpenHOI3.Lua.Native;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// The interpreter as built from <c>native/lua</c> and the way in and out of it, against
/// <c>docs/mechanics/lua.md</c>: the original's Lua 5.1.4, its rounding, its numbers, and errors and
/// objects crossing without a <c>longjmp</c> through .NET.
/// </summary>
public class LuaStateTests
{
	private sealed class Thing
	{
		public required string Name { get; init; }
	}

	private static CLuaState Open()
	{
		var lua = new CLuaState();
		lua.OpenLibraries("base", "package", "table", "string", "math");
		return lua;
	}

	private static void Run(CLuaState lua, string code) => lua.Run(Encoding.Latin1.GetBytes(code), "=test");

	private static object? Ask(CLuaState lua, string expression)
	{
		Run(lua, "function __ask() return " + expression + " end");
		return lua.Call("__ask", 1)[0];
	}

	[Fact]
	public void It_is_Lua_5_1()
	{
		using CLuaState lua = Open();
		Assert.Equal("Lua 5.1", Ask(lua, "_VERSION"));
		Assert.Equal(4.0, Ask(lua, "table.getn({1, 2, 3, 4})"));
		Assert.Equal(1.0, Ask(lua, "math.mod(7, 3)"));
	}

	[Fact]
	public void A_number_turns_into_an_integer_to_the_nearest_with_ties_to_even()
	{
		// The original's 32-bit build converts with the x87's fistp; ours rounds the same way.
		// string.rep reads its count through lua_tointeger, which is that conversion.
		using CLuaState lua = Open();
		Assert.Equal(2.0, Ask(lua, "#string.rep('x', 2.5)"));
		Assert.Equal(4.0, Ask(lua, "#string.rep('x', 3.5)"));
		Assert.Equal(3.0, Ask(lua, "#string.rep('x', 2.6)"));
	}

	[Fact]
	public void String_format_d_cuts_a_number_as_C_does()
	{
		// lstrlib casts to the integer type itself, which truncates in the original too.
		using CLuaState lua = Open();
		Assert.Equal("3", Ask(lua, "string.format('%d', 3.5)"));
		Assert.Equal("-2", Ask(lua, "string.format('%d', -2.5)"));
	}

	[Fact]
	public void Numbers_are_doubles_written_with_fourteen_digits()
	{
		using CLuaState lua = Open();
		Assert.Equal("0.1", Ask(lua, "tostring(0.1)"));
		Assert.Equal("0.33333333333333", Ask(lua, "tostring(1/3)"));
		Assert.Equal("1e+15", Ask(lua, "tostring(10^15)"));
	}

	[Fact]
	public void A_global_function_is_called_with_arguments_and_answers()
	{
		using CLuaState lua = Open();
		Run(lua, "function add(a, b) return a + b, 'done' end");
		object?[] results = lua.Call("add", 2, 2, 3.5);
		Assert.Equal(5.5, results[0]);
		Assert.Equal("done", results[1]);
	}

	[Fact]
	public void A_Lua_error_reaches_CSharp_as_an_exception()
	{
		using CLuaState lua = Open();
		Run(lua, "function broken() error('it broke') end");
		var thrown = Assert.Throws<CLuaException>(() => lua.Call("broken", 0));
		Assert.Contains("it broke", thrown.Message);
		// The state is still usable after.
		Assert.Equal(2.0, Ask(lua, "1 + 1"));
	}

	[Fact]
	public void A_chunk_that_does_not_parse_is_an_exception()
	{
		using CLuaState lua = Open();
		Assert.Throws<CLuaException>(() => Run(lua, "function ("));
	}

	[Fact]
	public void An_error_raised_in_CSharp_is_a_Lua_error_a_script_can_catch()
	{
		using CLuaState lua = Open();
		lua.SetGlobal("fails", (CLuaFunction)(_ => throw new CLuaError("not today")));
		Assert.Equal("not today", Ask(lua, "select(2, pcall(fails))"));
		Assert.Equal(false, Ask(lua, "pcall(fails)"));
	}

	[Fact]
	public void Any_other_exception_in_CSharp_is_a_Lua_error_too()
	{
		using CLuaState lua = Open();
		lua.SetGlobal("crashes", (CLuaFunction)(_ => throw new InvalidOperationException("oops")));
		var thrown = Assert.Throws<CLuaException>(() => Run(lua, "crashes()"));
		Assert.Contains("oops", thrown.Message);
	}

	[Fact]
	public void A_method_is_called_on_an_object_with_a_colon()
	{
		using CLuaState lua = Open();
		var thing = new CLuaClass("CThing").Method("GetName", call => call.Return(call.Object<Thing>(1, "CThing").Name));
		lua.DefineClass(thing);
		lua.SetGlobal("it", new CLuaObject(thing, new Thing { Name = "Germany" }));
		Assert.Equal("Germany", Ask(lua, "it:GetName()"));
	}

	[Fact]
	public void One_object_pushed_twice_is_equal_to_itself_and_not_to_another()
	{
		using CLuaState lua = Open();
		var thingClass = new CLuaClass("CThing");
		lua.DefineClass(thingClass);
		var one = new Thing { Name = "one" };
		var other = new Thing { Name = "other" };
		lua.SetGlobal("a", new CLuaObject(thingClass, one));
		lua.SetGlobal("b", new CLuaObject(thingClass, one));
		lua.SetGlobal("c", new CLuaObject(thingClass, other));
		Assert.Equal(true, Ask(lua, "a == b"));
		Assert.Equal(false, Ask(lua, "a == c"));
		Assert.Equal(false, Ask(lua, "a == nil"));
		Assert.Same(one, lua.GetGlobal("a"));
	}

	[Fact]
	public void A_wrong_argument_is_named()
	{
		using CLuaState lua = Open();
		var thing = new CLuaClass("CThing").Method("GetName", call => call.Return(call.Object<Thing>(1, "CThing").Name));
		lua.DefineClass(thing);
		lua.SetGlobal("it", new CLuaObject(thing, new Thing { Name = "x" }));
		var thrown = Assert.Throws<CLuaException>(() => Run(lua, "it.GetName(5)"));
		Assert.Contains("bad argument #1 (CThing expected, got number)", thrown.Message);
	}

	[Fact]
	public void An_object_no_script_holds_is_forgotten()
	{
		using CLuaState lua = Open();
		var thingClass = new CLuaClass("CThing");
		lua.DefineClass(thingClass);
		for (int i = 0; i < 50; i++)
		{
			lua.SetGlobal("x", new CLuaObject(thingClass, new Thing { Name = "n" + i }));
		}
		Run(lua, "x = nil; collectgarbage('collect')");
		Assert.Equal(0, lua.HeldObjects);
	}
}
