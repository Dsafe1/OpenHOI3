using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// Reads an effect file of <c>gfx/FX</c> through the install's layers: the file the highest layer has, its
/// <c>#include</c>s looked for the same way, preprocessed and parsed. What the preprocessor could not follow goes in
/// front of what the parser reports. See <c>docs/mechanics/shaders.md</c>, section 2.
/// </summary>
public static class CFxEffectReader
{
	public const string Folder = "gfx/FX";

	/// <summary>An effect by its file name in <c>gfx/FX</c>, or null where no layer has it.</summary>
	public static CFxEffect? Read(CDirectorySettings directories, string fileName)
	{
		return Read(name => ReadLayered(directories, name), fileName);
	}

	/// <summary>An effect from files looked for in folders, the first that has a file giving it.</summary>
	public static CFxEffect? Read(IReadOnlyList<string> folders, string fileName)
	{
		return Read(name => folders.Select(folder => Path.Combine(folder, name)).FirstOrDefault(File.Exists) is { } path
			? ReadText(path)
			: null, fileName);
	}

	/// <summary>An effect from a reader of files by name.</summary>
	public static CFxEffect? Read(Func<string, string?> readFile, string fileName)
	{
		if (readFile(fileName) is not { } source)
		{
			return null;
		}
		var problems = new List<string>();
		string text = CFxPreprocessor.Process(source, readFile, problems.Add);
		CFxEffect effect = CFxParser.Parse(text);
		return effect with { Problems = problems.Concat(effect.Problems).ToList() };
	}

	private static string? ReadLayered(CDirectorySettings directories, string name)
	{
		string path = directories.Resolve(Folder + "/" + name);
		return File.Exists(path) ? ReadText(path) : null;
	}

	/// <summary>The original's text files are Windows-1252; an effect file's are plain ASCII but for its comments.</summary>
	private static string ReadText(string path) => Encoding.Latin1.GetString(File.ReadAllBytes(path));
}
