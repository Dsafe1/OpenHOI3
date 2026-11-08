using System;
using System.IO;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.BattlePlans;

/// <summary>
/// <c>battleplans/bp_settings.txt</c>: the options of the four tools that have any. The file is taken whole from the
/// highest layer that has it; a key not known is dropped, as the original drops it. See
/// <c>docs/mechanics/battleplans.md</c>, section 8.
/// </summary>
public sealed class CBattlePlanSettings
{
	public const string FileName = "battleplans/bp_settings.txt";

	public CPlanTool Colours { get; } = new();

	public CPlanTool Arrows { get; } = new();

	public CPlanTool Borders { get; } = new();

	public CPlanTool Icons { get; } = new();

	public static CBattlePlanSettings Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		var settings = new CBattlePlanSettings();
		string path = directories.Resolve(FileName);
		if (!File.Exists(path))
		{
			(warn ?? CDiagnostics.Warn)($"battle plans: no {FileName}");
			return settings;
		}
		settings.Read(ClausewitzParser.ParseFile(path));
		return settings;
	}

	public void Read(ClausewitzNode root)
	{
		ReadTool(root.Get("tool_color"), Colours);
		ReadTool(root.Get("tool_arrow"), Arrows);
		ReadTool(root.Get("tool_borders"), Borders);
		ReadTool(root.Get("tool_icons"), Icons);
	}

	private static void ReadTool(ClausewitzNode? block, CPlanTool tool)
	{
		if (block is null)
		{
			return;
		}
		tool.Icon = block.GetString("icon");
		foreach (ClausewitzNode option in block.All("option"))
		{
			tool.Options.Add(new CPlanToolOption(option.GetString("name"), option.GetString("type"),
				option.GetFloat("red"), option.GetFloat("green"), option.GetFloat("blue")));
		}
	}
}
