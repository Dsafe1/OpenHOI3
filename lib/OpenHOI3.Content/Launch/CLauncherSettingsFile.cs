using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// The launcher's <c>launcher.txt</c>, in the player's application data and not beside the launcher, whose folder may
/// not be writable (<c>docs/launcher.md</c>, section 6). Ours, so UTF-8: a path may hold any letter a user name does.
/// </summary>
public static class CLauncherSettingsFile
{
	/// <summary>Where the launcher keeps it on this machine.</summary>
	public static string DefaultPath()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenHOI3", "launcher.txt");
	}

	/// <summary>What a file remembers, or nothing where there is none or it cannot be read.</summary>
	public static CLauncherSettings Read(string file)
	{
		if (!File.Exists(file))
		{
			return CLauncherSettings.Empty;
		}
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.Parse(File.ReadAllText(file, Encoding.UTF8));
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
		{
			return CLauncherSettings.Empty;
		}
		string? install = null;
		var mods = new List<string>();
		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key == "install" && entry.Value is { Length: > 0 } value)
			{
				install = value;
			}
			else if (entry.Key == "mods" && entry.IsBlock)
			{
				mods.AddRange(entry.Children.Where(child => child.Key is null && child.Value is { Length: > 0 }).Select(child => child.Value!));
			}
		}
		return new CLauncherSettings(install, mods);
	}

	/// <summary>Writes what is to be remembered, making the folder where needed.</summary>
	public static void Write(string file, CLauncherSettings settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
		File.WriteAllText(file, Text(settings), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	/// <summary>The file's text: a backslash doubled, as the reader takes two for one.</summary>
	public static string Text(CLauncherSettings settings)
	{
		var text = new StringBuilder("# What the OpenHOI3 launcher remembers (docs/launcher.md).\n");
		if (settings.Install is not null)
		{
			text.Append($"install = \"{Quoted(settings.Install)}\"\n");
		}
		text.Append("mods = {\n");
		foreach (string mod in settings.Mods)
		{
			text.Append($"\t\"{Quoted(mod)}\"\n");
		}
		text.Append("}\n");
		return text.ToString();
	}

	private static string Quoted(string value) => value.Replace("\\", "\\\\");
}
