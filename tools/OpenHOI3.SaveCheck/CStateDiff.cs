using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// Two games set beside each other field by field, everything a game object holds walked by reflection - the AI's own state
/// with the rest - and every place they differ told, grouped by its path with the numbers taken out. For the reload check:
/// what a game holds that a save and a load do not give back.
/// </summary>
internal sealed class CStateDiff
{
	private readonly HashSet<(object, object)> _visited = new(new CPairComparer());
	private readonly SortedDictionary<string, (int Count, string Example)> _groups = new(StringComparer.Ordinal);
	private readonly int _maxDepth;
	private static readonly Regex Numbers = new(@"\[[^\]]*\]", RegexOptions.Compiled);

	public CStateDiff(int maxDepth = 60, int examples = 1)
	{
		_maxDepth = maxDepth;
		Examples = examples;
	}

	/// <summary>How many examples each group keeps.</summary>
	public int Examples { get; }

	/// <summary>Each path that differs, its numbers taken out, with how many times and one example.</summary>
	public IReadOnlyDictionary<string, (int Count, string Example)> Groups => _groups;

	public void Compare(object? a, object? b, string path)
	{
		Walk(a, b, path, 0);
	}

	private void Walk(object? a, object? b, string path, int depth)
	{
		if (a is null || b is null)
		{
			if (a is not null || b is not null)
			{
				Note(path, $"{Show(a)} / {Show(b)}");
			}
			return;
		}
		if (ReferenceEquals(a, b))
		{
			return;
		}
		Type type = a.GetType();
		if (type != b.GetType())
		{
			Note(path, $"type {type.Name} / {b.GetType().Name}");
			return;
		}
		if (Skipped(type))
		{
			return;
		}
		if (type.IsValueType && !IsValue(type))
		{
			// A struct holding a reference is walked, its copies being new boxes each time.
			foreach (FieldInfo field in FieldsOf(type))
			{
				Walk(field.GetValue(a), field.GetValue(b), $"{path}.{Clean(field.Name)}", depth + 1);
			}
			return;
		}
		if (IsValue(type))
		{
			if (!Equals(a, b))
			{
				Note(path, $"{Show(a)} / {Show(b)}");
			}
			return;
		}
		if (depth > _maxDepth || !_visited.Add((a, b)))
		{
			return;
		}
		if (a is IDictionary da && b is IDictionary db)
		{
			var left = new Dictionary<string, object?>();
			foreach (DictionaryEntry entry in da)
			{
				left[KeyName(entry.Key)] = entry.Value;
			}
			var right = new Dictionary<string, object?>();
			foreach (DictionaryEntry entry in db)
			{
				right[KeyName(entry.Key)] = entry.Value;
			}
			foreach (string key in left.Keys.Union(right.Keys))
			{
				bool inLeft = left.TryGetValue(key, out object? l);
				bool inRight = right.TryGetValue(key, out object? r);
				if (inLeft != inRight && IsNought(inLeft ? l : r))
				{
					// An entry of nought on one side is the same as none on the other.
					continue;
				}
				if (inLeft != inRight)
				{
					Note(path + "{key}", inLeft ? $"only before the load: {key}" : $"only after it: {key}");
					continue;
				}
				Walk(l, r, $"{path}[{key}]", depth + 1);
			}
			return;
		}
		if (a is IEnumerable ea && b is IEnumerable eb && a is not string)
		{
			List<object?> left = ea.Cast<object?>().ToList();
			List<object?> right = eb.Cast<object?>().ToList();
			bool unordered = IsSet(type);
			if (unordered)
			{
				left = left.OrderBy(KeyName, StringComparer.Ordinal).ToList();
				right = right.OrderBy(KeyName, StringComparer.Ordinal).ToList();
			}
			if (Keyed(left) is { } leftKeys && Keyed(right) is { } rightKeys)
			{
				// Things with a number or a tag of their own are matched by it, and their order told apart.
				if (!unordered && !leftKeys.Select(pair => pair.Key).SequenceEqual(rightKeys.Select(pair => pair.Key)))
				{
					Note(path + ".order", $"{left.Count} / {right.Count} entries");
				}
				var byKey = rightKeys.ToDictionary(pair => pair.Key, pair => pair.Value);
				foreach (var (key, value) in leftKeys)
				{
					if (byKey.Remove(key, out object? other))
					{
						Walk(value, other, $"{path}[{key}]", depth + 1);
					}
					else
					{
						Note(path + "{entry}", $"only before the load: {key}");
					}
				}
				foreach (string key in byKey.Keys)
				{
					Note(path + "{entry}", $"only after it: {key}");
				}
			}
			else
			{
				if (left.Count != right.Count)
				{
					Note(path + ".Count", $"{left.Count} / {right.Count}");
				}
				for (int i = 0; i < Math.Min(left.Count, right.Count); i++)
				{
					Walk(left[i], right[i], $"{path}[{i}]", depth + 1);
				}
			}
			if (type.IsArray || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
			{
				return;
			}
		}
		foreach (FieldInfo field in FieldsOf(type))
		{
			Walk(field.GetValue(a), field.GetValue(b), $"{path}.{Clean(field.Name)}", depth + 1);
		}
	}

	private void Note(string path, string example)
	{
		string group = Numbers.Replace(path, "[#]");
		_groups[group] = _groups.TryGetValue(group, out var had)
			? (had.Count + 1, had.Count < Examples ? had.Example + "  ||  " + $"{path}: {example}" : had.Example)
			: (1, $"{path}: {example}");
	}

	private static readonly Dictionary<Type, List<FieldInfo>> s_fields = new();

	private static List<FieldInfo> FieldsOf(Type type)
	{
		if (s_fields.TryGetValue(type, out List<FieldInfo>? had))
		{
			return had;
		}
		var fields = new List<FieldInfo>();
		for (Type? t = type; t is not null && t != typeof(object); t = t.BaseType)
		{
			if (t.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
			{
				break;
			}
			fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
		}
		return s_fields[type] = fields;
	}

	private static readonly Dictionary<Type, bool> s_plain = new();

	/// <summary>Compared by its own equality: a primitive, an enum, a string, or a struct holding nothing but such.</summary>
	private static bool IsValue(Type type)
	{
		if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
		{
			return true;
		}
		if (!type.IsValueType)
		{
			return false;
		}
		if (s_plain.TryGetValue(type, out bool plain))
		{
			return plain;
		}
		s_plain[type] = true;
		plain = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.All(field => field.FieldType != type && IsValue(field.FieldType));
		return s_plain[type] = plain;
	}

	private static bool IsSet(Type type)
	{
		return type.IsGenericType && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>));
	}

