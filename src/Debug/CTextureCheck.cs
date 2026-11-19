using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Gfx;

namespace OpenHOI3.Debug;

/// <summary>
/// <c>--texturecheck=folder</c>: every <c>.dds</c>, <c>.tga</c> and <c>.png</c> under a folder read as the game reads a
/// texture (<see cref="CTextureManager.DecodeFile"/>), each that fails printed with its reason, each that is read but has
/// Godot's engine log something printed with the first of what it logged, then the counts, and the program ended. Nothing
/// of a game is made and nothing is written - unless <c>--textureout=folder</c> is given too, when each picture read is
/// written there as a PNG, to be looked at. See <c>docs/mechanics/interface.md</c>, section 5.
/// </summary>
public static class CTextureCheck
{
	public const string Switch = "--texturecheck=";

	private static readonly string[] Extensions = { ".dds", ".tga", ".png" };

	/// <summary>With <see cref="Switch"/>, a folder each picture read is written to as a PNG, under its path in the folder checked.</summary>
	public const string OutSwitch = "--textureout=";

	public static void Run(string folder, string? outFolder, SceneTree tree)
	{
		string[] files = Directory.Exists(folder)
			? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
				.Where(file => Extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
				.OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
				.ToArray()
			: Array.Empty<string>();
		var counter = new CGodotMessageCounter();
		OS.AddLogger(counter);
		Action<string> sink = CDiagnostics.WarnSink;
		Action<string> problems = CDiagnostics.ProblemSink;
		string? reason = null;
		// A texture that cannot be read is a fault in the files, reported as a problem (docs/mechanics/logs.md, section 4).
		CDiagnostics.WarnSink = message => reason ??= message;
		CDiagnostics.ProblemSink = message => reason ??= message;
		var read = new Dictionary<string, int>();
		var failed = new Dictionary<string, int>();
		int noisy = 0;
		try
		{
			foreach (string file in files)
			{
				string extension = Path.GetExtension(file).ToLowerInvariant();
				string name = Path.GetRelativePath(folder, file);
				reason = null;
				counter.Take(out _);
				Image? image = CTextureManager.DecodeFile(file);
				int logged = counter.Take(out string? first);
				if (image is null)
				{
					failed[extension] = failed.GetValueOrDefault(extension) + 1;
					GD.Print($"texturecheck: FAILED {name}: {reason ?? "no reason given"}"
						+ (logged > 0 ? $" - Godot logged {logged}, first: {first}" : ""));
					continue;
				}
				read[extension] = read.GetValueOrDefault(extension) + 1;
				if (logged > 0)
				{
					noisy++;
					GD.Print($"texturecheck: read {name}, but Godot logged {logged}, first: {first}");
				}
				if (outFolder is not null)
				{
					string png = Path.Combine(outFolder, name + ".png");
					Directory.CreateDirectory(Path.GetDirectoryName(png)!);
					if (image.IsCompressed())
					{
						image.Decompress();
					}
					image.SavePng(png);
				}
				image.Dispose();
			}
		}
		finally
		{
			CDiagnostics.WarnSink = sink;
			CDiagnostics.ProblemSink = problems;
			OS.RemoveLogger(counter);
		}
		foreach (string extension in Extensions)
		{
			GD.Print($"texturecheck: {extension} {read.GetValueOrDefault(extension)} read, {failed.GetValueOrDefault(extension)} failed");
		}
		GD.Print($"texturecheck: {files.Length} files under {folder}, {read.Values.Sum()} read, {failed.Values.Sum()} failed, "
			+ $"{noisy} read with Godot logging something");
		tree.Quit();
	}
}
