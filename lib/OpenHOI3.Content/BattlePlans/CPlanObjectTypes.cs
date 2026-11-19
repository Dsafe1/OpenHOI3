using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.BattlePlans;

/// <summary>Every <c>battlearrow</c> and <c>mapinfo</c> type of the install's <c>.gfx</c> files, the last declaration winning.</summary>
public sealed class CPlanObjectTypes
{
	private readonly Dictionary<string, CBattleArrowType> _arrows = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CMapInfoType> _icons = new(StringComparer.OrdinalIgnoreCase);

	public CBattleArrowType? Arrow(string name) => _arrows.TryGetValue(name, out CBattleArrowType? type) ? type : null;

	public CMapInfoType? Icon(string name) => _icons.TryGetValue(name, out CMapInfoType? type) ? type : null;

	public static CPlanObjectTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		var types = new CPlanObjectTypes();
		foreach (string file in directories.ListFiles("interface", "*.gfx"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception failed) when (failed is IOException or FormatException or InvalidOperationException)
			{
				(warn ?? CDiagnostics.Problem)($"Could not parse '{file}': {failed.Message}");
				continue;
			}
			types.Read(root);
		}
		return types;
	}

	public void Read(ClausewitzNode root)
	{
		foreach (ClausewitzNode container in root.All("objectTypes"))
		{
			foreach (ClausewitzNode node in container.All("battlearrow"))
			{
				string name = node.GetString("name");
				_arrows[name] = new CBattleArrowType(name, node.GetString("textureFile"), node.GetString("textureFile1"), node.GetFloat("start"),
					node.GetFloat("stop"), node.GetFloat("x"), node.GetFloat("y"), node.GetString("font"), node.GetFloat("scale", 1f),
					node.GetFloat("textureloop"), node.GetString("nofade") == "yes");
			}
			foreach (ClausewitzNode node in container.All("mapinfo"))
			{
				string name = node.GetString("name");
				_icons[name] = new CMapInfoType(name, node.GetString("textureFile"), node.GetFloat("scale", 1f));
			}
		}
	}
}
