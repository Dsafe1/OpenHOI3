using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Messages;

/// <summary>
/// The message types of <c>interface/messagetypes.txt</c> and then the player's <c>messagetypes_custom.txt</c>, by name
/// and in the order they were first declared. A type declared again is the second, whole, in its first place; one
/// nobody declared is <see cref="CMessageType.Null"/>, which logs - and here it is reported too, as is a key a type does
/// not take. See <c>docs/mechanics/messages.md</c>, section 1, and <c>docs/mechanics/ingamemenu.md</c>, section 5.
/// </summary>
public sealed class CMessageTypes
{
	public const string File = "interface/messagetypes.txt";

	/// <summary>The player's own settings, in the player's folder.</summary>
	public const string CustomFile = "messagetypes_custom.txt";

	private static readonly string[] Categories =
	{
		"cat_combat", "cat_diplomacy", "cat_units", "cat_province", "cat_events", "cat_other",
	};

	private readonly Dictionary<string, CMessageType> _types = new(StringComparer.Ordinal);
	private readonly List<CMessageType> _order = new();
	private readonly Action<string> _warn;
	private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

	private CMessageTypes(Action<string> warn)
	{
		_warn = warn;
	}

	public int Count => _types.Count;

	/// <summary>Every type, in the order it was first declared - the message settings' rows.</summary>
	public IReadOnlyList<CMessageType> All => _order;

	/// <summary>
	/// The install's types, and over them the player's file where a path to it is given and it is there - as the
	/// original reads them at every start.
	/// </summary>
	public static CMessageTypes Load(CDirectorySettings directories, string? customPath = null, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var types = new CMessageTypes(warn);
		types.ReadFile(directories.Resolve(File));
		if (customPath is not null)
		{
			types.ReadFile(customPath);
		}
		return types;
	}

	/// <summary>The types of a file already parsed.</summary>
	public static CMessageTypes Read(ClausewitzNode root, Action<string>? warn = null)
	{
		var types = new CMessageTypes(warn ?? CDiagnostics.Problem);
		types.Read(root);
		return types;
	}

	/// <summary>A file read over the types there are: each it names is overwritten whole, and a new one goes last.</summary>
	public void ReadOver(ClausewitzNode root)
	{
		Read(root);
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

	/// <summary>
	/// The settings window's Reset: the player's file deleted and the install's read again over the types, and nothing
	/// else - a type only the player's file declared keeps what it had.
	/// </summary>
	public void Reset(CDirectorySettings directories, string customPath)
	{
		try
		{
			if (System.IO.File.Exists(customPath))
			{
				System.IO.File.Delete(customPath);
			}
		}
		catch (Exception e)
		{
			_warn($"Could not delete '{customPath}': {e.Message}");
		}
		ReadFile(directories.Resolve(File));
	}

	/// <summary>The settings window's Close: every type, whole and in order, into the player's file.</summary>
	public void WriteCustom(string customPath)
	{
		try
		{
			System.IO.File.WriteAllBytes(customPath, Encoding.Latin1.GetBytes(Write()));
		}
		catch (Exception e)
		{
			_warn($"Could not write '{customPath}': {e.Message}");
		}
	}

	/// <summary>What <see cref="WriteCustom"/> writes, as the original writes it: a key a line, each line ended CR LF.</summary>
	public string Write()
	{
		var text = new StringBuilder();
		foreach (CMessageType type in _order)
		{
			text.Append(type.Name).Append("=\r\n{\r\n");
			Line(text, "log", type.Log);
			Line(text, "onmap", type.OnMap);
			Line(text, "popup", type.Popup);
			Line(text, "idle", type.Idle);
			Line(text, "option", type.Option);
			Line(text, "pausepopup", type.PausePopup);
			text.Append("categories=\"").Append(Categories[(int)type.Category]).Append("\"\r\n}\r\n");
		}
		return text.ToString();
	}

	private static void Line(StringBuilder text, string key, bool value)
	{
		text.Append(key).Append('=').Append(value ? "yes" : "no").Append("\r\n");
	}

	private void ReadFile(string path)
	{
		if (!System.IO.File.Exists(path))
		{
			return;
		}
		try
		{
			Read(ClausewitzParser.ParseFile(path));
		}
		catch (Exception e)
		{
			_warn($"Could not read '{path}': {e.Message}");
		}
	}

	private void Read(ClausewitzNode root)
	{
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			// A second declaration is the type, whole, where the first stood.
			if (!_types.TryGetValue(block.Key, out CMessageType? type))
			{
				type = new CMessageType(block.Key);
				_types[block.Key] = type;
				_order.Add(type);
			}
			else
			{
				type.SetDefaults();
			}
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
		}
	}

	private static bool IsYes(string? value)
	{
		return string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
	}
}
