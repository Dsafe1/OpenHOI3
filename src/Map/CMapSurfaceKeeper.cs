using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gui;

namespace OpenHOI3.Map;

/// <summary>
/// The map's 3D surface, kept from one view of the map to the next: the main menu's and the lobby's worlds and the game Play
/// makes all draw the same map through the same files, so the surface made for the first is handed to the next rather than
/// made again - its meshes, the first technique's 2,700 materials and every map mode's were 3 seconds of a game's start, the
/// window not answering (<c>docs/mechanics/loadingscreen.md</c>, section 4). Held by the program, not by a view, as a child of
/// the node given, so a view freed does not take it along; a different map, interface or setting it was made with makes a
/// new one and frees the old. *Ours*: the original's surface is made by its map, which was not read for whether it outlives
/// a game.
/// </summary>
public sealed class CMapSurfaceKeeper
{
	private readonly Node _parent;
	private CMapSurface? _surface;

	/// <summary>What the kept surface was made from; another of any makes a new one.</summary>
	private (CMap Map, CGuiContext Gui, CMapCache Cache, IReadOnlyList<CQuadTile> Tiles, int Columns, bool Waves, bool Trees) _madeFrom;

	public CMapSurfaceKeeper(Node parent)
	{
		_parent = parent;
	}

	/// <summary>The borders' corners as the cache's three files give them, never handed out, and what they were read from.</summary>
	private CBorderCache? _cornersRead;
	private (CMapCache Cache, CMap Map)? _cornersFrom;

	/// <summary>The next view's copy, made on a thread of its own as soon as one is handed out.</summary>
	private System.Threading.Tasks.Task<CBorderCache>? _spareCorners;

	/// <summary>
	/// The borders' corners for a view of the map, its own to work out again: a copy of those read from the map cache once,
	/// where reading the three files again took 0.2 to 0.3 s on the main thread and a copy 0.15 s; null where the cache has
	/// none. The copy is made ahead, off the main thread, as the last is handed out - those read are never written to, so a
	/// copy may be made beside anything. Read again for another cache or map.
	/// </summary>
	public CBorderCache? Corners(CMapCache cache, CMap map)
	{
		Read(cache, map);
		if (_cornersRead is not { } read)
		{
			return null;
		}
		CBorderCache taken = _spareCorners?.Result ?? read.Copy();
		_spareCorners = System.Threading.Tasks.Task.Run(read.Copy);
		return taken;
	}

	/// <summary>
	/// The corners read and the first view's copy started, for the loading screen's thread to call before the first view is
	/// made: then the main thread only takes a copy already made, where it read the files and copied them itself, some
	/// 0.4 s of a still window (<c>docs/mechanics/loadingscreen.md</c>, section 3). Not beside a call of <see cref="Corners"/>.
	/// </summary>
	public void PrepareCorners(CMapCache cache, CMap map)
	{
		Read(cache, map);
		if (_cornersRead is { } read && _spareCorners is null)
		{
			_spareCorners = System.Threading.Tasks.Task.Run(read.Copy);
		}
	}

	/// <summary>The three files read once for a cache and a map, the copy made ahead forgotten for another.</summary>
	private void Read(CMapCache cache, CMap map)
	{
		if (_cornersFrom is not { } from || !ReferenceEquals(from.Cache, cache) || !ReferenceEquals(from.Map, map))
		{
			_cornersRead = CBorderCache.Load(cache, map.Width, map.Height, problem => GD.PushWarning($"map: {problem}"),
				map.IsWaterPixel);
			_cornersFrom = (cache, map);
			_spareCorners = null;
		}
	}

	/// <summary>How many surfaces were made and how many handed over, for a look at the log.</summary>
	public int Made { get; private set; }
	public int HandedOver { get; private set; }

	/// <summary>
	/// The surface for a view of the map, the view its holder: the one kept, wiped, where it was made from the same, else a new
	/// one, made at once - its meshes built as it enters the tree.
	/// </summary>
	public CMapSurface Take(object holder, CMap map, CGuiContext gui, CMapCache cache, IReadOnlyList<CQuadTile> tiles, int columns,
		bool simpleWater, bool waves, bool trees, CBorderCache? corners)
	{
		var wanted = (map, gui, cache, tiles, columns, waves, trees);
		if (_surface is { } kept && IsSame(_madeFrom, wanted))
		{
			kept.HandOver(holder, corners, simpleWater);
			HandedOver++;
			GD.Print($"Map surface handed over ({HandedOver} so far).");
			return kept;
		}
		Release();
		var made = new CMapSurface { Holder = holder };
		made.Setup(map, gui, cache, tiles, columns, simpleWater, waves, trees, corners);
		_surface = made;
		_madeFrom = wanted;
		_parent.AddChild(made);
		Made++;
		return made;
	}

	private static bool IsSame(
		(CMap Map, CGuiContext Gui, CMapCache Cache, IReadOnlyList<CQuadTile> Tiles, int Columns, bool Waves, bool Trees) a,
		(CMap Map, CGuiContext Gui, CMapCache Cache, IReadOnlyList<CQuadTile> Tiles, int Columns, bool Waves, bool Trees) b)
	{
		return ReferenceEquals(a.Map, b.Map) && ReferenceEquals(a.Gui, b.Gui) && ReferenceEquals(a.Cache, b.Cache)
			&& ReferenceEquals(a.Tiles, b.Tiles) && a.Columns == b.Columns && a.Waves == b.Waves && a.Trees == b.Trees;
	}

	/// <summary>The kept surface taken out of the tree, given back to the engine and freed; nothing once none is kept.</summary>
	public void Release()
	{
		if (_surface is not { } surface)
		{
			return;
		}
		_surface = null;
		if (surface.GetParent() is { } parent && surface.IsInsideTree())
		{
			parent.RemoveChild(surface);
		}
		surface.Release();
		surface.QueueFree();
	}
}
