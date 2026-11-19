using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Every <c>projectionType</c> the interface's <c>.gfx</c> files declare, by name - in the same <c>objectTypes</c> blocks
/// as the counter and billboard types. See <see cref="CProjectionType"/>.
/// </summary>
public sealed class CProjectionTypes
{
	/// <summary>The mark a right click leaves on a province a unit is sent to.</summary>
	public const string Legal = "legal_selection_projection";

	/// <summary>The mark a right click leaves on a province a unit may not be sent to.</summary>
	public const string Illegal = "illegal_selection_projection";

	/// <summary>
	/// The ten keys the original's reading of one takes, and the name (<c>reversing/findings/FINDINGS-definitions.md</c>):
	/// any other it drops in silence, and here it is reported.
	/// </summary>
	private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "textureFile", "size", "spin", "pulsating", "pulseLowest", "pulseSpeed", "additative", "expanding", "duration",
		"fadeout",
	};

	private readonly Dictionary<string, CProjectionType> _types = new(StringComparer.OrdinalIgnoreCase);

	public int Count => _types.Count;

	public CProjectionType? Find(string name)
	{
		return _types.TryGetValue(name, out CProjectionType? type) ? type : null;
	}

	public static CProjectionTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var types = new CProjectionTypes();
		foreach (string file in directories.ListFiles("interface", "*.gfx"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception e)
			{
				warn(CDiagnostics.Located(file, 0, $"could not be parsed: {e.Message}"));
				continue;
			}
			foreach (ClausewitzNode container in root.All("objectTypes"))
			{
				foreach (ClausewitzNode node in container.Children.Where(child => child.IsBlock
					&& string.Equals(child.Key, "projectionType", StringComparison.OrdinalIgnoreCase)))
				{
					if (Read(node, Path.GetFileName(file), warn) is { } type)
					{
						// The last declaration wins: files come base layer first.
						types._types[type.Name] = type;
					}
				}
			}
		}
		return types;
	}

	/// <summary>One declaration, every key it leaves out at the original's default.</summary>
	public static CProjectionType? Read(ClausewitzNode node, string file, Action<string> warn)
	{
		string name = node.GetString("name");
		if (name.Length == 0)
		{
			warn(CDiagnostics.Located(file, node.Line, "a projectionType has no name and is left out"));
			return null;
		}
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not null && !Keys.Contains(child.Key))
			{
				warn(CDiagnostics.Located(file, child.Line, $"projectionType '{name}' has a key nothing reads: {child.Key}"));
			}
		}
		return new CProjectionType
		{
			Name = name,
			TextureFile = node.GetString("textureFile"),
			Size = node.GetFloat("size", 1f),
			Spin = node.GetFloat("spin"),
			Pulsating = node.GetBool("pulsating"),
			PulseLowest = node.GetFloat("pulseLowest", 0.4f),
			PulseSpeed = node.GetFloat("pulseSpeed", 3f),
			Additive = node.GetBool("additative"),
			Expanding = node.GetFloat("expanding"),
			Duration = node.GetFloat("duration"),
			Fadeout = node.GetFloat("fadeout"),
		};
	}
}