	private static bool Skipped(Type type)
	{
		return typeof(Delegate).IsAssignableFrom(type) || typeof(MemberInfo).IsAssignableFrom(type)
			|| type.Namespace is { } ns && (ns.StartsWith("System.Threading", StringComparison.Ordinal)
				|| ns.StartsWith("System.Diagnostics", StringComparison.Ordinal) || ns.StartsWith("System.IO", StringComparison.Ordinal)
				|| ns.StartsWith("System.Runtime", StringComparison.Ordinal) || ns.StartsWith("System.Reflection", StringComparison.Ordinal))
			|| type.Namespace == "OpenHOI3.Lua" || type.Name.Contains("Lua", StringComparison.Ordinal);
	}

	private static bool IsNought(object? value)
	{
		return value is null || IsValue(value.GetType()) && value.GetType().IsValueType && Equals(value, Activator.CreateInstance(value.GetType()));
	}

	/// <summary>The entries by a number or tag of their own, where every one has one and none is shared; else null.</summary>
	private static List<KeyValuePair<string, object?>>? Keyed(List<object?> entries)
	{
		if (entries.Count == 0 || entries.Any(entry => entry is null || IsValue(entry.GetType())))
		{
			return entries.Count == 0 ? new List<KeyValuePair<string, object?>>() : null;
		}
		var keyed = new List<KeyValuePair<string, object?>>();
		var seen = new HashSet<string>();
		foreach (object? entry in entries)
		{
			string key = KeyName(entry);
			if (!(key.StartsWith("Tag=", StringComparison.Ordinal) || key.StartsWith("Id=", StringComparison.Ordinal)) || !seen.Add(key))
			{
				return null;
			}
			keyed.Add(new KeyValuePair<string, object?>(key, entry));
		}
		return keyed;
	}

	private static string KeyName(object? key)
	{
		if (key is null)
		{
			return "null";
		}
		Type type = key.GetType();
		if (IsValue(type))
		{
			return key.ToString() ?? "";
		}
		foreach (string name in new[] { "Tag", "Id", "Name", "Key" })
		{
			if (type.GetProperty(name) is { } property && property.GetIndexParameters().Length == 0
				&& property.GetValue(key) is { } value)
			{
				return $"{name}={value}";
			}
		}
		return key.ToString() ?? "";
	}

	private static string Show(object? value)
	{
		return value switch
		{
			null => "null",
			string text => $"\"{text}\"",
			_ when IsValue(value.GetType()) => value.ToString() ?? "",
			_ => value.GetType().Name,
		};
	}

	private static string Clean(string name)
	{
		return name.StartsWith('<') && name.IndexOf('>') is var end and > 0 ? name[1..end] : name;
	}

	private sealed class CPairComparer : IEqualityComparer<(object, object)>
	{
		public bool Equals((object, object) x, (object, object) y) => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);

		public int GetHashCode((object, object) pair) =>
			HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Item1),
				System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Item2));
	}
}
