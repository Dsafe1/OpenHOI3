using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Map;

/// <summary>
/// *Ours*: the map surface's meshes hidden where the camera does not see them. Every mesh of the surface is drawn through
/// the original's effects, which place its vertices themselves, so Godot cannot tell what is on screen and draws them all -
/// some ten thousand a frame, a tile's land, beaches, borders, water borders, rivers and surf, wherever the camera is
/// (<c>docs/performance.md</c>, section 2). Each is given here the rectangle of the map it covers, in the world's x and z,
/// and the middle its copy of the map is chosen by (<see cref="CMapSurfaceCamera.Shift"/>), and shown only where that
/// rectangle, moved to the copy, meets what the camera sees. The camera hangs straight over the map, so it sees a
/// rectangle of the ground; what stands above the ground is drawn further from the middle of the screen than its foot,
/// never nearer, so a mesh whose ground lies outside that rectangle is off the screen however high it stands.
///
/// A mesh is hidden by taking its render layers away, not by hiding the node: hiding takes it out of the rendering
/// server's tree of bounds and showing puts it back, and with every mesh's bounds the whole world, a zoom outwards that
/// showed thousands of them drew a second of frames at a quarter of their rate (measured 2026-11-19). A mesh with no
/// layers stays in the tree and the camera's mask passes it by.
/// </summary>
public sealed class CMapCulling
{
	/// <summary>Map pixels beyond the edges of the view a mesh is still drawn within, for the half pixels and rounding.</summary>
	private const float Margin = 4f;

	private readonly List<Entry> _entries = new();

	private CMapSurfaceCamera? _camera;
	private float _aspect = 16f / 9f;

	/// <summary>
	/// How many frames a mesh is left on its layers after it is made, before it may be hidden: drawn once as it was before
	/// culling, the first zoom outwards then ran at 122 to 123 frames a second, its longest frame 16 ms, and without at 99
	/// to 117, one frame 64 ms (measured 2026-11-19, twice each).
	/// </summary>
	private const int WarmFrames = 3;

	/// <summary>Frames counted since the surface began.</summary>
	private long _frame;

	/// <summary>The meshes still within their first frames.</summary>
	private readonly List<Entry> _warming = new();

	/// <summary>Once a frame: the meshes whose first frames are over shown or hidden as the camera wants.</summary>
	public void Frame()
	{
		_frame++;
		for (int i = _warming.Count - 1; i >= 0; i--)
		{
			Entry entry = _warming[i];
			if (entry.WarmUntil <= _frame)
			{
				_warming.RemoveAt(i);
				if (_camera is { } camera)
				{
					Show(entry, camera);
				}
			}
		}
	}

	/// <summary>Whether meshes are hidden at all: <c>--nocull</c> draws every one, as before 2026-11-19.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>How far apart in Godot's sorting of transparent meshes two meshes added one after the other are put.</summary>
	private const float SortingStep = 0.001f;

	/// <summary>
	/// A mesh covering x0..x1 and z0..z1 of the map, drawn at the copy of the map its middle x is nearest. It is also given
	/// its place among the meshes of its priority, the order it was added in: every mesh has the same bounds, so Godot has
	/// no depth to sort the transparent ones by, and its sort, which keeps no order among equals, drew overlapping rivers,
	/// borders and surf in an order that changed with how many were drawn - with what was hidden, and so as the camera
	/// moved. Added in the tiles' order, they are drawn in it, as the original draws its tiles.
	/// </summary>
	public void Add(MeshInstance3D instance, float x0, float z0, float x1, float z1, float middleX)
	{
		instance.SortingOffset = -_entries.Count * SortingStep;
		var entry = new Entry(instance, x0, z0, x1, z1, middleX) { WarmUntil = _frame + WarmFrames };
		_entries.Add(entry);
		_warming.Add(entry);
	}

	/// <summary>
	/// A tile's mesh whose vertices are the tile's own, x and z from its middle, every <paramref name="stride"/> values of
	/// <paramref name="positions"/> beginning with them: the land's, the beaches', the land borders'. What it covers is
	/// what its vertices reach, which is not the tile's square - a tile's land is the whole of the provinces it was given.
	/// </summary>
	public void AddTile<T>(MeshInstance3D instance, (int X, int Y) middle, T[] positions, int stride) where T : System.Numerics.INumber<T>
	{
		float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
		for (int i = 0; i + 1 < positions.Length; i += stride)
		{
			float x = float.CreateTruncating(positions[i]);
			float z = float.CreateTruncating(positions[i + 1]);
			x0 = MathF.Min(x0, x);
			x1 = MathF.Max(x1, x);
			z0 = MathF.Min(z0, z);
			z1 = MathF.Max(z1, z);
		}
		if (x0 > x1)
		{
			x0 = x1 = z0 = z1 = 0;
		}
		Add(instance, middle.X + x0, middle.Y + z0, middle.X + x1, middle.Y + z1, middle.X);
	}

	/// <summary>Each mesh shown or hidden for where the camera now is.</summary>
	public void Apply(CMapSurfaceCamera camera, Vector2 screen)
	{
		_camera = camera;
		_aspect = screen.X / MathF.Max(1f, screen.Y);
		foreach (Entry entry in _entries)
		{
			Show(entry, camera);
		}
	}

	private void Show(Entry entry, CMapSurfaceCamera camera)
	{
		bool seen = !Enabled || entry.WarmUntil > _frame || Sees(camera, entry);
		uint layers = seen ? entry.Layers : 0;
		if (GodotObject.IsInstanceValid(entry.Instance) && entry.Instance.Layers != layers)
		{
			entry.Instance.Layers = layers;
		}
	}

	private bool Sees(CMapSurfaceCamera camera, Entry entry)
	{
		float halfZ = camera.Height * MathF.Tan(CMapSurfaceCamera.FieldOfView / 2f) + Margin;
		float halfX = (halfZ - Margin) * _aspect + Margin;
		float shift = camera.Shift(entry.MiddleX);
		return entry.X1 + shift >= camera.X - halfX && entry.X0 + shift <= camera.X + halfX
			&& entry.Z1 >= camera.Z - halfZ && entry.Z0 <= camera.Z + halfZ;
	}

	private sealed record Entry(MeshInstance3D Instance, float X0, float Z0, float X1, float Z1, float MiddleX)
	{
		/// <summary>The frame before which it is shown whatever the camera, being new.</summary>
		public long WarmUntil { get; init; }

		/// <summary>The render layers it is drawn on where it is seen; none where it is not.</summary>
		public uint Layers { get; init; } = Instance.Layers;
	}
}
