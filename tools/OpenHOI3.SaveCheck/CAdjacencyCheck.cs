using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim.Provinces;

/// <summary>
/// Checks the ways between provinces against the cache the original keeps of its own:
///
///   OpenHOI3.SaveCheck adjacency &lt;install folder&gt; [adjacencies.bin]
///
/// The original works the edges out once and writes them to <c>map/cache/adjacencies.bin</c>:
/// for each province id from 0 a count, then that many records of five 32 bit numbers -
/// kind, neighbour, province crossed, distance in thousandths, bearing. The file is read
/// and nothing of it is kept. See <c>docs/mechanics/movement.md</c>, section 2.
/// </summary>
public static class CAdjacencyCheck
{
	private const int RecordSize = 20;

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck adjacency <install folder> [adjacencies.bin]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CMap map = CMap.Load(directories, warnings.Add);
		string path = args.Length > 2 ? args[2] : directories.Resolve("map/cache/adjacencies.bin");
		byte[] bytes = File.ReadAllBytes(path);
		Console.WriteLine($"cache: {path}, {bytes.Length} bytes");

		var theirs = new List<(int Kind, int To, int Through, int Distance)>[map.ProvinceCount];
		int at = 0;
		int total = 0;
		for (int id = 0; id < map.ProvinceCount && at + 4 <= bytes.Length; id++)
		{
			int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
			at += 4;
			theirs[id] = new List<(int, int, int, int)>(count);
			for (int i = 0; i < count; i++, at += RecordSize)
			{
				ReadOnlySpan<byte> record = bytes.AsSpan(at, RecordSize);
				theirs[id].Add((BinaryPrimitives.ReadInt32LittleEndian(record),
					BinaryPrimitives.ReadInt32LittleEndian(record[4..]),
					BinaryPrimitives.ReadInt32LittleEndian(record[8..]),
					BinaryPrimitives.ReadInt32LittleEndian(record[12..])));
				total++;
			}
		}
		Console.WriteLine($"theirs: {total} edges, {bytes.Length - at} bytes left over");

		int ours = 0;
		int sameSet = 0;
		int sameOrder = 0;
		int provinces = 0;
		var compared = new SortedDictionary<string, int>();
		var wrong = new SortedDictionary<string, int>();
		var examples = new Dictionary<string, List<string>>();
		void Count(string what, bool agrees, Func<string> detail)
		{
			compared[what] = compared.GetValueOrDefault(what) + 1;
			if (agrees)
			{
				return;
			}
			wrong[what] = wrong.GetValueOrDefault(what) + 1;
			if (!examples.TryGetValue(what, out List<string>? list))
			{
				examples[what] = list = new List<string>();
			}
			if (list.Count < 6)
			{
				list.Add(detail());
			}
		}

		for (int id = 1; id < map.ProvinceCount; id++)
		{
			CProvinceEdge[] mine = map.Edges[id];
			List<(int Kind, int To, int Through, int Distance)> other = theirs[id] ?? new();
			ours += mine.Length;
			if (mine.Length == 0 && other.Count == 0)
			{
				continue;
			}
			provinces++;

			var mineTo = new HashSet<int>();
			foreach (CProvinceEdge edge in mine)
			{
				mineTo.Add(edge.To);
			}
			var otherTo = new HashSet<int>();
			foreach ((int _, int to, int _, int _) in other)
			{
				otherTo.Add(to);
			}
			bool set = mineTo.SetEquals(otherTo);
			sameSet += set ? 1 : 0;
			if (!set && Remember(examples, "the neighbours of a province"))
			{
				examples["the neighbours of a province"].Add(
					$"{id}: ours {string.Join(" ", mineTo)} | theirs {string.Join(" ", otherTo)}");
			}

			bool order = mine.Length == other.Count;
			for (int i = 0; order && i < mine.Length; i++)
			{
				order = mine[i].To == other[i].To;
			}
			sameOrder += order ? 1 : 0;
			if (set && !order && Remember(examples, "the order of a province's edges"))
			{
				var a = new List<int>();
				var b = new List<int>();
				foreach (CProvinceEdge edge in mine) { a.Add(edge.To); }
				foreach ((int _, int to, int _, int _) in other) { b.Add(to); }
				examples["the order of a province's edges"].Add(
					$"{id}: ours {string.Join(" ", a)} | theirs {string.Join(" ", b)}");
			}

			foreach ((int kind, int to, int through, int distance) in other)
			{
				CProvinceEdge? match = null;
				foreach (CProvinceEdge edge in mine)
				{
					if (edge.To == to)
					{
						match = edge;
						break;
					}
				}
				if (match is not CProvinceEdge found)
				{
					continue;
				}
				string ends = (map.IsSea(id) ? "sea" : "land") + " to " + (map.IsSea(to) ? "sea" : "land");
				Count($"kind, {ends}", (int)found.Kind == kind,
					() => $"{id} -> {to}: ours {(int)found.Kind}, theirs {kind}");
				Count($"province crossed, {ends}", found.Through == through,
					() => $"{id} -> {to}: ours {found.Through}, theirs {through}");
				Count($"distance, {ends}", found.Distance.Raw == distance,
					() => $"{id} -> {to}: ours {found.Distance.Raw}, theirs {distance} " +
						$"(from {map.Positions[id]} to {map.Positions[to]})");
			}
		}

		Console.WriteLine($"ours:   {ours} edges");
		Console.WriteLine($"provinces with edges: {provinces}; the same neighbours in {sameSet}, in the same order in {sameOrder}");
		foreach ((string what, int count) in compared)
		{
			int bad = wrong.GetValueOrDefault(what);
			Console.WriteLine($"  {what,-34} {count - bad,7} of {count} agree");
		}
		foreach ((string what, List<string> list) in examples)
		{
			Console.WriteLine();
			Console.WriteLine(what + ":");
			foreach (string line in list)
			{
				Console.WriteLine("  " + line);
			}
		}
		Console.WriteLine();
		Console.WriteLine($"{warnings.Count} warnings while loading");
		return 0;
	}

	private static bool Remember(Dictionary<string, List<string>> examples, string what)
	{
		if (!examples.TryGetValue(what, out List<string>? list))
		{
			examples[what] = list = new List<string>();
		}
		return list.Count < 6;
	}
}
