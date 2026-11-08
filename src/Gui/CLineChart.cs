using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// A <c>LineChartType</c>: lines through the install's own <c>gfx/FX/linechart.fx</c>, translated as every effect is. The effect works out where each vertex goes from its table of
/// heights, so it draws in a 3D viewport of its own, whose picture stands in the interface. See <c>docs/mechanics/strategicwarfare.md</c>, section 5.
/// </summary>
public sealed partial class CLineChart : Control
{
	private const string Effect = "linechart.fx";
	private const string Technique = "BarShader";

	/// <summary>How many points the effect's table holds.</summary>
	private const int MostPoints = 40;

	/// <summary>The viewport is this many times the chart's size, for the edges of a line.</summary>
	private const int Sampling = 2;

	private readonly List<MeshInstance3D> _lines = new();
	private CGuiContext _context = null!;
	private CSpriteType _sprite = null!;
	private Vector2 _size;
	private SubViewport? _viewport;

	public void Setup(CGuiContext context, CSpriteType sprite)
	{
		_context = context;
		_sprite = sprite;
		_size = sprite.TileSize;
		Size = _size;
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_viewport = new SubViewport
		{
			Size = new Vector2I((int)_size.X * Sampling, (int)_size.Y * Sampling),
			TransparentBg = true,
			OwnWorld3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
			RenderTargetClearMode = SubViewport.ClearMode.Always,
		};
		_viewport.AddChild(new Camera3D { Current = true });
		AddChild(_viewport);
		var picture = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
			Texture = _viewport.GetTexture(),
		};
		AddChild(picture);
		picture.Size = _size;
	}

	/// <summary>The chart drawn again: for each line its heights, nought to a hundred, and its colour.</summary>
	public void Show(IReadOnlyList<(IReadOnlyList<int> Heights, Color Colour)> lines)
	{
		if (_viewport is null)
		{
			return;
		}
		foreach (MeshInstance3D line in _lines)
		{
			line.QueueFree();
		}
		_lines.Clear();
		(Shader? shader, CGodotShader? translation) = _context.Effects.Get(Effect, Technique, EShaderTarget.Spatial);
		if (shader is null || translation is null)
		{
			CDiagnostics.Warn($"interface: gfx/FX/{Effect} has no '{Technique}' to draw the line chart '{_sprite.Name}' with");
			return;
		}
		foreach ((IReadOnlyList<int> heights, Color colour) in lines)
		{
			if (heights.Count >= 2 && heights.Count <= MostPoints && Line(heights, colour, shader, translation) is { } mesh)
			{
				_lines.Add(mesh);
				_viewport.AddChild(mesh);
			}
		}
		_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
	}

	private MeshInstance3D Line(IReadOnlyList<int> heights, Color colour, Shader shader, CGodotShader translation)
	{
		var material = new CProgressBarMaterial(new ShaderMaterial { Shader = shader }, translation);
		int points = heights.Count;
		Vector4[] table = new Vector4[MostPoints];
		for (int point = 0; point < MostPoints; point++)
		{
			float height = point < points ? heights[point] / 100f : 0f;
			table[point] = new Vector4(colour.R, colour.G, colour.B, height);
		}
		material.Set("vColorHeight", table);
		material.Set("bIsOutline", _sprite.Outline);
		material.Set("vScreenPosWidthHeight", new Vector4(0f, 0f, _size.X, _size.Y));
		material.Set("vHalfLineWidthScale", new Vector2(_sprite.LineWidth * 0.5f, _size.X / points));
		// What the effect's position is multiplied by: a point of the chart to the device's coordinates.
		material.Set("InverseView", new Projection(new Vector4(2f / _size.X, 0f, 0f, 0f), new Vector4(0f, -2f / _size.Y, 0f, 0f),
			new Vector4(0f, 0f, 1f, 0f), new Vector4(-1f, 1f, 0f, 1f)));
		return new MeshInstance3D { Mesh = Segments(points), MaterialOverride = material.Material };
	}

	/// <summary>
	/// The original's vertices, two triangles a segment: each gives the point it stands on, the way to the point it joins, which side of the line it is on and whether it is the
	/// line's outline.
	/// </summary>
	private static ArrayMesh Segments(int points)
	{
		int[][] corners =
		{
			new[] { 0, 1, 1, 1 }, new[] { 1, -1, 1, 0 }, new[] { 0, 1, -1, 0 },
			new[] { 1, -1, 1, 0 }, new[] { 1, -1, -1, 1 }, new[] { 0, 1, 1, 1 },
		};
		int segments = points - 1;
		var positions = new Vector3[segments * corners.Length];
		var custom = new float[positions.Length * 4];
		for (int segment = 0; segment < segments; segment++)
		{
			for (int corner = 0; corner < corners.Length; corner++)
			{
				int at = (segment * corners.Length + corner) * 4;
				custom[at] = segment + corners[corner][0];
				custom[at + 1] = corners[corner][1];
				custom[at + 2] = corners[corner][2];
				custom[at + 3] = corners[corner][3];
			}
		}
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = positions;
		arrays[(int)Mesh.ArrayType.Custom0] = custom;
		var mesh = new ArrayMesh();
		var format = (Mesh.ArrayFormat)((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift) | Mesh.ArrayFormat.FormatCustom0;
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, null, null, format);
		// The effect places every vertex itself, so nothing about where the mesh is may leave it out of the picture.
		mesh.CustomAabb = new Aabb(new Vector3(-10000f, -10000f, -10000f), new Vector3(20000f, 20000f, 20000f));
		return mesh;
	}
}
