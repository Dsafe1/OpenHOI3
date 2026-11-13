using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.BattlePlans;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.BattlePlans;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Map;

/// <summary>
/// Battle plans on the surface, through the install's <c>battlearrow.fx</c> and <c>mapinfo.fx</c>: each arrow, border and
/// icon a mesh of its own, drawn where it is and a map's width east. See <c>docs/mechanics/battleplans.md</c>, section 11.
/// </summary>
public partial class CMapSurface
{
	public const string BattleArrowFile = "battlearrow.fx";
	public const string BattleArrowTechnique = "BattleArrow";
	public const string MapInfoFile = "mapinfo.fx";
	public const string MapInfoTechnique = "MapInfo";

	/// <summary>
	/// Drawn after the hierarchy's lines: the arrows a colour at a time in the palette's order, each colour's fills and then
	/// its outlines, and the icons over every arrow (<c>battleplans.md</c>, section 11).
	/// </summary>
	private const int PlanPriority = 9;

	/// <summary>How many colours have a stencil of their own; a colour further on shares the last one's.</summary>
	private const int PlanStencilLayers = 50;

	private const int IconPriority = PlanPriority + 2 * PlanStencilLayers;

	/// <summary>Which of the original's colour lists, each with its stencil cleared, a colour is drawn in.</summary>
	private static int PlanLayer(int colour) => Math.Clamp(colour, 0, PlanStencilLayers - 1);

	private static readonly Dictionary<string, string> PlanStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"), ("DESTBLEND", "INVSRCALPHA"));

	private sealed class CPlanDrawn
	{
		public int Signature;
		public ArrayMesh? Mesh;
		public readonly List<MeshInstance3D> Instances = new();

		/// <summary>The materials for each copy: one, or a first pass with its second.</summary>
		public readonly List<(ShaderMaterial Material, CGodotShader? Translation)> Materials = new();
	}

	private readonly Dictionary<object, CPlanDrawn> _planDrawn = new();
	/// <summary>The arrow's two passes for each colour's stencil layer.</summary>
	private readonly Dictionary<int, (Shader? Shader, CGodotShader? Translation)[]> _arrowEffects = new();
	private (Shader? Shader, CGodotShader? Translation)? _iconEffect;
	private readonly Dictionary<string, Texture2D?> _planTextures = new(StringComparer.OrdinalIgnoreCase);

	private Texture2D? PlanTexture(string path)
	{
		if (!_planTextures.TryGetValue(path, out Texture2D? texture))
		{
			_planTextures[path] = texture = path.Length > 0 && _gui.Textures.Exists(path) ? _gui.Textures.LoadFromGamePath(path) : null;
		}
		return texture;
	}

	private static float Pulse(double time, bool selected) => selected ? 0.25f * ((float)Math.Sin(time) + 1f) + 0.5f : 1f;

	/// <summary>What is to be seen now; a thing no longer in it goes, and one whose shape has changed is made again.</summary>
	public void DrawBattlePlans(IReadOnlyList<CPlanStroke> strokes, IReadOnlyList<CPlanIcon> icons, CPlanObjectTypes types, Func<int, (float R, float G, float B)> palette, double time)
	{
		var seen = new HashSet<object>();
		foreach (CPlanStroke stroke in strokes)
		{
			if (types.Arrow(stroke.Type) is { } type && DrawStroke(stroke, type, palette, time))
			{
				seen.Add(stroke);
			}
		}
		foreach (CPlanIcon icon in icons)
		{
			if (types.Icon(icon.Type) is { } type && DrawIcon(icon, type, palette, time))
			{
				seen.Add(icon);
			}
		}
		foreach (object gone in _planDrawn.Keys.Where(item => !seen.Contains(item)).ToList())
		{
			Discard(_planDrawn[gone]);
			_planDrawn.Remove(gone);
		}
		if (_camera is { } camera)
		{
			ApplyPlanView(camera);
		}
	}

	private void Discard(CPlanDrawn drawn)
	{
		foreach (MeshInstance3D instance in drawn.Instances)
		{
			instance.Mesh = null;
			_overlays.RemoveChild(instance);
			instance.QueueFree();
		}
		drawn.Instances.Clear();
		drawn.Materials.Clear();
		drawn.Mesh?.Dispose();
		drawn.Mesh = null;
	}

