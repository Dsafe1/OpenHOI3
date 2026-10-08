using System;
using System.IO;
using System.Linq;
using OpenHOI3.Content;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The expansion layering is the setting where a mistake is invisible: reading the base
/// install alone loads and renders perfectly, it is simply the wrong expansion. These
/// cases pin the precedence and ordering rules down against a fixture install.
/// </summary>
public class DirectoryLayeringTests : IDisposable
{
	private readonly string _root;

	public DirectoryLayeringTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_root, "interface"));
		Directory.CreateDirectory(Path.Combine(_root, "tfh", "interface"));
		Directory.CreateDirectory(Path.Combine(_root, "gfx", "interface"));
		Directory.CreateDirectory(Path.Combine(_root, "tfh", "gfx", "interface"));
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch (IOException)
		{
			// A locked file must not fail the test that already passed.
		}
		GC.SuppressFinalize(this);
	}

	private void Write(string relative, string content)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
	}

	private CDirectorySettings Settings => CDirectorySettings.At(_root);

	[Fact]
	public void ExpansionCopyWinsOverBase()
	{
		Write("interface/core.gfx", "base");
		Write("tfh/interface/core.gfx", "expansion");

		Assert.Equal("expansion", File.ReadAllText(Settings.Resolve("interface/core.gfx")));
	}

	[Fact]
	public void FallsBackToBaseWhenExpansionLacksTheFile()
	{
		Write("interface/menubar.gui", "base only");

		Assert.Equal("base only", File.ReadAllText(Settings.Resolve("interface/menubar.gui")));
	}

	[Fact]
	public void ResolvesBackslashPathsFromScriptFiles()
	{
		Write("gfx/interface/frontend_button.dds", "art");

		// Script files write paths as "gfx\\interface\\frontend_button.dds".
		string resolved = Settings.Resolve("gfx\\interface\\frontend_button.dds");
		Assert.True(File.Exists(resolved));
	}

	[Fact]
	public void TextureLookupRetriesOtherExtensions()
	{
		// The front end asks for frontend_tile.tga when only the .dds shipped.
		Write("gfx/interface/frontend_tile.dds", "art");

		string? found = Settings.ResolveTexture("gfx\\interface\\frontend_tile.tga");
		Assert.NotNull(found);
		Assert.EndsWith(".dds", found);
	}

	[Fact]
	public void TextureLookupPrefersTheExpansionCopy()
	{
		Write("gfx/interface/frontend_main_bg.dds", "base art");
		Write("tfh/gfx/interface/frontend_main_bg.dds", "expansion art");

		string? found = Settings.ResolveTexture("gfx\\interface\\frontend_main_bg.dds");
		Assert.Equal("expansion art", File.ReadAllText(found!));
	}

	[Fact]
	public void ATextureNamedTgaIsTheDdsWhereBothExist()
	{
		// select_ship_button ships as both, and they are not the same picture: the
		// original looks for the .dds first and draws that.
		Write("gfx/interface/select_ship_button.tga", "the older picture");
		Write("gfx/interface/select_ship_button.dds", "the picture drawn");

		string? found = Settings.ResolveTexture("gfx\\interface\\select_ship_button.tga");
		Assert.Equal("the picture drawn", File.ReadAllText(found!));
	}

	[Fact]
	public void ADdsInTheBaseGameBeatsATgaInTheExpansion()
	{
		// The .dds is looked for in every layer before the name as it is written.
		Write("gfx/interface/listbutton.dds", "base dds");
		Write("tfh/gfx/interface/listbutton.tga", "expansion tga");

		string? found = Settings.ResolveTexture("gfx\\interface\\listbutton.tga");
		Assert.Equal("base dds", File.ReadAllText(found!));
	}

	[Fact]
	public void ATextureNamedDdsIsNotLookedForAsTga()
	{
		Write("gfx/interface/both.tga", "tga");
		Write("gfx/interface/both.dds", "dds");

		string? found = Settings.ResolveTexture("gfx\\interface\\both.dds");
		Assert.Equal("dds", File.ReadAllText(found!));
	}

	[Fact]
	public void MissingTextureReturnsNull()
	{
		Assert.Null(Settings.ResolveTexture("gfx/interface/nothing_here.dds"));
	}

	[Fact]
	public void ListFilesDeduplicatesToTheExpansionCopy()
	{
		Write("interface/core.gfx", "base");
		Write("tfh/interface/core.gfx", "expansion");
		Write("interface/only_base.gfx", "base");
		Write("tfh/interface/only_tfh.gfx", "expansion");

		string[] files = Settings.ListFiles("interface", "*.gfx");

		Assert.Equal(3, files.Length);
		Assert.Single(files, f => Path.GetFileName(f) == "core.gfx");
		Assert.Equal("expansion",
			File.ReadAllText(files.Single(f => Path.GetFileName(f) == "core.gfx")));
	}

	[Fact]
	public void ListFilesReturnsBaseLayerFirst()
	{
		// Registries built from these resolve conflicts by load order, so the base has
		// to be read before the expansion that overrides it.
		Write("interface/aaa_base_only.gfx", "base");
		Write("tfh/interface/zzz_expansion_only.gfx", "expansion");
		Write("interface/mmm_shared.gfx", "base");
		Write("tfh/interface/mmm_shared.gfx", "expansion");

		string[] files = Settings.ListFiles("interface", "*.gfx");
		int baseOnly = Array.FindIndex(files, f => Path.GetFileName(f) == "aaa_base_only.gfx");
		int shared = Array.FindIndex(files, f => Path.GetFileName(f) == "mmm_shared.gfx");
		int expansionOnly = Array.FindIndex(files,
			f => Path.GetFileName(f) == "zzz_expansion_only.gfx");

		// Both expansion-supplied files must come after the base-only one, even though
		// "mmm_shared" sorts before "zzz" alphabetically.
		Assert.True(baseOnly < shared, "base-only file should load before the expansion copy");
		Assert.True(baseOnly < expansionOnly);
	}

	[Fact]
	public void ListFilesOnMissingDirectoryIsEmpty()
	{
		Assert.Empty(Settings.ListFiles("no_such_directory", "*.txt"));
	}
}
