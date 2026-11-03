using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// Writes a command as JSON and reads it back, field by field, whatever its kind: a command holds its own targets as plain
/// values - numbers, tags, text, figures, and records of them - so its fields are all there is to it. What comes from the
/// loaded files is written by name - a region, a casus belli type - and a rebel faction by its number. A command that holds
/// a live part of the game - a unit, a country, a province, a war - or a delegate cannot be played back, and writing one
/// fails at once rather than when it is read. Ours; see <c>docs/replay.md</c>.
/// </summary>
public static class CCommandCodec
{
	private const string TypeKey = "$t";

	/// <summary>The types a command may not hold, being parts of the game rather than values.</summary>
	private static readonly Type[] Live =
	{
		typeof(CGameState), typeof(CCountry), typeof(Provinces.CProvince), typeof(Units.CUnit), typeof(Units.CSubUnit),
		typeof(Wars.CWar), typeof(Leaders.CLeader),
	};

	public static void Write(Utf8JsonWriter writer, CCommand command)
	{
		WriteObject(writer, command, writeType: true);
	}

	public static CCommand Read(JsonElement element, CGameState state)
	{
		return (CCommand)ReadObject(element, typeof(CCommand), state)!;
	}

	private static void WriteValue(Utf8JsonWriter writer, object? value, Type declared)
	{
		switch (value)
		{
			case null:
				writer.WriteNullValue();
				return;
			case string text:
				writer.WriteStringValue(text);
				return;
			case bool flag:
				writer.WriteBooleanValue(flag);
				return;
			case Enum named:
				writer.WriteStringValue(named.ToString());
				return;
			case int or long or short or byte or sbyte or ushort or uint:
				writer.WriteNumberValue(Convert.ToInt64(value));
				return;
			case ulong big:
				writer.WriteStringValue(big.ToString(System.Globalization.CultureInfo.InvariantCulture));
				return;
			case CCountryTag tag:
				writer.WriteStringValue(tag.Value);
				return;
			case Provinces.CRegion region:
				writer.WriteStringValue(region.Name);
				return;
			case Wars.CCasusBelliType casusBelli:
				writer.WriteStringValue(casusBelli.Name);
				return;
			case Revolts.CRebelFaction faction:
				writer.WriteNumberValue(faction.Id);
				return;
			case Script.CEventScope scope:
				WriteScope(writer, scope);
				return;
			case Delegate:
				throw new NotSupportedException($"a command holds a delegate ({value.GetType().Name}), which a recording cannot keep");
		}
		Type type = value.GetType();
		if (Live.Any(live => live.IsAssignableFrom(type)))
		{
			throw new NotSupportedException($"a command holds a {type.Name}, a part of the game a recording cannot keep");
		}
		if (value is IEnumerable items and not string)
		{
			writer.WriteStartArray();
			Type element = ElementType(declared) ?? ElementType(type) ?? typeof(object);
			foreach (object? item in items)
			{
				WriteValue(writer, item, element);
			}
			writer.WriteEndArray();
			return;
		}
		// A record or a struct of values: its fields, with its type where the field could hold another.
		Type underlying = Nullable.GetUnderlyingType(declared) ?? declared;
		WriteObject(writer, value, writeType: type != underlying);
	}

	private static void WriteObject(Utf8JsonWriter writer, object value, bool writeType)
	{
		writer.WriteStartObject();
		if (writeType)
		{
			writer.WriteString(TypeKey, value.GetType().FullName);
		}
		foreach (FieldInfo field in Fields(value.GetType()))
		{
			writer.WritePropertyName(field.Name);
			WriteValue(writer, field.GetValue(value), field.FieldType);
		}
		writer.WriteEndObject();
	}

	private static void WriteScope(Utf8JsonWriter writer, Script.CEventScope scope)
	{
		writer.WriteStartObject();
		writer.WriteString("country", scope.Country?.Value);
		writer.WriteNumber("province", scope.Province);
		writer.WriteString("from", scope.FromCountry?.Value);
		writer.WriteNumber("fromProvince", scope.FromProvince);
		writer.WriteNumber("seed", scope.Seed);
		writer.WriteNumber("random", scope.RandomState);
		writer.WriteNumber("lookAhead", scope.LookAheadDays);
		if (scope.RebelFaction is { } faction)
		{
			writer.WriteNumber("faction", faction.Id);
		}
		writer.WriteEndObject();
	}

