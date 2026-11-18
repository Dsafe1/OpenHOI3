using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Launch;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// A fixture install for the launcher's tests (<c>docs/launcher.md</c>): the folders an install has, descriptors in
/// <c>tfh/mod</c> with their folders, and engines - an <c>engine.txt</c> and an empty program - wherever asked.
/// </summary>
public sealed class LauncherFixture : IDisposable
{
	public LauncherFixture()
	{
		Root = Path.Combine(Path.GetTempPath(), "openhoi3-launcher-" + Guid.NewGuid().ToString("N"));
		Documents = Path.Combine(Root, "Documents", "Hearts of Iron III");
		foreach (string folder in new[] { "interface", "gfx", "localisation" })
		{
			Directory.CreateDirectory(Path.Combine(Game, folder));
		}
		Directory.CreateDirectory(Documents);
	}

	public string Root { get; }

	public string Game => Path.Combine(Root, "game");

	public string Documents { get; }

	public void Dispose()
	{
		try
		{
			Directory.Delete(Root, recursive: true);
		}
		catch (IOException)
		{
			// A locked file must not fail the test that already passed.
		}
	}

	/// <summary>A descriptor tfh/mod/&lt;file&gt;.mod for a folder tfh/mod/&lt;file&gt;, with the lines given after its name and path.</summary>
	public void Descriptor(string file, params string[] lines)
	{
		string folder = ModFolder(file);
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(Game, "tfh", "mod", file + ".mod"),
			$"name = \"{file}\"\npath = \"tfh/mod/{file}\"\n" + string.Join("\n", lines) + "\n");
	}

	/// <summary>A mod's folder.</summary>
	public string ModFolder(string file) => Path.Combine(Game, "tfh", "mod", file);

	/// <summary>An engine in a folder: its <c>engine.txt</c> and an empty <c>OpenHOI3.exe</c>.</summary>
	public string Engine(string folder, string version, int contract = CLaunchContract.Version)
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(folder, CEngineFile.FileName), CEngineFile.Text(version, "OpenHOI3.exe", contract));
		File.WriteAllBytes(Path.Combine(folder, "OpenHOI3.exe"), Array.Empty<byte>());
		return folder;
	}

	/// <summary>The engine a mod ships, in its folder's engine folder.</summary>
	public string ModEngine(string file, string version, int contract = CLaunchContract.Version)
	{
		return Engine(Path.Combine(ModFolder(file), CEngineFile.FolderName), version, contract);
	}

	/// <summary>The launcher's own engine, read from a folder of the fixture's.</summary>
	public CEngine Own(string version = "normal-1")
	{
		return CEngineFile.Read(Engine(Path.Combine(Root, "launcher", CEngineFile.FolderName), version))!;
	}

	/// <summary>Every descriptor as the launcher lists it.</summary>
	public List<CModEntry> Entries() => CModList.Read(Game, Documents);

	/// <summary>The mods that may be played.</summary>
	public List<CMod> Mods() => CModList.Playable(Entries());

	/// <summary>The mods of the files named, in the list's order.</summary>
	public List<CMod> Chosen(params string[] files)
	{
		var picks = new HashSet<string>(StringComparer.Ordinal);
		List<CMod> mods = Mods();
		foreach (string file in files)
		{
			picks = CModPicks.Set(mods, picks, mods.Find(mod => mod.VirtualPath == $"mod/{file}.mod")!, on: true);
		}
		return CModPicks.Chosen(mods, picks);
	}
}
