using System.Collections.Generic;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A river's mesh: one triangle strip, six vertices for each pair of nodes, and its mouth's quad - a strip of four - where
/// it has an exit frame, else null.
/// </summary>
public sealed class CRiverStrip
{
	public CRiverStrip(IReadOnlyList<CRiverVertex> vertices, IReadOnlyList<CRiverVertex>? exit)
	{
		Vertices = vertices;
		Exit = exit;
	}

	public IReadOnlyList<CRiverVertex> Vertices { get; }

	public IReadOnlyList<CRiverVertex>? Exit { get; }

	/// <summary>A strip's triangles as a list, (i, i + 1, i + 2) for each, the degenerate ones kept: the draw culls nothing.</summary>
	public static IEnumerable<int> StripTriangles(int count)
	{
		for (int i = 0; i + 2 < count; i++)
		{
			yield return i;
			yield return i + 1;
			yield return i + 2;
		}
	}
}
