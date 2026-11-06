using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Map;

/// <summary>
/// A tile of the map's surface as drawn: the quad cache's tile; its land's and its beaches' meshes, made once; a material for
/// each of the four ways the surface draws them - the terrain modes near and far, the others near and far - made the first
/// time that way is wanted, so that a change of height or of mode shows another set of instances instead of giving 2,700
/// materials another shader; its land borders' material; and how far east or west its world matrix was last moved to the
/// copy of the map nearest the camera.
/// </summary>
public sealed class CSurfaceTile
{
	public const int Ways = 4;

	public CSurfaceTile(CQuadTile tile, ArrayMesh land, ArrayMesh? beach, ShaderMaterial? border)
	{
		Tile = tile;
		LandMesh = land;
		BeachMesh = beach;
		Border = border;
	}

	public CQuadTile Tile { get; }

	public ArrayMesh LandMesh { get; }

	public ArrayMesh? BeachMesh { get; }

	/// <summary>The land's material for each way it is drawn, or null for a way not yet wanted.</summary>
	public ShaderMaterial?[] Land { get; } = new ShaderMaterial?[Ways];

	public ShaderMaterial?[] Beach { get; } = new ShaderMaterial?[Ways];

	/// <summary>The land borders' material, which a texture of the land's name means another thing to, so set apart.</summary>
	public ShaderMaterial? Border { get; }

	/// <summary>Nought, or a map's width either way.</summary>
	public float Shift { get; set; }

	/// <summary>The land's and the beaches' materials of one way, where made.</summary>
	public IEnumerable<ShaderMaterial> Materials(int way)
	{
		if (Land[way] is { } land)
		{
			yield return land;
		}
		if (Beach[way] is { } beach)
		{
			yield return beach;
		}
	}
}
