using System.Collections.Generic;

namespace OpenHOI3.Content.BattlePlans;

/// <summary>A tool's sprite for its options and the options in the file's order.</summary>
public sealed class CPlanTool
{
	public string Icon { get; set; } = "";

	public List<CPlanToolOption> Options { get; } = new();
}
