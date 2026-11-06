using System.Collections.Generic;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// One river as the trace leaves it, which becomes one strip: its nodes from the source, and the frame of
/// <c>river_exit.dds</c> its mouth takes, or -1 for none - set only by river pixels of index 12 and up, which Their Finest
/// Hour's map has none of.
/// </summary>
public sealed class CRiverPiece
{
	public CRiverPiece(IReadOnlyList<CRiverNode> nodes, int exitFrame)
	{
		Nodes = nodes;
		ExitFrame = exitFrame;
	}

	public IReadOnlyList<CRiverNode> Nodes { get; }

	public int ExitFrame { get; }
}