	private bool DrawStroke(CPlanStroke stroke, CBattleArrowType type, Func<int, (float R, float G, float B)> palette, double time)
	{
		// The original clears the stencil between its colour lists, so a colour's arrows keep from painting a pixel twice and
		// another colour's draw over them; Godot clears it once a frame, so each colour writes a value of its own.
		int layer = PlanLayer(stroke.Colour);
		if (!_arrowEffects.TryGetValue(layer, out var effect))
		{
			_arrowEffects[layer] = effect = new[]
			{
				_gui.Effects.Get(BattleArrowFile, BattleArrowTechnique, EShaderTarget.Spatial, pass: 0, device: PlanStates, stencilLayer: layer),
				_gui.Effects.Get(BattleArrowFile, BattleArrowTechnique, EShaderTarget.Spatial, pass: 1, device: PlanStates, stencilLayer: layer),
			};
		}
		if (effect[0].Shader is null || effect[1].Shader is null)
		{
			return false;
		}
		int signature = HashCode.Combine(stroke.Points.Count, stroke.Points.Count > 0 ? stroke.Points[^1] : default, stroke.Type, stroke.OriginX, stroke.OriginY, stroke.SnapPrefix, layer);
		if (!_planDrawn.TryGetValue(stroke, out CPlanDrawn? drawn))
		{
			_planDrawn[stroke] = drawn = new CPlanDrawn { Signature = signature ^ 1 };
		}
		if (drawn.Signature != signature)
		{
			Discard(drawn);
			drawn.Signature = signature;
			(List<CPlanVertex> vertices, int body) = CArrowMesh.Build(stroke, type);
			if (vertices.Count == 0)
			{
				return true;
			}
			var indices = new List<ushort>();
			for (int at = 0; at + 3 < body; at += 2)
			{
				indices.AddRange(new[] { (ushort)at, (ushort)(at + 1), (ushort)(at + 2), (ushort)(at + 1), (ushort)(at + 3), (ushort)(at + 2) });
			}
			indices.AddRange(new[] { (ushort)body, (ushort)(body + 1), (ushort)(body + 2), (ushort)(body + 1), (ushort)(body + 3), (ushort)(body + 2) });
			drawn.Mesh = BuildMesh(indices.ToArray(), vertices.Count, effect[0].Translation, (semantic, v) => semantic switch
			{
				"POSITION" => new Vector4(vertices[v].X, vertices[v].Y, 0, 1),
				"TEXCOORD0" => new Vector4(vertices[v].U, vertices[v].V, vertices[v].IsHead, 1),
				_ => null,
			});
			for (int copy = 0; copy < 2; copy++)
			{
				ShaderMaterial first = StrokeMaterial(effect[0], type, PlanPriority + 2 * layer);
				ShaderMaterial second = StrokeMaterial(effect[1], type, PlanPriority + 2 * layer + 1);
				first.NextPass = second;
				drawn.Materials.Add((first, effect[0].Translation));
				drawn.Materials.Add((second, effect[1].Translation));
				MeshInstance3D instance = Instance("BattleArrow", drawn.Mesh, first);
				_overlays.AddChild(instance);
				drawn.Instances.Add(instance);
			}
		}
		(float r, float g, float b) = palette(stroke.Colour);
		foreach ((ShaderMaterial material, CGodotShader? translation) in drawn.Materials)
		{
			CGodotShader?[] one = { translation };
			Set(material, one, "ArrowColorAlpha", new Vector4(r, g, b, 1f));
			Set(material, one, "ArrowTransparency", Pulse(time, stroke.IsSelected));
		}
		return true;
	}

	private ShaderMaterial StrokeMaterial((Shader? Shader, CGodotShader? Translation) effect, CBattleArrowType type, int priority)
	{
		var material = new ShaderMaterial { Shader = effect.Shader, RenderPriority = priority };
		CGodotShader?[] one = { effect.Translation };
		Set(material, one, "CameraPosition", Godot.Vector2.Zero);
		Set(material, one, "NoFade", type.NoFade);
		SetTexture(material, one, "tex0", PlanTexture(type.Body));
		SetTexture(material, one, "tex1", PlanTexture(type.Head));
		_copyMaterials.Add(material);
		return material;
	}

