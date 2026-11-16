using System;
using System.IO;
using System.Text;
using OpenHOI3.Lua.Native;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// The global <c>OpenHOI3</c> and its <c>Files</c>, against <c>docs/mechanics/mods.md</c>, section 6: the engine's mark,
/// a folder's files without its subfolders, nil for no folder, a relative path taken against the working folder, and a
/// folder listed once and kept.
/// </summary>
public sealed class LuaFilesTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "ohoi-luafiles-" + Guid.NewGuid().ToString("N"));

	public LuaFilesTests()
	{
		string technologies = Path.Combine(_root, "tfh", "mod", "Some Mod", "technologies");
		Directory.CreateDirectory(Path.Combine(technologies, "subfolder"));
		File.WriteAllText(Path.Combine(technologies, "armour.txt"), "");
		File.WriteAllText(Path.Combine(technologies, "infantry.txt"), "");
		Directory.CreateDirectory(Path.Combine(_root, "empty"));
	}

	public void Dispose() => Directory.Delete(_root, recursive: true);

	private CLuaState Open()
	{
		var lua = new CLuaState();
		lua.OpenLibraries("base", "package", "table", "string", "math");
		CLuaFiles.Install(lua, () => _root);
		return lua;
	}

	private static object? Ask(CLuaState lua, string expression)
	{
		lua.Run(Encoding.Latin1.GetBytes("function __ask() return " + expression + " end"), "=test");
		return lua.Call("__ask", 1)[0];
	}

	[Fact]
	public void The_global_marks_the_engine()
	{
		using CLuaState lua = Open();
		Assert.Equal("table", Ask(lua, "type(OpenHOI3)"));
		Assert.Equal("table", Ask(lua, "type(OpenHOI3.Files)"));
		Assert.Null(Ask(lua, "__ohoi_listfiles"));
	}

	[Fact]
	public void A_folder_gives_its_files_and_not_its_subfolders()
	{
		using CLuaState lua = Open();
		const string Path = "OpenHOI3.Files['tfh\\\\mod\\\\Some Mod\\\\technologies']";
		Assert.Equal(2.0, Ask(lua, "#" + Path));
		Assert.Equal("armour.txt,infantry.txt", Ask(lua, "table.concat(" + Path + ", ',')"));
	}

	[Fact]
	public void No_folder_is_nil_and_an_empty_one_an_empty_table()
	{
		using CLuaState lua = Open();
		Assert.Equal(true, Ask(lua, "OpenHOI3.Files['no such folder'] == nil"));
		Assert.Equal(0.0, Ask(lua, "#OpenHOI3.Files['empty']"));
		Assert.Equal(true, Ask(lua, "OpenHOI3.Files[3] == nil"));
	}

	[Fact]
	public void A_folder_is_listed_once_and_kept()
	{
		using CLuaState lua = Open();
		Assert.Equal(true, Ask(lua, "OpenHOI3.Files['empty'] == OpenHOI3.Files['empty']"));
		File.WriteAllText(System.IO.Path.Combine(_root, "empty", "later.txt"), "");
		Assert.Equal(0.0, Ask(lua, "#OpenHOI3.Files['empty']"));
	}

	[Fact]
	public void An_absolute_path_is_taken_as_it_is()
	{
		using CLuaState lua = Open();
		string folder = System.IO.Path.Combine(_root, "tfh", "mod", "Some Mod", "technologies").Replace("\\", "\\\\");
		Assert.Equal(2.0, Ask(lua, "#OpenHOI3.Files['" + folder + "']"));
	}
}