	private static object? ReadValue(JsonElement element, Type declared, CGameState state)
	{
		if (element.ValueKind == JsonValueKind.Null)
		{
			return null;
		}
		Type type = Nullable.GetUnderlyingType(declared) ?? declared;
		if (type == typeof(string))
		{
			return element.GetString();
		}
		if (type == typeof(bool))
		{
			return element.GetBoolean();
		}
		if (type.IsEnum)
		{
			return Enum.Parse(type, element.GetString()!);
		}
		if (type == typeof(int))
		{
			return element.GetInt32();
		}
		if (type == typeof(long))
		{
			return element.GetInt64();
		}
		if (type == typeof(uint) || type == typeof(short) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(ushort))
		{
			return Convert.ChangeType(element.GetInt64(), type, System.Globalization.CultureInfo.InvariantCulture);
		}
		if (type == typeof(ulong))
		{
			return ulong.Parse(element.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
		}
		if (type == typeof(CCountryTag))
		{
			return new CCountryTag(element.GetString()!);
		}
		if (type == typeof(Provinces.CRegion))
		{
			string name = element.GetString()!;
			return state.Regions.FirstOrDefault(region => region.Name == name)
				?? throw new InvalidOperationException($"the recording names a region '{name}' the game does not have");
		}
		if (type == typeof(Wars.CCasusBelliType))
		{
			string name = element.GetString()!;
			return state.CasusBelliTypes.FirstOrDefault(cb => cb.Name == name)
				?? throw new InvalidOperationException($"the recording names a casus belli '{name}' the game does not have");
		}
		if (type == typeof(Revolts.CRebelFaction))
		{
			return Faction(state, element.GetInt32());
		}
		if (type == typeof(Script.CEventScope))
		{
			return ReadScope(element, state);
		}
		if (element.ValueKind == JsonValueKind.Array)
		{
			Type item = ElementType(type) ?? typeof(object);
			var values = element.EnumerateArray().Select(entry => ReadValue(entry, item, state)).ToList();
			if (type.IsArray)
			{
				Array array = Array.CreateInstance(item, values.Count);
				for (int i = 0; i < values.Count; i++)
				{
					array.SetValue(values[i], i);
				}
				return array;
			}
			var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item))!;
			foreach (object? value in values)
			{
				list.Add(value);
			}
			return list;
		}
		return ReadObject(element, type, state);
	}

	private static object? ReadObject(JsonElement element, Type declared, CGameState state)
	{
		Type type = declared;
		if (element.TryGetProperty(TypeKey, out JsonElement named))
		{
			string name = named.GetString()!;
			type = typeof(CCommand).Assembly.GetType(name)
				?? throw new InvalidOperationException($"the recording names a type '{name}' this build does not have");
		}
		object value = RuntimeHelpers.GetUninitializedObject(type);
		foreach (FieldInfo field in Fields(type))
		{
			if (element.TryGetProperty(field.Name, out JsonElement entry))
			{
				field.SetValue(value, ReadValue(entry, field.FieldType, state));
			}
		}
		return value;
	}

	private static Script.CEventScope ReadScope(JsonElement element, CGameState state)
	{
		static CCountryTag? Tag(JsonElement entry) => entry.GetString() is { } text ? new CCountryTag(text) : null;
		var scope = Script.CEventScope.Start(Tag(element.GetProperty("country")), element.GetProperty("province").GetInt32(),
			Tag(element.GetProperty("from")), element.GetProperty("fromProvince").GetInt32(), element.GetProperty("seed").GetUInt32(),
			element.GetProperty("lookAhead").GetInt32());
		scope.RandomState = element.GetProperty("random").GetUInt32();
		if (element.TryGetProperty("faction", out JsonElement faction))
		{
			scope = scope.WithRebelFaction(Faction(state, faction.GetInt32()));
		}
		return scope;
	}

	private static Revolts.CRebelFaction Faction(CGameState state, int id)
	{
		return state.RebelFactions.FirstOrDefault(faction => faction.Id == id)
			?? throw new InvalidOperationException($"the recording names a rebel faction {id} the game does not have");
	}

	/// <summary>Every instance field of the type and its bases, but a command's place in the queue, which posting sets.</summary>
	private static IEnumerable<FieldInfo> Fields(Type type)
	{
		for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
		{
			foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.DeclaredOnly))
			{
				if (current == typeof(CCommand))
				{
					continue;
				}
				yield return field;
			}
		}
	}

	private static Type? ElementType(Type type)
	{
		if (type.IsArray)
		{
			return type.GetElementType();
		}
		return type.GetInterfaces().Append(type)
			.FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			?.GetGenericArguments()[0];
	}
}
