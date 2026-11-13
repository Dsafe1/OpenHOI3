using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Map;

/// <summary>
/// The arrows along selected units' ways, drawn through the effect every arrow type draws through - the install's own
/// <c>gfx/fx/arrow.fx</c>, translated for the canvas - with each type's <c>bodytexture</c>: the band as a strip of two
/// vertices a cut, the first set of coordinates a cut's place along the arrow and the second the picture's column and
/// half, <c>CurrentProgress</c> the share of the way walked. Each arrow is a canvas item of its own, since each has its
/// own progress. See <c>docs/mechanics/movement.md</c>, section 7, and
/// <c>reversing/findings/FINDINGS-movement.md</c>, section 11.
/// </summary>
public sealed class CPathArrows
{
	private const string Technique = "tec0";
	private const string DrawnTexture = "tex0";

	/// <summary>The device's states around the map's objects (<c>FINDINGS-movement.md</c>, section 11.6).</summary>
	private static readonly Dictionary<string, string> States = new()
	{
		["ALPHABLENDENABLE"] = "1", ["SRCBLEND"] = "SRCALPHA", ["DESTBLEND"] = "INVSRCALPHA",
		["ALPHATESTENABLE"] = "1", ["ALPHAREF"] = "8", ["ALPHAFUNC"] = "GREATEREQUAL",
	};

	private readonly Shader? _shader;
	private readonly CGodotShader? _translation;
	private readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<(Rid Item, ShaderMaterial Material)> _items = new();
	private readonly List<ArrayMesh> _meshes = new();
	private Node2D? _parent;
	private int _used;

	public CPathArrows(CGuiContext gui, CArrowTypes arrows)
	{
		foreach (string name in new[] { CPlanMarkers.OwnArrow, CPlanMarkers.OtherArrow, CArrowTypes.Move, CArrowTypes.Attack, CArrowTypes.Retreat, CArrowTypes.Invasion, CArrowTypes.Strategic })
		{
			_textures[name] = arrows.Find(name) is { BodyTextureFile.Length: > 0 } arrow && gui.Textures.Exists(arrow.BodyTextureFile)
				? gui.Textures.LoadFromGamePath(arrow.BodyTextureFile)
				: null;
		}
		string file = arrows.EffectFile.Split('\\', '/').LastOrDefault(part => part.Length > 0) ?? "";
		(_shader, _translation) = file.Length == 0
			? (null, null)
			: gui.Effects.Get(file, Technique, EShaderTarget.Canvas, DrawnTexture, device: States);
		if (_shader is null)
		{
			CDiagnostics.Warn($"map: the arrows' effect '{arrows.EffectFile}' gives no '{Technique}', so no unit's way is drawn");
		}
	}

	/// <summary>Where the arrows' canvas items hang. Has to be given before the first is drawn.</summary>
	public void SetParent(Node2D parent) => _parent = parent;

	/// <summary>The picture an arrow type is drawn with, falling back to the march's; null where neither loads.</summary>
	public Texture2D? Texture(string type) => _textures.GetValueOrDefault(type) ?? _textures.GetValueOrDefault(CArrowTypes.Move);

	/// <summary>Takes every arrow away, ready to be drawn again.</summary>
	public void Clear()
	{
		for (int index = 0; index < _used; index++)
		{
			RenderingServer.CanvasItemClear(_items[index].Item);
		}
		_used = 0;
		_meshes.Clear();
	}

	/// <summary>
	/// One arrow: its band, its picture, the share of its way walked and the shifts it is drawn at - one copy of the map
	/// to each side as well as the middle one.
	/// </summary>
	public void Draw(List<CArrowSample> band, Texture2D texture, float walked, float mapWidth)
	{
		if (_shader is null || _translation is null || _parent is null || band.Count < 2)
		{
			return;
		}
		ArrayMesh mesh = Strip(band, _translation);
		_meshes.Add(mesh);
		(Rid item, ShaderMaterial material) = Next();
		foreach (CShaderUniform uniform in _translation.Uniforms.Where(u => u.FxName == "CurrentProgress" && !u.Shared))
		{
			material.SetShaderParameter(uniform.Name, walked);
		}
		for (int copy = -1; copy <= 1; copy++)
		{
			RenderingServer.CanvasItemAddMesh(item, mesh.GetRid(), new Transform2D(0f, new Vector2(copy * mapWidth, 0f)),
				Colors.White, texture.GetRid());
		}
	}

	/// <summary>Frees every canvas item; the arrows are gone.</summary>
	public void Free()
	{
		foreach ((Rid item, ShaderMaterial _) in _items)
		{
			RenderingServer.FreeRid(item);
		}
		_items.Clear();
		_meshes.Clear();
		_used = 0;
	}

	private (Rid Item, ShaderMaterial Material) Next()
	{
		if (_used == _items.Count)
		{
			Rid item = RenderingServer.CanvasItemCreate();
			RenderingServer.CanvasItemSetParent(item, _parent!.GetCanvasItem());
			RenderingServer.CanvasItemSetDrawIndex(item, _items.Count);
			RenderingServer.CanvasItemSetDefaultTextureFilter(item, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
			var material = new ShaderMaterial { Shader = _shader };
			RenderingServer.CanvasItemSetMaterial(item, material.GetRid());
			_items.Add((item, material));
		}
		return _items[_used++];
	}

	/// <summary>
	/// The band as the original's strip: for each cut its left edge and then its right, the left with the picture's
	/// upper half (v nought) and the right with its lower edge of that half (v a half); the first set of coordinates the
	/// cut's place along the arrow, the second its column and v.
	/// </summary>
	private static ArrayMesh Strip(List<CArrowSample> band, CGodotShader translation)
	{
		int count = band.Count * 2;
		var vertices = new Vector2[count];
		var progress = new Vector2[count];
		var picture = new Vector2[count];
		for (int cut = 0; cut < band.Count; cut++)
		{
			CArrowSample sample = band[cut];
			vertices[2 * cut] = new Vector2(sample.Left.X, sample.Left.Y);
			vertices[2 * cut + 1] = new Vector2(sample.Right.X, sample.Right.Y);
			progress[2 * cut] = progress[2 * cut + 1] = new Vector2(sample.Progress, 0f);
			picture[2 * cut] = new Vector2(sample.Along, 0f);
			picture[2 * cut + 1] = new Vector2(sample.Along, 0.5f);
		}
		using var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		Mesh.ArrayFormat format = 0;
		foreach (CShaderVertexInput input in translation.Inputs)
		{
			Vector2[]? values = input.Semantic switch
			{
				"TEXCOORD0" => progress,
				"TEXCOORD1" => picture,
				_ => null,
			};
			if (values is null)
			{
				continue;
			}
			if (input.Channel == "UV")
			{
				arrays[(int)Mesh.ArrayType.TexUV] = values;
			}
			else if (input.Channel is "CUSTOM0" or "CUSTOM1")
			{
				int slot = input.Channel[^1] - '0';
				var custom = new float[values.Length * 4];
				for (int v = 0; v < values.Length; v++)
				{
					custom[4 * v] = values[v].X;
					custom[4 * v + 1] = values[v].Y;
					custom[4 * v + 3] = 1f;
				}
				arrays[(int)Mesh.ArrayType.Custom0 + slot] = custom;
				format |= (Mesh.ArrayFormat)((long)Mesh.ArrayCustomFormat.RgbaFloat
					<< ((int)Mesh.ArrayFormat.FormatCustom0Shift + slot * (int)Mesh.ArrayFormat.FormatCustomBits));
			}
		}
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.TriangleStrip, arrays, null, null, format);
		return mesh;
	}
}
