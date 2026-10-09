using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim.Provinces;

/// <summary>
/// Checks the distances by sea against the cache the original keeps of its own:
///
///   OpenHOI3.SaveCheck navaldist &lt;install folder&gt; [navaldist.bin]
///
/// The original works them out once and writes them to <c>map/cache/navaldist.bin</c>:
/// sixteen bits for every pair of places, the half of the table on and above its
/// diagonal, row by row. The file is read and nothing of it is kept. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 3.
/// </summary>
public static class CNavalDistanceCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck navaldist <install folder> [navaldist.bin]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CMap map = CMap.Load(directories, warnings.Add);
		string path = args.Length > 2 ? args[2] : directories.Resolve("map/cache/navaldist.bin");
		byte[] bytes = File.ReadAllBytes(path);

		long cells = bytes.Length / 2;
		long theirCount = ((long)Math.Sqrt(8.0 * cells + 1) - 1) / 2;
		Console.WriteLine($"cache: {path}, {bytes.Length} bytes: {cells} cells, which is a table of {theirCount}");

		int ports = 0;
		int seas = 0;
		int lakes = 0;
		for (int id = 1; id < map.ProvinceCount; id++)
		{
			if (map.SeaIndex[id] >= 0)
			{
				if (map.IsSea(id))
				{
					seas++;
				}
				else
				{
					ports++;
				}
			}
			else if (map.IsSea(id))
			{
				lakes++;
			}
		}
		Console.WriteLine($"ours: a table of {map.SeaIndexCount}: {ports} ports and {seas} sea provinces; {lakes} lakes left out");
		if (theirCount != map.SeaIndexCount || theirCount * (theirCount + 1) / 2 != cells)
		{
			Console.WriteLine("the two tables are not of one size, and nothing more can be compared");
			return 1;
		}

		var provinces = new CProvince[map.ProvinceCount];
		for (int id = 0; id < provinces.Length; id++)
		{
			provinces[id] = new CProvince(id, "", map.IsSea(id))
			{
				Edges = map.Edges[id],
				CentreX = map.Shapes[id].IsEmpty ? 0 : map.Shapes[id].CentreX,
				CentreY = map.Shapes[id].IsEmpty ? 0 : map.Shapes[id].CentreY,
				PortSea = map.PortSea[id],
				SeaIndex = map.SeaIndex[id],
			};
		}
		var distances = new CNavalDistances(provinces, map.Width);

		int count = map.SeaIndexCount;
		long same = 0;
		long compared = 0;
		long nothing = 0;
		var examples = new List<string>();
		for (int from = 0; from < count; from++)
		{
			ushort[] row = distances.Row(from);
			long start = (long)from * count - (long)from * (from + 1) / 2;
			for (int to = from; to < count; to++)
			{
				ushort theirs = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan((int)((start + to) * 2)));
				compared++;
				if (theirs == 0)
				{
					nothing++;
				}
				if (theirs == row[to])
				{
					same++;
				}
				else if (examples.Count < 12)
				{
					examples.Add($"{distances.ProvinceOf(from)} to {distances.ProvinceOf(to)}: ours {row[to]}, theirs {theirs}");
				}
			}
		}
		Console.WriteLine($"distances: {same} of {compared} agree; {nothing} of theirs are nothing");
		foreach (string example in examples)
		{
			Console.WriteLine("  " + example);
		}
		foreach (string warning in warnings)
		{
			Console.WriteLine("warning: " + warning);
		}
		return same == compared ? 0 : 1;
	}
}