	private bool DrawIcon(CPlanIcon icon, CMapInfoType type, Func<int, (float R, float G, float B)> palette, double time)
	{
		_iconEffect ??= _gui.Effects.Get(MapInfoFile, MapInfoTechnique, EShaderTarget.Spatial, device: PlanStates);
		if (_iconEffect.Value.Shader is null)
		{
			return false;
		}
		float x = icon.X + icon.OriginX, y = icon.Y + icon.OriginY, s = type.Scale * 0.5f;
		int signature = HashCode.Combine(x, y, icon.Type);
		if (!_planDrawn.TryGetValue(icon, out CPlanDrawn? drawn))
		{
			_planDrawn[icon] = drawn = new CPlanDrawn { Signature = signature ^ 1 };
		}
		CGodotShader?[] one = { _iconEffect.Value.Translation };
		if (drawn.Signature != signature)
		{
			Discard(drawn);
			drawn.Signature = signature;
			var corners = new[] { (x - s, y - s, 0f, 1f), (x + s, y - s, 1f, 1f), (x - s, y + s, 0f, 0f), (x + s, y + s, 1f, 0f) };
			drawn.Mesh = BuildMesh(new ushort[] { 0, 1, 2, 1, 3, 2 }, 4, _iconEffect.Value.Translation, (semantic, v) => semantic switch
			{
				"POSITION" => new Vector4(corners[v].Item1, corners[v].Item2, 0, 1),
				"TEXCOORD0" => new Vector4(corners[v].Item3, corners[v].Item4, 0, 1),
				_ => null,
			});
			for (int copy = 0; copy < 2; copy++)
			{
				var material = new ShaderMaterial { Shader = _iconEffect.Value.Shader, RenderPriority = IconPriority };
				Set(material, one, "CameraPosition", Godot.Vector2.Zero);
				SetTexture(material, one, "tex0", PlanTexture(type.Texture));
				_copyMaterials.Add(material);
				drawn.Materials.Add((material, _iconEffect.Value.Translation));
				MeshInstance3D instance = Instance("BattlePlanIcon", drawn.Mesh, material);
				_overlays.AddChild(instance);
				drawn.Instances.Add(instance);
			}
		}
		(float r, float g, float b) = palette(icon.Colour);
		foreach ((ShaderMaterial material, _) in drawn.Materials)
		{
			Set(material, one, "ArrowColorAlpha", new Vector4(r, g, b, 1f));
			Set(material, one, "IconTransparency", Pulse(time, icon.IsSelected));
			Set(material, one, "IconRotation", icon.Rotation);
			Set(material, one, "IconPosition", new Godot.Vector2(x, y));
		}
		return true;
	}

	/// <summary>Every plan mesh's view × projection, the second copy a map's width east.</summary>
	private void ApplyPlanView(CMapSurfaceCamera camera)
	{
		if (_planDrawn.Count == 0)
		{
			return;
		}
		float[] viewProjection = CD3DMatrix.Multiply(camera.ViewAt, camera.Projection);
		Projection here = Matrix(CD3DMatrix.Multiply(CD3DMatrix.Translation(0, 0, 0), viewProjection));
		Projection east = Matrix(CD3DMatrix.Multiply(CD3DMatrix.Translation(_map.Width, 0, 0), viewProjection));
		foreach (CPlanDrawn drawn in _planDrawn.Values)
		{
			int perCopy = drawn.Materials.Count / Math.Max(drawn.Instances.Count, 1);
			for (int index = 0; index < drawn.Materials.Count; index++)
			{
				(ShaderMaterial material, CGodotShader? translation) = drawn.Materials[index];
				CGodotShader?[] one = { translation };
				Set(material, one, "WorldViewProjectionMatrix", index / Math.Max(perCopy, 1) == 0 ? here : east);
			}
		}
	}
}
