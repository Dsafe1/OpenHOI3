using System;
using System.Numerics;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The original's map camera for the 2D view's: hanging at (0, h, 0) over the origin, looking straight down with north up,
/// its height what makes the 2D zoom - from h it sees 2 h tan(0.785 / 2) of the map from the top of the screen to the
/// bottom - and the map moved under it, each tile's world matrix the tile's middle less the camera's place. A 2D point
/// (X, Y), north up and rows counting down, is the world's (X − 0.5, H − 0.5 − Y). See
/// <c>docs/mechanics/mapdraw.md</c>, section 3.
/// </summary>
public sealed class CMapSurfaceCamera
{
	/// <summary>The original's field of view, top to bottom [exe].</summary>
	public const float FieldOfView = CMapCamera.FieldOfView;

	/// <summary>Its near plane, and the far plane it has [exe].</summary>
	public const float Near = 8f;
	public const float Far = 1200f;

	/// <summary>How far past the camera's height the far plane is kept, where the 2D view goes higher than the original's.</summary>
	private const float FarMargin = 100f;

	/// <param name="centre">The 2D view's middle, in map pixels, rows counting down.</param>
	/// <param name="zoom">Screen pixels to a map pixel.</param>
	/// <param name="screen">The screen's size in pixels.</param>
	/// <param name="mapWidth">The map's width in pixels.</param>
	/// <param name="mapHeight">The map's height in pixels.</param>
	public CMapSurfaceCamera(Vector2 centre, float zoom, Vector2 screen, int mapWidth, int mapHeight)
	{
		MapWidth = mapWidth;
		Height = screen.Y / zoom / (2f * MathF.Tan(FieldOfView / 2f));
		X = centre.X - 0.5f;
		Z = mapHeight - 0.5f - centre.Y;
		View = CD3DMatrix.LookAtLH(new Vector3(0, Height, 0), Vector3.Zero, Vector3.UnitZ);
		Projection = CD3DMatrix.PerspectiveFovLH(FieldOfView, screen.X / screen.Y, Near, MathF.Max(Far, Height + FarMargin));
		ViewProjection = CD3DMatrix.Multiply(View, Projection);
	}

	public int MapWidth { get; }

	/// <summary>The camera's height.</summary>
	public float Height { get; }

	/// <summary>Where it hangs over the map, in the world's x and z.</summary>
	public float X { get; }
	public float Z { get; }

	public float[] View { get; }
	public float[] Projection { get; }
	public float[] ViewProjection { get; }

	/// <summary>
	/// A tile's world matrix: its middle less the camera's place, the copy of the map a width east or west taken where it
	/// is the nearer - the original's wrap. Ours as to which copy: the nearest, which is the one on screen.
	/// </summary>
	public float[] World(float middleX, float middleZ)
	{
		return CD3DMatrix.Translation(Wrap(middleX - X), 0, middleZ - Z);
	}

	/// <summary>
	/// The view with the camera where it hangs over the map rather than over the origin. Ours: with
	/// <see cref="WorldAt"/>, the product a tile is drawn through is the original's - World × View is the same translation
	/// followed by the same look - while the view is one matrix for every tile and a tile's own changes only where it
	/// crosses the seam, so moving the camera sets one matrix and not a thousand (<c>docs/mechanics/mapdraw.md</c>, section 3).
	/// </summary>
	public float[] ViewAt => CD3DMatrix.LookAtLH(new Vector3(X, Height, Z), new Vector3(X, 0, Z), Vector3.UnitZ);

	/// <summary>A tile's world matrix to go with <see cref="ViewAt"/>: its middle, moved a map's width where that is nearer.</summary>
	public float[] WorldAt(float middleX, float middleZ)
	{
		return CD3DMatrix.Translation(middleX + Shift(middleX), 0, middleZ);
	}

	/// <summary>How far a tile is moved east or west to the copy of the map nearest the camera: nought or a map's width.</summary>
	public float Shift(float middleX)
	{
		return Wrap(middleX - X) - (middleX - X);
	}

	/// <summary>A distance east of the camera, a map's width added or taken off to bring it nearest.</summary>
	public float Wrap(float dx)
	{
		if (dx > MapWidth / 2f)
		{
			return dx - MapWidth;
		}
		return dx < -MapWidth / 2f ? dx + MapWidth : dx;
	}
}
