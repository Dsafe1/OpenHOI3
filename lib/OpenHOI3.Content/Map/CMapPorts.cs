using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Which land provinces have a port, onto which sea province each opens, and the number
/// every port and every sea province is known by in the table of distances by sea. The
/// rule is the original's, in <c>docs/mechanics/fleetsandair.md</c>, section 2:
///
///     a land province has a port where the pixel its naval_base position falls on
///     belongs to a sea province that is not a lake; that sea province is the one its
///     ships put out into;
///     the ports are numbered first, in the order positions.txt names them, and then
///     every sea province that is not a lake, in the order of its id.
///
/// A lake is a sea province none of whose neighbours is sea, or one continent.txt
/// lists under <c>lake</c>.
/// </summary>
public static class CMapPorts
{
	/// <param name="PortSea">Per province id, the sea province its port opens onto, or 0.</param>
	/// <param name="SeaIndex">Per province id, its number in the table of distances by sea, or -1.</param>
	/// <param name="Count">How many provinces have such a number.</param>
	public readonly record struct Result(int[] PortSea, int[] SeaIndex, int Count);

	/// <summary>The continent of <c>continent.txt</c> whose provinces are lakes whatever they touch.</summary>
	public const string LakeContinent = "lake";

	public static Result Build(int provinceCount, Func<int, bool> isSea, Func<int, int, int> provinceAt,
		IReadOnlyList<CProvinceEdge[]> edges, IReadOnlyList<CPortPlacement> ports, IReadOnlyList<string> continents)
	{
		bool IsLake(int id)
		{
			return isSea(id) && (continents[id] == LakeContinent || TouchesNoSea(id, isSea, edges));
		}

		var portSea = new int[provinceCount];
		var seaIndex = new int[provinceCount];
		Array.Fill(seaIndex, -1);
		int count = 0;

		foreach (CPortPlacement port in ports)
		{
			if (port.Province <= 0 || port.Province >= provinceCount)
			{
				continue;
			}
			int under = provinceAt((int)port.X, (int)port.Y);
			if (under <= 0 || under >= provinceCount || !isSea(under) || IsLake(under))
			{
				continue;
			}
			portSea[port.Province] = under;
			seaIndex[port.Province] = count++;
		}
		for (int id = 1; id < provinceCount; id++)
		{
			if (seaIndex[id] < 0 && isSea(id) && !IsLake(id))
			{
				seaIndex[id] = count++;
			}
		}
		return new Result(portSea, seaIndex, count);
	}

	/// <summary>Whether a province touches no sea province.</summary>
	public static bool TouchesNoSea(int id, Func<int, bool> isSea, IReadOnlyList<CProvinceEdge[]> edges)
	{
		foreach (CProvinceEdge edge in edges[id])
		{
			if (isSea(edge.To))
			{
				return false;
			}
		}
		return true;
	}
}
