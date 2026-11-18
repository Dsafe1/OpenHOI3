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
	public void An_error_raised_in_CSharp_says_where_in_the_scripts_it_was_called_and_the_stack_under_it()
	{
		using CLuaState lua = Open();
		lua.SetGlobal("fails", (CLuaFunction)(_ => throw new CLuaError("bad argument #2 (CThing expected, got nil)")));
		lua.Run(Encoding.Latin1.GetBytes("function inner()\n  local x = 1\n  fails(x, nil)\nend\nfunction outer()\n  inner()\nend"),
			"@script/ai_test.lua");

		var thrown = Assert.Throws<CLuaException>(() => lua.Call("outer", 0));

		string[] lines = thrown.Message.Split('\n');
		Assert.Equal("script/ai_test.lua:3: bad argument #2 (CThing expected, got nil)", lines[0]);
		Assert.Equal("stack traceback:", lines[1]);
		Assert.Contains(lines, line => line == "\t[C]: in function 'fails'");
		Assert.Contains(lines, line => line == "\tscript/ai_test.lua:3: in function 'inner'");
		// Called from C#, the outermost function has no name: it is named by where it was defined, as Lua's own traceback does.
		Assert.Contains(lines, line => line == "\tscript/ai_test.lua:6: in function <script/ai_test.lua:5>");
		// A script's own pcall still sees the message alone, and the state is still usable after.
		Assert.Equal("script/ai_test.lua:3: bad argument #2 (CThing expected, got nil)",
			Ask(lua, "select(2, pcall(inner))"));
		Assert.Equal(2.0, Ask(lua, "1 + 1"));
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
	public void One_object_pushed_twice_is_two_values_and_a_class_with_no_equality_cannot_compare_them()
	{
		// luabind makes a new userdata at each push and answers == through one operator for every
		// class, which is an error for a class that registers none.
		using CLuaState lua = Open();
		var thingClass = new CLuaClass("CThing");
		lua.DefineClass(thingClass);
		var one = new Thing { Name = "one" };
		lua.SetGlobal("a", new CLuaObject(thingClass, one));
		lua.SetGlobal("b", new CLuaObject(thingClass, one));
		Assert.Equal(true, Ask(lua, "a == a"));
		Assert.Equal(false, Ask(lua, "rawequal(a, b)"));
		Assert.Equal(false, Ask(lua, "a == nil"));
		Assert.Equal(false, Ask(lua, "a == 1"));
		Assert.Contains("No such operator defined", Assert.Throws<CLuaException>(() => Ask(lua, "a == b")).Message);
		Assert.Same(one, lua.GetGlobal("a"));
	}

	[Fact]
	public void A_class_equality_answers_for_two_of_its_objects_and_across_classes_through_the_left_one()
	{
		using CLuaState lua = Open();
		var thingClass = new CLuaClass("CThing")
			.Operator("__eq", call => call.Return(call.Object<Thing>(1, "CThing").Name == call.Object<Thing>(2, "CThing").Name));
		var otherClass = new CLuaClass("COther");
		lua.DefineClass(thingClass);
		lua.DefineClass(otherClass);
		lua.SetGlobal("a", new CLuaObject(thingClass, new Thing { Name = "x" }));
		lua.SetGlobal("b", new CLuaObject(thingClass, new Thing { Name = "x" }));
		lua.SetGlobal("c", new CLuaObject(thingClass, new Thing { Name = "y" }));
		lua.SetGlobal("o", new CLuaObject(otherClass, new object()));
		Assert.Equal(true, Ask(lua, "a == b"));
		Assert.Equal(false, Ask(lua, "a == c"));
		Assert.Equal(true, Ask(lua, "a ~= c"));
		// The other class has no ==, so the thing's answers whichever side it is on - here with an error,
		// the other object not being a thing.
		Assert.Throws<CLuaException>(() => Ask(lua, "o == a"));
	}

	[Fact]
	public void A_class_is_a_global_with_its_static_functions_constants_and_constructor()
	{
		using CLuaState lua = Open();
		CLuaClass thingClass = new CLuaClass("CThing")
			.Method("GetName", call => call.Return(call.Object<Thing>(1, "CThing").Name))
			.Static("Twice", call => call.Return(call.Number(1) * 2))
			.Constant("_ONE_", 1);
		thingClass.Constructs(call => call.Return(new CLuaObject(thingClass, new Thing { Name = call.String(2) })));
		lua.DefineClass(thingClass);
		Assert.Equal(6.0, Ask(lua, "CThing.Twice(3)"));
		Assert.Equal(1.0, Ask(lua, "CThing._ONE_"));
		Assert.Equal("made", Ask(lua, "CThing('made'):GetName()"));
	}

	[Fact]
	public void A_field_is_read_with_a_dot_beside_the_methods()
	{
		using CLuaState lua = Open();
		var thingClass = new CLuaClass("CThing")
			.Method("GetName", call => call.Return(call.Object<Thing>(1, "CThing").Name))
			.Field("vName", call => call.Return(call.Object<Thing>(1, "CThing").Name + "!"));
		lua.DefineClass(thingClass);
		lua.SetGlobal("it", new CLuaObject(thingClass, new Thing { Name = "Italy" }));
		Assert.Equal("Italy", Ask(lua, "it:GetName()"));
		Assert.Equal("Italy!", Ask(lua, "it.vName"));
		Assert.Null(Ask(lua, "it.vNothing"));
	}

	[Fact]
	public void An_iterator_walks_its_values_in_order_and_stops_at_the_first_nil()
	{
		using CLuaState lua = Open();
		var values = new object?[] { "a", "b", null, "d" };
		lua.SetGlobal("walk", (CLuaFunction)(call => call.ReturnIterator(values)));
		Assert.Equal("ab", Ask(lua, "(function() local s = '' for v in walk() do s = s .. v end return s end)()"));
		lua.SetGlobal("none", (CLuaFunction)(call => call.ReturnIterator(System.Array.Empty<object?>())));
		Assert.Equal(0.0, Ask(lua, "(function() local n = 0 for v in none() do n = n + 1 end return n end)()"));
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
