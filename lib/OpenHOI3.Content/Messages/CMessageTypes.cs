using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Messages;

/// <summary>
/// The message types of <c>interface/messagetypes.txt</c>, by name. A type declared twice is the
/// second, whole; one nobody declared is <see cref="CMessageType.Null"/>, which logs - and here it is
/// reported too, as is a key a type does not take. See <c>docs/mechanics/messages.md</c>, section 1.
/// </summary>
public sealed class CMessageTypes
{
	public const string File = "interface/messagetypes.txt";

	private static readonly string[] Categories =
	{
		"cat_combat", "cat_diplomacy", "cat_units", "cat_province", "cat_events", "cat_other",
	};

	private readonly Dictionary<string, CMessageType> _types = new(StringComparer.Ordinal);
	private readonly Action<string> _warn;
	private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

	private CMessageTypes(Action<string> warn)
	{
		_warn = warn;
	}

	public int Count => _types.Count;

	public static CMessageTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var types = new CMessageTypes(warn);
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			return types;
		}
		try
		{
			types.Read(ClausewitzParser.ParseFile(path));
		}
		catch (Exception e)
		{
			warn($"Could not read '{path}': {e.Message}");
		}
		return types;
	}

	/// <summary>The types of a file already parsed.</summary>
	public static CMessageTypes Read(ClausewitzNode root, Action<string>? warn = null)
	{
		var types = new CMessageTypes(warn ?? CDiagnostics.Warn);
		types.Read(root);
		return types;
	}

	/// <summary>A type by name, or the null type - which logs - where nobody declared it.</summary>
	public CMessageType Find(string name)
	{
		if (_types.TryGetValue(name, out CMessageType? type))
		{
			return type;
		}
		if (_reported.Add(name))
		{
			_warn($"'{File}' declares no message type '{name}'; the original logs it as NULL_MESSAGE");
		}
		return CMessageType.Null;
	}

	private void Read(ClausewitzNode root)
	{
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			// A second declaration is the type, whole.
			var type = new CMessageType(block.Key);
			foreach (ClausewitzNode entry in block.Children)
			{
				string? value = entry.Value;
				switch (entry.Key?.ToLowerInvariant())
				{
					case "log":
						type.Log = IsYes(value);
						break;
					case "onmap":
						type.OnMap = IsYes(value);
						break;
					case "popup":
						type.Popup = IsYes(value);
						break;
					case "idle":
						type.Idle = IsYes(value);
						break;
					case "option":
						type.Option = IsYes(value);
						break;
					case "pausepopup":
						type.PausePopup = IsYes(value);
						break;
					case "categories":
					{
						int index = Array.IndexOf(Categories, value);
						type.Category = index < 0 ? EMessageCategory.Other : (EMessageCategory)index;
						break;
					}
					default:
						_warn($"'{File}': the message type '{block.Key}' has a key '{entry.Key}' no type takes");
						break;
				}
			}
			_types[block.Key] = type;
		}
	}

	private static bool IsYes(string? value)
	{
		return string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
	}
}
