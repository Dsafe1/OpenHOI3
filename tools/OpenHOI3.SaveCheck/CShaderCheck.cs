using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Shaders;

/// <summary>
/// Reads and translates every effect file of the install's <c>gfx/FX</c>, and of a mod's where one is given:
///
///   OpenHOI3.SaveCheck shaders &lt;install&gt; [mod folder] [out=folder] [file.fx] [canvas] [mod=name]
///
/// Each file is preprocessed, parsed and every pass of every technique written as a Godot shader; it says how many of
/// each read and translated and lists every problem. A mod's folder is laid over the install's, as the original lays
/// a mod's files. <c>out=</c> writes each translation there as <c>file.technique.pass.gdshader</c>. Nothing is written
/// anywhere else. See <c>docs/mechanics/shaders.md</c>.
/// </summary>
public static class CShaderCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("shaders: <install> [mod folder] [out=folder] [file.fx] [canvas]");
			return 2;
		}
		CDirectorySettings? directories = CToolDirectories.Make(args[1], ref args);
		if (directories is null)
		{
			return 2;
		}
		string? mod = args.Skip(2).FirstOrDefault(a => !a.StartsWith("out=") && !a.EndsWith(".fx") && a != "canvas" && Directory.Exists(a));
		string? output = args.FirstOrDefault(a => a.StartsWith("out="))?["out=".Length..];
		// canvas writes every pass for the 2D canvas, the first texture of a file the drawn one.
		bool canvas = args.Contains("canvas");
		string? only = args.Skip(2).FirstOrDefault(a => a.EndsWith(".fx"));
		// pre=file writes the one file given, preprocessed, for reading a problem's line against.
		string? preprocessed = args.FirstOrDefault(a => a.StartsWith("pre="))?["pre=".Length..];

		// The folders a file is looked for in, highest first: the mod's, then the install's layers.
		var folders = new List<string>();
		if (mod is not null)
		{
			folders.Add(Path.Combine(mod, "gfx", "FX"));
		}
		folders.AddRange(directories.LayerFolders(CFxEffectReader.Folder));

		if (preprocessed is not null && only is not null)
		{
			string? Find(string file) => folders.Select(folder => Path.Combine(folder, file)).FirstOrDefault(File.Exists) is { } path
				? File.ReadAllText(path, System.Text.Encoding.Latin1)
				: null;
			if (Find(only) is { } source)
			{
				File.WriteAllText(preprocessed, CFxPreprocessor.Process(source, Find, problem => Console.WriteLine("preprocessor: " + problem)));
			}
		}
		var names = folders.Where(Directory.Exists)
			.SelectMany(folder => Directory.GetFiles(folder, "*.fx").Select(Path.GetFileName))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Where(name => only is null || string.Equals(name, only, StringComparison.OrdinalIgnoreCase))
			.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (output is not null)
		{
			Directory.CreateDirectory(output);
		}

		int files = 0;
		int clean = 0;
		int passes = 0;
		int translated = 0;
		foreach (string name in names.OfType<string>())
		{
			files++;
			CFxEffect? effect = CFxEffectReader.Read(folders, name);
			if (effect is null)
			{
				continue;
			}
			var problems = new List<string>(effect.Problems);
			foreach (CFxTechnique technique in effect.Techniques)
			{
				foreach (CFxPass pass in technique.Passes)
				{
					passes++;
					CGodotShader shader = canvas
						? CGodotShaderWriter.Write(effect, technique, pass, EShaderTarget.Canvas, effect.Variables.FirstOrDefault(v => v.Type.Base == EFxBase.Texture)?.Name)
						: CGodotShaderWriter.Write(effect, technique, pass);
					problems.AddRange(shader.Problems.Select(p => $"{technique.Name}.{pass.Name}: {p}"));
					if (shader.Problems.Count == 0)
					{
						translated++;
					}
					if (output is not null)
					{
						File.WriteAllText(Path.Combine(output, $"{Path.GetFileNameWithoutExtension(name)}.{technique.Name}.{pass.Name}.gdshader"),
							shader.Code);
					}
				}
			}
			if (problems.Count == 0)
			{
				clean++;
			}
			Console.WriteLine($"{name}: {effect.Techniques.Count} techniques, {effect.Functions.Count} functions, " +
				$"{effect.Variables.Count} variables, {effect.Samplers.Count} samplers, {problems.Count} problems");
			foreach (string problem in problems.Take(12))
			{
				Console.WriteLine("    " + problem);
			}
			if (problems.Count > 12)
			{
				Console.WriteLine($"    ... and {problems.Count - 12} more");
			}
		}
		Console.WriteLine($"{clean} of {files} files without a problem; {translated} of {passes} passes translated cleanly.");
		return clean == files ? 0 : 1;
	}
}
