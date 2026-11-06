using System.Numerics;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>The original's map camera set to the 2D view's, as <c>docs/mechanics/mapdraw.md</c>, section 3, has it.</summary>
public class MapSurfaceCameraTests
{
	/// <summary>Where a world point lands on a screen, in pixels from its top left, through a tile's matrices.</summary>
	private static Vector2 OnScreen(CMapSurfaceCamera camera, float[] world, Vector3 local, Vector2 screen)
	{
		float[] all = CD3DMatrix.Multiply(world, camera.ViewProjection);
		Vector4 clip = CD3DMatrix.Transform(new Vector4(local, 1), all);
		return new Vector2((clip.X / clip.W + 1) / 2 * screen.X, (1 - clip.Y / clip.W) / 2 * screen.Y);
	}

	[Fact]
	public void A_point_on_the_land_lands_where_the_2D_view_draws_it()
	{
		var screen = new Vector2(1920, 1080);
		const float zoom = 1.43f;
		var centre = new Vector2(2900, 559.5f);
		var camera = new CMapSurfaceCamera(centre, zoom, screen, 5616, 2160);

		// The 2D point (3000, 500), a hundred pixels east and 59.5 north of the middle: the world's (2999.5, 1659.5),
		// on the land's height 0.35, which the camera's height makes all but nothing.
		float[] world = CD3DMatrix.Translation(2999.5f - camera.X, 0, 1659.5f - camera.Z);
		Vector2 at = OnScreen(camera, world, new Vector3(0, 0, 0), screen);

		Assert.Equal(960 + 100 * zoom, at.X, 0.5f);
		Assert.Equal(540 - 59.5f * zoom, at.Y, 0.5f);
		// From that height the camera sees the screen's height in map pixels, as the 2D view does.
		Assert.Equal(1080 / zoom, 2 * camera.Height * System.MathF.Tan(CMapSurfaceCamera.FieldOfView / 2), 0.01f);
	}

	[Fact]
	public void A_tile_across_the_seam_is_moved_a_maps_width_to_the_nearer_copy()
	{
		var camera = new CMapSurfaceCamera(new Vector2(5600, 1000), 1f, new Vector2(1920, 1080), 5616, 2160);

		float[] world = camera.World(25, 1000);

		// The tile at the west edge is drawn a map's width east, beside the camera at the east edge.
		Assert.Equal(25 + 5616 - camera.X, world[12], 0.01f);
		// The view at the camera's place with the tile's own middle moved is the same product.
		float[] original = CD3DMatrix.Multiply(world, camera.View);
		float[] moved = CD3DMatrix.Multiply(camera.WorldAt(25, 1000), camera.ViewAt);
		for (int i = 0; i < 16; i++)
		{
			Assert.Equal(original[i], moved[i], 0.01f);
		}
		// And the far plane is kept past the camera where the 2D view is higher than the original's 1000.
		var high = new CMapSurfaceCamera(new Vector2(2808, 1080), 1080 / 2160f / 1.2f, new Vector2(1920, 1080), 5616, 2160);
		Assert.True(high.Height > 2000);
	}
}
