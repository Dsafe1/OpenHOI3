using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Map;

/// <summary>
/// A tile of the map's surface as drawn: the quad cache's tile, its land's material, its beaches' and its land borders'
/// where it has any, and how far east or west its world matrix was last moved to the copy of the map nearest the camera.
/// </summary>
public sealed class CSurfaceTile
{
	public CSurfaceTile(CQuadTile tile, ShaderMaterial land, ShaderMaterial? beach, ShaderMaterial? border)
	{
		Tile = tile;
		Land = land;
		Beach = beach;
		Border = border;
	}

	public CQuadTile Tile { get; }

	public ShaderMaterial Land { get; }

	public ShaderMaterial? Beach { get; }

	/// <summary>The land borders' material, which a texture of the land's name means another thing to, so set apart.</summary>
	public ShaderMaterial? Border { get; }

	/// <summary>Nought, or a map's width either way.</summary>
	public float Shift { get; set; }

	public IEnumerable<ShaderMaterial> Materials
	{
		get
		{
			yield return Land;
			if (Beach is not null)
			{
				yield return Beach;
			}
		}
	}
}
