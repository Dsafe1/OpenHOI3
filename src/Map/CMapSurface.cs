using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Map;

/// <summary>
/// The map's surface drawn as the original draws it: the land and the beaches tile by tile from the install's quad cache,
/// the sea as three map-sized quads, each through the install's own effect file - terrain_2_0.fx, water.fx - translated to a
/// Godot shader, its technique picked as the original picks it and every parameter and texture set by the effect's own
/// names, under the original's camera made to agree with the 2D view laid over it. See <c>docs/mechanics/mapdraw.md</c>.
/// </summary>
public partial class CMapSurface : Node3D
{
	public const string TerrainFile = "terrain_2_0.fx";
	public const string WaterFile = "water.fx";

	/// <summary>The original's generated textures' sizes [exe].</summary>
	private const int LookupSize = 256;
	private const int RasterSize = 1024;

	/// <summary>The sea's colour off any province [exe].</summary>
	private const uint OffProvince = 0x80808080;

	/// <summary>
	/// The land's and the beaches' view and projection, one for every tile: Godot's global uniforms, under these names
	/// (<c>shaders.md</c>, section 3.11).
	/// </summary>
	private const string SharedView = "fx_map_view";
	private const string SharedProjection = "fx_map_projection";

	private static readonly Dictionary<string, string> Shared = new()
	{
		["ViewMatrix"] = SharedView,
		["ProjectionMatrix"] = SharedProjection,
	};

	private CMap _map = null!;
	private CGuiContext _gui = null!;
	private IReadOnlyList<CQuadTile> _tiles = null!;
	private int _columns;

	/// <summary>Each tile's land and beaches, and the copy of the map its world matrix was last set for.</summary>
	private readonly List<CSurfaceTile> _parts = new();

	private readonly Dictionary<string, Texture2D?> _files = new(StringComparer.OrdinalIgnoreCase);
	private readonly Image _lookup0 = Image.CreateEmpty(LookupSize, LookupSize, false, Image.Format.Rgba8);
	private readonly Image _lookup1 = Image.CreateEmpty(LookupSize, LookupSize, false, Image.Format.Rgba8);
	private readonly Image _sea0 = Image.CreateEmpty(RasterSize, RasterSize, false, Image.Format.Rgba8);
	private readonly Image _sea1 = Image.CreateEmpty(RasterSize, RasterSize, false, Image.Format.Rgba8);
	private readonly Image _fog = Image.CreateEmpty(RasterSize, RasterSize, false, Image.Format.L8);
	private ImageTexture _lookup0Texture = null!;
	private ImageTexture _lookup1Texture = null!;
	private ImageTexture _sea0Texture = null!;
	private ImageTexture _sea1Texture = null!;
	private ImageTexture _fogTexture = null!;

	private int _style = 2;
	private int _level;
	private bool _simpleWater;
	private CMapSurfaceCamera? _camera;
	private Vector2 _screen;
	private double _clock;


	/// <summary>The land borders' corner cache, whose bytes the map view works out again; null where the install has none.</summary>
	private Content.Map.CBorderCache? _corners;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CMap map, CGuiContext gui, IReadOnlyList<CQuadTile> tiles, int columns, bool simpleWater,
		Content.Map.CBorderCache? corners = null)
	{
		_corners = corners;
		_map = map;
		_gui = gui;
		_tiles = tiles;
		_columns = columns;
		_simpleWater = simpleWater;
	}

	public override void _Ready()
	{
		Name = "Surface";
		// Godot draws 3D only through a current camera; the effects place everything themselves, so it stands anywhere.
		// The original clears its frame to mid-grey before the scene (FINDINGS-mapdraw.md, C.1), and what a blended beach lets
		// through is that grey, the sea being drawn after it.
		_camera3D = new Camera3D
		{
			Name = "Camera",
			Current = true,
			Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0x808080FF) },
			CullMask = BaseLayer,
		};
		AddChild(_camera3D);
		RegisterShared();
		_lookup0Texture = ImageTexture.CreateFromImage(_lookup0);
		_lookup1Texture = ImageTexture.CreateFromImage(_lookup1);
		_sea0Texture = ImageTexture.CreateFromImage(_sea0);
		_sea1Texture = ImageTexture.CreateFromImage(_sea1);
		_fogTexture = ImageTexture.CreateFromImage(_fog);
		_borderLookupTexture = ImageTexture.CreateFromImage(_borderLookup);
		AddChild(_borders);
		AddChild(_waterBorders);
		BuildLand();
		BuildSea();
		BuildWaterBorders();
		Shader? border = Terrain(BorderTechnique, BorderStates).Shader;
		foreach (CSurfaceTile part in _parts)
		{
			if (part.Border is { } material)
			{
				material.Shader = border;
			}
			SetBorderTile(part);
		}
		// The land's way is made at the camera's first place, the height deciding which.
	}

	/// <summary>The global uniforms the shared parameters are, registered once for the process before any shader names them.</summary>
	private static void RegisterShared()
	{
		// Godot's list of them is the editor's alone, so the process remembers that it has registered them.
		if (_registered)
		{
			return;
		}
		_registered = true;
		foreach (string name in new[] { SharedView, SharedProjection })
		{
			RenderingServer.GlobalShaderParameterAdd(name, RenderingServer.GlobalShaderParameterType.Mat4, Projection.Identity);
		}
	}

	private static bool _registered;

	// ---------------------------------------------------------------------------------------------------------- techniques

	/// <summary>
	/// The map's style - which technique the land takes, Graphical in style 0 (the Terrain and Weather modes) - as the map
	/// mode sets it (<c>reversing/findings/FINDINGS-farmap.md</c>, A.3).
	/// </summary>
	public int Style
	{
		get => _style;
		set
		{
			if (_style != value)
			{
				_style = value;
				if (IsInsideTree())
				{
					ShowTechniques();
				}
			}
		}
	}

	private bool Far => _level > 0;

	/// <summary>
	/// The four ways the land and the beaches are drawn (<c>FINDINGS-mapdraw.md</c>, A.2): style 0 Graphical at every height,
	/// any other style General near and General_Low far; colormap.dds in style 0 and the political one otherwise; the noise
	/// and the near sheet near, the overlay and the far sheet far [exe].
	/// </summary>
	private static (string Land, string Beach, bool Far, bool Political) Way(int way) => way switch
	{
		0 => ("TerrainShader_Graphical", "BeachShader_Graphical", false, false),
		1 => ("TerrainShader_Graphical", "BeachShader_Graphical", true, false),
		2 => ("TerrainShader_General", "BeachShader_General", false, true),
		_ => ("TerrainShader_General_Low", "BeachShader_General_Low", true, true),
	};

	private int CurrentWay => (_style == 0 ? 0 : 2) + (Far ? 1 : 0);

	/// <summary>Each way's instances under a node of their own, and the translations its materials were set through.</summary>
	private readonly Node3D?[] _wayNodes = new Node3D?[CSurfaceTile.Ways];
	private readonly CGodotShader?[][] _wayTranslations = new CGodotShader?[CSurfaceTile.Ways][];
	private int _shownWay = -1;

	private string WaterTechnique
	{
		get
		{
			string technique = _simpleWater ? "WaterSimple" : Far ? "WaterFar" : "WaterNear";
			return _style is 16 or 18 or 19 ? technique + "Color" : technique;
		}
	}

	/// <summary>The device's states around each draw, as the original's scene sets them (<c>FINDINGS-mapdraw.md</c>, C.1-C.2).</summary>
	private static readonly Dictionary<string, string> LandStates = Baseline(("ALPHABLENDENABLE", "0"), ("SRCBLEND", "ONE"), ("DESTBLEND", "ZERO"));
	private static readonly Dictionary<string, string> BeachStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"), ("DESTBLEND", "ZERO"));
	private static readonly Dictionary<string, string> WaterStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"), ("DESTBLEND", "INVSRCALPHA"), ("ZENABLE", "1"));

	private static Dictionary<string, string> Baseline(params (string Name, string Value)[] states)
	{
		// The engine's baseline after every device creation [exe].
		var baseline = new Dictionary<string, string>
		{
			["ALPHATESTENABLE"] = "1", ["ALPHAREF"] = "8", ["ALPHAFUNC"] = "GREATEREQUAL", ["ZENABLE"] = "1", ["ZWRITEENABLE"] = "1",
		};
		foreach ((string name, string value) in states)
		{
			baseline[name] = value;
		}
		return baseline;
	}

	private (Shader? Shader, CGodotShader? Translation) Terrain(string technique, Dictionary<string, string> states)
	{
		return _gui.Effects.Get(TerrainFile, technique, EShaderTarget.Spatial, device: states, shared: Shared);
	}

	/// <summary>
	/// The way the style and the height want shown, made first where it never was, and the others hidden; the sea's one
	/// material given its technique. *Ours*: giving 2,700 materials another shader at a change of height made the rendering
	/// device build each one's state again, a frame of 140 ms at every crossing of 500; a way once made keeps its materials,
	/// so a crossing shows one node and hides another.
	/// </summary>
	private void ShowTechniques()
	{
		if (_camera is null)
		{
			return;
		}
		int way = CurrentWay;
		MakeWay(way);
		// The same style's other height made with it, so that the first crossing of 500 does not make 2,700 materials, and
		// drawn too for the first frames, so that the rendering device has set them up before they are wanted.
		if (_wayNodes[way ^ 1] is null)
		{
			MakeWay(way ^ 1);
			_warmWay = way ^ 1;
			_warmFrames = 2;
		}
		_shownWay = way;
		// The sea likewise: its technique's own material, the other height's made and warmed with it.
		string sea = WaterTechnique;
		MakeSea(sea);
		string otherSea = sea.Contains("Near") ? sea.Replace("Near", "Far") : sea.Replace("Far", "Near");
		if (otherSea != sea && !_seaWays.ContainsKey(otherSea))
		{
			MakeSea(otherSea);
			_warmSea = otherSea;
			_warmFrames = 2;
		}
		_shownSea = sea;
		ApplyCullMask();
	}

	private string? _shownSea;
	private string? _warmSea;

	/// <summary>
	/// The render layers: everything always drawn on the first, each way's land and beaches on one of its own, the land
	/// borders on another. *Ours*: what is shown is chosen by the camera's cull mask and not by hiding instances - showing or
	/// hiding a few thousand instances cost the rendering server some 120 ms at every crossing of 500, where a mask is one
	/// call and no instance changes.
	/// </summary>
	private const uint BaseLayer = 1u;
	private const uint BorderLayer = 1u << 5;

	private static uint WayLayer(int way) => 1u << (1 + way);

	private Camera3D _camera3D = null!;
	private bool _bordersShown;

	private void ApplyCullMask()
	{
		uint Sea(string? technique) => technique is not null && _seaWays.TryGetValue(technique, out var sea) ? sea.Layer : 0;
		bool warm = _warmFrames > 0;
		_camera3D.CullMask = BaseLayer | (_shownWay >= 0 ? WayLayer(_shownWay) : 0) | (_bordersShown ? BorderLayer : 0) | Sea(_shownSea)
			| (warm && _warmWay >= 0 ? WayLayer(_warmWay) | BorderLayer : 0) | (warm ? Sea(_warmSea) : 0);
	}

	/// <summary>A way made ahead of its use, drawn under the one shown for so many frames, the land borders with it.</summary>
	private int _warmWay = -1;
	private int _warmFrames;

	/// <summary>A way's materials and instances, one of each for every tile's land and beaches, set as the way draws them.</summary>
	private void MakeWay(int way)
	{
		if (_wayNodes[way] is not null)
		{
			return;
		}
		(string landTechnique, string beachTechnique, bool far, bool political) = Way(way);
		var land = Terrain(landTechnique, LandStates);
		var beach = Terrain(beachTechnique, BeachStates);
		CGodotShader?[] translations = { land.Translation, beach.Translation };
		_wayTranslations[way] = translations;
		var node = new Node3D { Name = $"Way{way}" };
		AddChild(node);
		_wayNodes[way] = node;
		Texture2D? colourMap = File(political ? "colormap_political.dds" : "colormap.dds");
		Texture2D? overlay = far ? File("map_overlay_tile.dds") : File("noise_test2.dds");
		Texture2D? sheet = far ? File("texturesheet2.tga") : File("texturesheet.tga");
		foreach (CSurfaceTile part in _parts)
		{
			var landMaterial = new ShaderMaterial { Shader = land.Shader };
			part.Land[way] = landMaterial;
			node.AddChild(Instance($"Land{part.Tile.Index}", part.LandMesh, landMaterial, WayLayer(way)));
			if (part.BeachMesh is { } beachMesh)
			{
				var beachMaterial = new ShaderMaterial { Shader = beach.Shader };
				part.Beach[way] = beachMaterial;
				node.AddChild(Instance($"Beach{part.Tile.Index}", beachMesh, beachMaterial, WayLayer(way)));
			}
			foreach (ShaderMaterial material in part.Materials(way))
			{
				SetTile(part, material, translations);
				SetTexture(material, translations, "tex4", colourMap);
				SetTexture(material, translations, "tex5", overlay);
				SetTexture(material, translations, "tex6", sheet);
			}
		}
	}

	// ------------------------------------------------------------------------------------------------------- parameters

	/// <summary>An effect's parameter on a material by every name the given translations have for it.</summary>
	private void Set(ShaderMaterial material, CGodotShader?[] translations, string fxName, Variant value)
	{
		foreach (string name in Names(translations, fxName, texture: false))
		{
			material.SetShaderParameter(name, value);
		}
	}

	/// <summary>An effect's texture on a material, for every sampler of the given translations that reads it.</summary>
	private void SetTexture(ShaderMaterial material, CGodotShader?[] translations, string texture, Texture2D? value)
	{
		if (value is null)
		{
			return;
		}
		foreach (string name in Names(translations, texture, texture: true))
		{
			material.SetShaderParameter(name, value);
		}
	}

	/// <summary>The shaders' names for an effect's parameter or texture across a set of translations, worked out once.</summary>
	private string[] Names(CGodotShader?[] translations, string fxName, bool texture)
	{
		var key = (translations, fxName, texture);
		if (!_names.TryGetValue(key, out string[]? names))
		{
			names = texture
				? translations.SelectMany(t => t?.Textures ?? Array.Empty<CShaderTexture>())
					.Where(t => string.Equals(t.Texture, fxName, StringComparison.Ordinal)).Select(t => t.Name).Distinct().ToArray()
				: translations.SelectMany(t => t?.Uniforms ?? Array.Empty<CShaderUniform>())
					.Where(u => u.FxName == fxName && !u.Shared).Select(u => u.Name).Distinct().ToArray();
			_names[key] = names;
		}
		return names;
	}

	private readonly Dictionary<(CGodotShader?[], string, bool), string[]> _names = new();

	/// <summary>A D3DX row-major matrix as the translation takes it: each row a column of Godot's (<c>shaders.md</c>, 3.3).</summary>
	private static Projection Matrix(float[] m)
	{
		return new Projection(
			new Vector4(m[0], m[1], m[2], m[3]),
			new Vector4(m[4], m[5], m[6], m[7]),
			new Vector4(m[8], m[9], m[10], m[11]),
			new Vector4(m[12], m[13], m[14], m[15]));
	}

	private Texture2D? File(string name)
	{
		if (!_files.TryGetValue(name, out Texture2D? texture))
		{
			texture = _gui.Textures.LoadFromGamePath("map/terrain/" + name);
			_files[name] = texture;
		}
		return texture;
	}

	/// <summary>
	/// What terrain_2_0.fx is fed for a tile that is the tile's own (<c>FINDINGS-mapdraw.md</c>, A.2): the colour map's size
	/// twice - the texture kept at the picture's own size, ours - the map's, the tile's, its terrain-index rectangle and
	/// picture, its middle, and the textures every tile shares whatever the height and the style. The beaches set no tex3,
	/// tex5 or tex7 of their own in the original and keep the land's, which are these.
	/// </summary>
	private void SetTile(CSurfaceTile part, ShaderMaterial material, CGodotShader?[] translations)
	{
		CQuadTile tile = part.Tile;
		Texture2D? plain = File("colormap.dds");
		float width = plain?.GetWidth() ?? 1;
		float height = plain?.GetHeight() ?? 1;
		(int x, int y, int w, int h) = tile.LandRectangle(_columns);
		(int midX, int midY) = tile.Middle(_columns);
		Texture2D? index = _gui.Textures.LoadFromGamePath($"map/cache/quad_textures/quad_texture_{tile.Index}.dds");
		{
			Set(material, translations, "ColorMapWidth", width);
			Set(material, translations, "ColorMapHeight", height);
			Set(material, translations, "ColorMapTextureWidth", width);
			Set(material, translations, "ColorMapTextureHeight", height);
			Set(material, translations, "MapWidth", (float)_map.Width);
			Set(material, translations, "MapHeight", (float)_map.Height);
			Set(material, translations, "BorderWidth", (float)CQuadTile.Size);
			Set(material, translations, "BorderHeight", (float)CQuadTile.Size);
			Set(material, translations, "TerrainIndexOffsetX", (float)(x - 2));
			Set(material, translations, "TerrainIndexOffsetY", (float)(y - 2));
			Set(material, translations, "TerrainIndexSizeX", (float)(w + 4));
			Set(material, translations, "TerrainIndexSizeY", (float)(h + 4));
			Set(material, translations, "AbsoluteWorldMatrix", Matrix(CD3DMatrix.Translation(midX, 0, midY)));
			Set(material, translations, "WorldMatrix", Matrix(CD3DMatrix.Translation(midX + part.Shift, 0, midY)));
			SetTexture(material, translations, "tex0", File("base.dds"));
			SetTexture(material, translations, "tex1", index);
			SetTexture(material, translations, "tex2", _lookup0Texture);
			SetTexture(material, translations, "tex3", _lookup1Texture);
			SetTexture(material, translations, "tex7", File("stripes.dds"));
		}
	}

	// ----------------------------------------------------------------------------------------------------------- camera

	/// <summary>The 2D view's middle, zoom and the screen: the camera made again where any has changed.</summary>
	public void SetView(Vector2 centre, float zoom, Vector2 screen)
	{
		if (_camera is { } last && last.X == centre.X - 0.5f && last.Z == _map.Height - 0.5f - centre.Y
			&& Mathf.IsEqualApprox(last.Height, screen.Y / zoom / (2f * Mathf.Tan(CMapSurfaceCamera.FieldOfView / 2f)))
			&& _screen == screen)
		{
			return;
		}
		_screen = screen;
		_camera = new CMapSurfaceCamera(new System.Numerics.Vector2(centre.X, centre.Y), zoom,
			new System.Numerics.Vector2(screen.X, screen.Y), _map.Width, _map.Height);
		if (IsInsideTree())
		{
			ApplyView();
		}
	}

	/// <summary>
	/// The camera given to the effects: the shared view and projection, a tile's world matrix only where it has crossed
	/// the seam, the sea's own two, and the techniques and their textures where the height has crossed a threshold.
	/// </summary>
	private void ApplyView()
	{
		if (_camera is null)
		{
			return;
		}
		int level = _map.Level(_camera.Height);
		if (level != _level || _shownWay < 0)
		{
			_level = level;
			ShowTechniques();
		}
		RenderingServer.GlobalShaderParameterSet(SharedView, Matrix(_camera.ViewAt));
		RenderingServer.GlobalShaderParameterSet(SharedProjection, Matrix(_camera.Projection));
		foreach (CSurfaceTile part in _parts)
		{
			(int midX, int midY) = part.Tile.Middle(_columns);
			float shift = _camera.Shift(midX);
			if (shift != part.Shift)
			{
				part.Shift = shift;
				Projection world = Matrix(_camera.WorldAt(midX, midY));
				for (int way = 0; way < CSurfaceTile.Ways; way++)
				{
					if (_wayTranslations[way] is { } translations)
					{
						foreach (ShaderMaterial material in part.Materials(way))
						{
							Set(material, translations, "WorldMatrix", world);
						}
					}
				}
				if (part.Border is { } border)
				{
					Set(border, BorderAll, "WorldMatrix", world);
				}
			}
		}
		ApplyBorderHeight(_camera.Height);
		ApplyWaterBorderView(_camera);
		foreach ((ShaderMaterial material, CGodotShader?[] translations, uint _) in _seaWays.Values)
		{
			SetSeaCamera(material, translations, _camera);
		}
	}

	/// <summary>
	/// Everything the surface made given back to the engine as it leaves: its meshes, its 4,500 materials and their
	/// textures. Each has a wrapper on the managed side that holds it until the collector gets to it, which under a debugger
	/// can be a long while - and the lobby makes a surface for every world it shows, so their texture sets ran the rendering
	/// device's descriptor heap out once the game's own borders were first drawn.
	/// </summary>
	public override void _ExitTree()
	{
		foreach (Node node in GetChildren().Concat(_borders.GetChildren()).Concat(_waterBorders.GetChildren()))
		{
			if (node is MeshInstance3D instance && instance.Mesh is { } mesh)
			{
				instance.Mesh = null;
				mesh.Dispose();
			}
		}
		// The ways' instances share each tile's meshes, which are given back once, after them.
		foreach (Node3D? way in _wayNodes)
		{
			foreach (Node node in way?.GetChildren() ?? new Godot.Collections.Array<Node>())
			{
				if (node is MeshInstance3D instance)
				{
					instance.Mesh = null;
					instance.MaterialOverride = null;
				}
			}
		}
		foreach (CSurfaceTile part in _parts)
		{
			part.LandMesh.Dispose();
			part.BeachMesh?.Dispose();
			for (int way = 0; way < CSurfaceTile.Ways; way++)
			{
				part.Land[way]?.Dispose();
				part.Beach[way]?.Dispose();
			}
			part.Border?.Dispose();
		}
		_parts.Clear();
		foreach ((ShaderMaterial material, CGodotShader?[] _, uint _) in _seaWays.Values)
		{
			material.Dispose();
		}
		_seaWays.Clear();
		_seaMesh.Dispose();
		foreach (ShaderMaterial? material in _waterBorderMaterials)
		{
			material?.Dispose();
		}
		_waterBorderTiles.Clear();
		foreach (GodotObject made in new GodotObject[]
		{
			_lookup0Texture, _lookup1Texture, _sea0Texture, _sea1Texture, _fogTexture, _borderLookupTexture,
			_lookup0, _lookup1, _sea0, _sea1, _fog, _borderLookup,
		})
		{
			made.Dispose();
		}
	}

	public override void _Process(double delta)
	{
		// water.fx's Time: a sawtooth from −100 to nought, a hundred seconds round [exe].
		_clock = (_clock + delta) % 100.0;
		if (_warmFrames > 0 && --_warmFrames == 0)
		{
			ApplyCullMask();
		}
		if (_shownSea is { } shown && _seaWays.TryGetValue(shown, out var sea))
		{
			Set(sea.Material, sea.Translations, "Time", (float)-_clock);
		}
	}

	// ------------------------------------------------------------------------------------------------------------ meshes

	/// <summary>
	/// Every tile's land and its beaches where it has some, a mesh each, made once - every technique of either takes the same
	/// vertex - and its land borders, a mesh and a material.
	/// </summary>
	private void BuildLand()
	{
		var land = Terrain(Way(0).Land, LandStates).Translation;
		var beach = Terrain(Way(0).Beach, BeachStates).Translation;
		foreach (CQuadTile tile in _tiles)
		{
			ArrayMesh landMesh = BuildMesh(tile.LandIndices, tile.LandProvinces.Length, land,
				(semantic, v) => semantic switch
				{
					"POSITION" => new Vector4(tile.LandPositions[2 * v], tile.LandPositions[2 * v + 1], 0, 1),
					"TEXCOORD0" => new Vector4(tile.LandProvinces[v] & 0xFF, tile.LandProvinces[v] >> 8, 0, 1),
					_ => null,
				});
			ArrayMesh? beachMesh = null;
			if (tile.BeachIndices.Length > 0)
			{
				beachMesh = BuildMesh(tile.BeachIndices, tile.BeachColours.Length, beach,
					(semantic, v) => semantic switch
					{
						"POSITION" => new Vector4(tile.BeachPositions[2 * v], tile.BeachPositions[2 * v + 1], 0, 1),
						"COLOR0" => Colour(tile.BeachColours[v]),
						_ => null,
					});
			}
			ShaderMaterial? borderMaterial = null;
			if (tile.BorderIndices.Length > 0 && tile.BorderColours.Length > 0)
			{
				// Each vertex (x, z, u, v) as the cache keeps them - x and z carrying the corner flag of ±1000 the effect
				// takes off, u and v a texel of the lookup - and the colour whose blue and alpha pick the pattern's columns.
				borderMaterial = new ShaderMaterial { RenderPriority = BorderPriority };
				_borders.AddChild(MakeMesh($"Border{tile.Index}", tile.BorderIndices, tile.BorderColours.Length, BorderTranslation,
					borderMaterial,
					(semantic, v) => semantic switch
					{
						"POSITION" => new Vector4(tile.BorderShorts[4 * v], tile.BorderShorts[4 * v + 1], tile.BorderShorts[4 * v + 2],
							tile.BorderShorts[4 * v + 3]),
						"COLOR0" => Colour(tile.BorderColours[v]),
						_ => null,
					}, layer: BorderLayer));
			}
			_parts.Add(new CSurfaceTile(tile, landMesh, beachMesh, borderMaterial));
		}
	}

	// ------------------------------------------------------------------------------------------------------ water borders

	public const string WaterBorderFile = "borders_2_0.fx";
	public const string WaterBorderTechnique = "WaterBorderShader";

	/// <summary>Drawn after the sea and over everything before, the depth test off [exe] (FINDINGS-mapdraw.md, B.8).</summary>
	private const int WaterBorderPriority = 3;

	private static readonly Dictionary<string, string> WaterBorderStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"),
		("DESTBLEND", "INVSRCALPHA"), ("ZENABLE", "0"), ("ZWRITEENABLE", "1"));

	/// <summary>The three materials, for a tile drawn a map's width west, where it is, and a width east.</summary>
	private readonly ShaderMaterial[] _waterBorderMaterials = new ShaderMaterial[3];

	/// <summary>Each tile's water-border mesh with the middle of its tile and the copy of the map it was last given.</summary>
	private readonly List<(MeshInstance3D Instance, int MiddleX, int Copy)> _waterBorderTiles = new();

	private readonly Node3D _waterBorders = new() { Name = "WaterBorders" };

	/// <summary>
	/// The sea zones' borders: for each tile of the corner cache's quads a mesh, each quad four vertices and six indices as the
	/// original builds them (B.8). *Ours*: the vertices hold the map's own coordinates and not the tile's - the original's
	/// world-view-projection is the tile's middle less the camera's place, and the product is the same - so that three
	/// materials, one for each copy of the map, serve every tile, and moving the camera sets three matrices.
	/// </summary>
	private void BuildWaterBorders()
	{
		if (_corners is not { } cache || cache.WaterBorders.Length == 0)
		{
			return;
		}
		var effect = _gui.Effects.Get(WaterBorderFile, WaterBorderTechnique, EShaderTarget.Spatial, device: WaterBorderStates);
		if (effect.Shader is null)
		{
			return;
		}
		CGodotShader?[] translations = { effect.Translation };
		for (int copy = 0; copy < 3; copy++)
		{
			var material = new ShaderMaterial { Shader = effect.Shader, RenderPriority = WaterBorderPriority };
			SetTexture(material, translations, "tex1", File("water_borders.dds"));
			SetTexture(material, translations, "tex3", File("water_diag_borders.dds"));
			_waterBorderMaterials[copy] = material;
		}
		_waterBorderAll = translations;
		int columns = _map.Width / CQuadTile.Size + 1;
		for (int index = 0; index < cache.WaterBorders.Length; index++)
		{
			IReadOnlyList<CWaterBorderQuad> quads = cache.WaterBorders[index];
			if (quads.Count == 0)
			{
				continue;
			}
			// The four corners of each pixel's quad: (x − 1, y − 1), (x − 1, y), (x, y), (x, y − 1), uv (0,1), (0,0), (1,0), (1,1).
			var indices = new ushort[quads.Count * 6];
			for (int q = 0; q < quads.Count; q++)
			{
				int n = q * 4;
				indices[q * 6] = (ushort)n;
				indices[q * 6 + 1] = (ushort)(n + 1);
				indices[q * 6 + 2] = (ushort)(n + 2);
				indices[q * 6 + 3] = (ushort)(n + 2);
				indices[q * 6 + 4] = (ushort)(n + 3);
				indices[q * 6 + 5] = (ushort)n;
			}
			MeshInstance3D instance = MakeMesh($"WaterBorder{index}", indices, quads.Count * 4, effect.Translation,
				_waterBorderMaterials[1],
				(semantic, v) =>
				{
					CWaterBorderQuad quad = quads[v / 4];
					int corner = v % 4;
					return semantic switch
					{
						"POSITION" => new Vector4(quad.X - (corner < 2 ? 1 : 0), quad.Y - (corner is 0 or 3 ? 1 : 0), 0, 1),
						"TEXCOORD0" => new Vector4(corner < 2 ? 0 : 1, corner is 0 or 3 ? 1 : 0, quad.Province, quad.Diagonal),
						_ => null,
					};
				});
			instance.MaterialOverride = _waterBorderMaterials[1];
			_waterBorders.AddChild(instance);
			_waterBorderTiles.Add((instance, index % columns * CQuadTile.Size + CQuadTile.Size / 2, 1));
		}
	}

	private CGodotShader?[] _waterBorderAll = Array.Empty<CGodotShader?>();

	/// <summary>The water borders' three matrices, and each tile given the copy of the map nearest the camera.</summary>
	private void ApplyWaterBorderView(CMapSurfaceCamera camera)
	{
		if (_waterBorderTiles.Count == 0)
		{
			return;
		}
		float[] viewProjection = CD3DMatrix.Multiply(camera.ViewAt, camera.Projection);
		for (int copy = 0; copy < 3; copy++)
		{
			float[] world = CD3DMatrix.Translation((copy - 1) * _map.Width, 0, 0);
			Set(_waterBorderMaterials[copy], _waterBorderAll, "WorldViewProjectionMatrix", Matrix(CD3DMatrix.Multiply(world, viewProjection)));
		}
		for (int i = 0; i < _waterBorderTiles.Count; i++)
		{
			(MeshInstance3D instance, int middleX, int copy) = _waterBorderTiles[i];
			int wanted = 1 + (int)MathF.Round(camera.Shift(middleX) / _map.Width);
			if (wanted != copy)
			{
				instance.MaterialOverride = _waterBorderMaterials[wanted];
				_waterBorderTiles[i] = (instance, middleX, wanted);
			}
		}
	}

	// ----------------------------------------------------------------------------------------------------------- borders

	public const string BorderTechnique = "TerrainShader_Border";

	/// <summary>The land borders drawn after the land and the beaches and before the sea, as the scene draws them (C.1).</summary>
	private const int BorderPriority = 1;
	private const int SeaPriority = 2;

	/// <summary>The border lookup's size, 14 texels for each province from 14 × id [exe].</summary>
	private const int BorderLookupSize = 512;
	private const int BorderTexels = 14;

	private static readonly Dictionary<string, string> BorderStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"), ("DESTBLEND", "INVSRCALPHA"));

	private readonly Node3D _borders = new() { Name = "Borders" };
	private readonly Image _borderLookup = Image.CreateEmpty(BorderLookupSize, BorderLookupSize, false, Image.Format.Rgba8);
	private ImageTexture _borderLookupTexture = null!;
	private int _borderPicture = -1;

	private CGodotShader? BorderTranslation => Terrain(BorderTechnique, BorderStates).Translation;

	private CGodotShader?[] BorderAll => _borderAll ??= new[] { BorderTranslation };

	private CGodotShader?[]? _borderAll;

	/// <summary>
	/// The border lookup made again (<c>FINDINGS-mapdraw.md</c>, B.3): for each province a texel for each of its edges in the
	/// map's order - blue full, and alpha a quarter where the neighbour is held by another than the province's holder, which
	/// moves the effect one band down the border picture, from the province's line to the country's. The texels after the
	/// edges hold what the original's border manager keeps for the province, which was not read: nought here, so what they
	/// draw - the cache gives them to one border vertex in twenty - is not drawn.
	/// </summary>
	public void SetBorders(Func<int, int, bool> sameHolder)
	{
		if (!IsInsideTree())
		{
			return;
		}
		const int Texels = BorderLookupSize * BorderLookupSize;
		var bytes = new byte[Texels * 4];
		int last = Math.Min(_map.ProvinceCount, Texels / BorderTexels);
		int written = 0;
		for (int id = 1; id < last; id++)
		{
			// Each province from 14 × id, what the last left short of it cleared; nothing caps its run, so where it runs past
			// its fourteen the next province writes over it, as the original's does (FINDINGS-mapdraw.md, B.7).
			int start = BorderTexels * id;
			if (written < start)
			{
				Array.Clear(bytes, written * 4, (start - written) * 4);
			}
			written = start;
			Sim.Provinces.CProvinceEdge[] edges = _map.Edges[id];
			foreach (Sim.Provinces.CProvinceEdge edge in edges)
			{
				if (written >= Texels)
				{
					break;
				}
				int at = written++ * 4;
				bytes[at] = 0;
				bytes[at + 1] = 0;
				bytes[at + 2] = 255;
				bytes[at + 3] = sameHolder(id, edge.To) ? (byte)0 : (byte)0x40;
			}
			// Then each junction pixel's entry: alpha the province pattern's column, red the country's, green the diagonal's,
			// each times 8 and kept to a byte; blue nought, which leaves the vertex's own columns aside.
			if (_corners is { } cache && id < cache.Pairs.Length)
			{
				foreach ((int x, int y) in cache.Pairs[id])
				{
					if (written >= Texels)
					{
						break;
					}
					Content.Map.CBorderCorner? corner = cache.At(x, y);
					int at = written++ * 4;
					bytes[at] = (byte)((corner?.Country ?? 0) * 8);
					bytes[at + 1] = (byte)((corner?.Diagonal ?? 0) * 8);
					bytes[at + 2] = 0;
					bytes[at + 3] = (byte)((corner?.Province ?? 0) * 8);
				}
			}
		}
		_borderLookup.SetData(BorderLookupSize, BorderLookupSize, false, Image.Format.Rgba8, bytes);
		_borderLookupTexture.Update(_borderLookup);
	}

	/// <summary>Which of the border pictures a height takes: borders.tga under the first of `border_heights`, then 2, then 3 [mod: default.map].</summary>
	private int BorderPicture(float height)
	{
		int picture = 0;
		while (picture < _map.BorderHeights.Count && height >= _map.BorderHeights[picture])
		{
			picture++;
		}
		return picture;
	}

	/// <summary>The land borders shown only under `border_cutoff`, so never in the far view, with the picture the height takes.</summary>
	private void ApplyBorderHeight(float height)
	{
		bool shown = height < _map.BorderCutoff;
		if (shown != _bordersShown)
		{
			_bordersShown = shown;
			ApplyCullMask();
		}
		int picture = BorderPicture(height);
		if (picture == _borderPicture)
		{
			return;
		}
		_borderPicture = picture;
		Texture2D? texture = File(picture == 0 ? "borders.tga" : $"borders{picture + 1}.tga");
		foreach (CSurfaceTile part in _parts)
		{
			if (part.Border is { } border)
			{
				SetTexture(border, BorderAll, "tex2", texture);
			}
		}
	}

	/// <summary>What TerrainShader_Border is fed for a tile beyond the shared view: its world matrix and the lookup (B.3).</summary>
	private void SetBorderTile(CSurfaceTile part)
	{
		if (part.Border is not { } border)
		{
			return;
		}
		(int midX, int midY) = part.Tile.Middle(_columns);
		Set(border, BorderAll, "WorldMatrix", Matrix(CD3DMatrix.Translation(midX, 0, midY)));
		SetTexture(border, BorderAll, "tex4", _borderLookupTexture);
	}

	/// <summary>A D3DCOLOR, 0xAARRGGBB, as the shader reads it: each channel over 255.</summary>
	private static Vector4 Colour(uint packed)
	{
		return new Vector4((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF, packed >> 24) / 255f;
	}

	/// <summary>The sea: the map's quad and its copies a width east and west, each (x, z, u, v) (<c>FINDINGS-mapdraw.md</c>, B.1).</summary>
	private void BuildSea()
	{
		var water = _gui.Effects.Get(WaterFile, "WaterNear", EShaderTarget.Spatial, device: WaterStates).Translation;
		float w = _map.Width;
		float h = _map.Height;
		var corners = new List<Vector4>();
		var indices = new List<ushort>();
		foreach (float dx in new[] { -w, 0f, w })
		{
			ushort first = (ushort)corners.Count;
			corners.Add(new Vector4(dx, 0, 0, 0));
			corners.Add(new Vector4(dx + w, 0, 1, 0));
			corners.Add(new Vector4(dx + w, h, 1, 1));
			corners.Add(new Vector4(dx, h, 0, 1));
			indices.AddRange(new[] { first, (ushort)(first + 1), (ushort)(first + 2), first, (ushort)(first + 2), (ushort)(first + 3) });
		}
		// Every technique of water.fx takes the same vertex, so one mesh serves them all.
		_seaMesh = BuildMesh(indices.ToArray(), corners.Count, water, (semantic, v) => semantic == "POSITION" ? corners[v] : null);
	}

	private ArrayMesh _seaMesh = null!;

	/// <summary>Each technique of the sea wanted so far: its material, the translation it was set through and its layer.</summary>
	private readonly Dictionary<string, (ShaderMaterial Material, CGodotShader?[] Translations, uint Layer)> _seaWays = new();

	/// <summary>The first render layer the sea's techniques take, one each from it.</summary>
	private const int FirstSeaLayer = 6;

	/// <summary>A technique of the sea's material and instance, set as water.fx is fed (<c>FINDINGS-mapdraw.md</c>, B.1), where never made.</summary>
	private void MakeSea(string technique)
	{
		if (_seaWays.ContainsKey(technique))
		{
			return;
		}
		var effect = _gui.Effects.Get(WaterFile, technique, EShaderTarget.Spatial, device: WaterStates);
		var material = new ShaderMaterial { Shader = effect.Shader, RenderPriority = SeaPriority };
		CGodotShader?[] translations = { effect.Translation };
		Texture2D? water = File("colormap_water.dds");
		float w = water?.GetWidth() ?? 1;
		float h = water?.GetHeight() ?? 1;
		Set(material, translations, "MinMaxUV", new Vector4(0.5f / w, 0.5f / h, (w - 0.5f) / w, (h - 0.5f) / h));
		SetTexture(material, translations, "ColorTex", water);
		SetTexture(material, translations, "OverlayTex", File("map_overlay_tile.dds"));
		SetTexture(material, translations, "NormalTex", File("sea_normal.dds"));
		SetTexture(material, translations, "FOWTex", _fogTexture);
		SetTexture(material, translations, "color0", _sea0Texture);
		SetTexture(material, translations, "color1", _sea1Texture);
		SetTexture(material, translations, "stripes", File("stripes.dds"));
		uint layer = 1u << (FirstSeaLayer + _seaWays.Count);
		_seaWays[technique] = (material, translations, layer);
		AddChild(Instance($"Sea{technique}", _seaMesh, material, layer));
		if (_camera is { } camera)
		{
			SetSeaCamera(material, translations, camera);
		}
	}

	private void SetSeaCamera(ShaderMaterial material, CGodotShader?[] translations, CMapSurfaceCamera camera)
	{
		Set(material, translations, "CameraPosition", new Vector3(camera.X, 0, camera.Z));
		Set(material, translations, "ViewProjectionMatrix", Matrix(camera.ViewProjection));
	}

	/// <summary>
	/// A mesh whose every channel the translation reads is filled by its semantic from what the original's vertex
	/// declaration holds, the triangles as the cache gives them; never culled by Godot, the effect placing it.
	/// </summary>
	private static MeshInstance3D MakeMesh(string name, ushort[] indices, int count, CGodotShader? translation, ShaderMaterial material,
		Func<string, int, Vector4?> source, uint layer = BaseLayer)
	{
		ArrayMesh mesh = BuildMesh(indices, count, translation, source);
		if (mesh.GetSurfaceCount() > 0)
		{
			mesh.SurfaceSetMaterial(0, material);
		}
		return Instance(name, mesh, null, layer);
	}

	/// <summary>A mesh instance never culled by Godot, the effect placing it, with the material it draws through where given.</summary>
	private static MeshInstance3D Instance(string name, Mesh mesh, ShaderMaterial? material, uint layer = BaseLayer)
	{
		return new MeshInstance3D
		{
			Name = name,
			Mesh = mesh,
			MaterialOverride = material,
			Layers = layer,
			CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
	}

	/// <summary>The mesh alone, so that one mesh may be drawn by several instances, each through a material of its own.</summary>
	private static ArrayMesh BuildMesh(ushort[] indices, int count, CGodotShader? translation, Func<string, int, Vector4?> source)
	{
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Godot.Mesh.ArrayType.Max);
		var vertices = new Vector3[count];
		Godot.Mesh.ArrayFormat format = 0;
		foreach (CShaderVertexInput input in translation?.Inputs ?? Array.Empty<CShaderVertexInput>())
		{
			switch (input.Channel)
			{
				case "VERTEX":
					for (int v = 0; v < count; v++)
					{
						Vector4 value = source(input.Semantic, v) ?? new Vector4(0, 0, 0, 1);
						vertices[v] = new Vector3(value.X, value.Y, value.Z);
					}
					break;
				case "UV" or "UV2":
					var uv = new Vector2[count];
					for (int v = 0; v < count; v++)
					{
						Vector4 value = source(input.Semantic, v) ?? Vector4.Zero;
						uv[v] = new Vector2(value.X, value.Y);
					}
					arrays[(int)(input.Channel == "UV" ? Godot.Mesh.ArrayType.TexUV : Godot.Mesh.ArrayType.TexUV2)] = uv;
					break;
				case "COLOR":
					var colours = new Color[count];
					for (int v = 0; v < count; v++)
					{
						Vector4 value = source(input.Semantic, v) ?? Vector4.One;
						colours[v] = new Color(value.X, value.Y, value.Z, value.W);
					}
					arrays[(int)Godot.Mesh.ArrayType.Color] = colours;
					break;
				case "CUSTOM0" or "CUSTOM1" or "CUSTOM2" or "CUSTOM3":
					int slot = input.Channel[^1] - '0';
					var custom = new float[count * 4];
					for (int v = 0; v < count; v++)
					{
						Vector4 value = source(input.Semantic, v) ?? new Vector4(0, 0, 0, 1);
						custom[4 * v] = value.X;
						custom[4 * v + 1] = value.Y;
						custom[4 * v + 2] = value.Z;
						custom[4 * v + 3] = value.W;
					}
					arrays[(int)Godot.Mesh.ArrayType.Custom0 + slot] = custom;
					format |= (Godot.Mesh.ArrayFormat)((long)Godot.Mesh.ArrayCustomFormat.RgbaFloat
						<< ((int)Godot.Mesh.ArrayFormat.FormatCustom0Shift + slot * (int)Godot.Mesh.ArrayFormat.FormatCustomBits));
					break;
			}
		}
		arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Godot.Mesh.ArrayType.Index] = indices.Select(i => (int)i).ToArray();
		var mesh = new ArrayMesh();
		if (count > 0 && indices.Length > 0)
		{
			mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays, null, null, format);
		}
		return mesh;
	}

	// -------------------------------------------------------------------------------------------- the generated textures

	/// <summary>
	/// The province colour lookups made again from the map mode's colours - the first colour and the second, a palette of
	/// 256 a row - and with them the sea's rasters; in style 0 the first lookup is the province's state instead: red the fog
	/// (<c>FINDINGS-mapdraw.md</c>, B.2).
	/// </summary>
	public void SetColours(Image palette, Image stripes, Func<int, float> knownWell)
	{
		if (!IsInsideTree())
		{
			return;
		}
		// Both palettes are RGBA8 with 256 texels a row, as the lookups are, so a province's four bytes stand at id × 4 in each.
		_colours = palette.GetData();
		_seconds = stripes.GetData();
		_knownWell = knownWell;
		int count = Math.Min(_map.ProvinceCount, Math.Min(LookupSize * LookupSize, Math.Min(_colours.Length, _seconds.Length) / 4));
		var first = new uint[_map.ProvinceCount];
		var second = new uint[_map.ProvinceCount];
		for (int id = 1; id < count; id++)
		{
			int at = id * 4;
			first[id] = 0xFF000000u | (uint)_colours[at] << 16 | (uint)_colours[at + 1] << 8 | _colours[at + 2];
			second[id] = 0xFF000000u | (uint)_seconds[at] << 16 | (uint)_seconds[at + 1] << 8 | _seconds[at + 2];
		}
		WriteLookups();
		BuildSeaRasters(first, second);
		BuildFog(knownWell);
	}

	private byte[] _colours = Array.Empty<byte>();
	private byte[] _seconds = Array.Empty<byte>();
	private Func<int, float> _knownWell = _ => 1f;
	private Func<int, bool> _selected = _ => false;

	/// <summary>
	/// The provinces selected, which the lookups lift (<c>FINDINGS-mapdraw.md</c>, B.9): both lookups written again, as the
	/// original's refresh writes them whole after any change of the selection, the sea's rasters and the fog untouched.
	/// </summary>
	public void SetSelection(Func<int, bool> selected)
	{
		_selected = selected;
		if (IsInsideTree() && _colours.Length > 0)
		{
			WriteLookups();
		}
	}

	/// <summary>
	/// The two province colour lookups (B.2, B.9). In style 0 the first holds the province's state - red the fog, 1.0 known
	/// well and 0.5 less; blue the share frozen, not built, nought - and in both the green is 25 for a selected province and
	/// nought for any other, the second's red and blue its second colour. In any other style each is the mode's colour, and a
	/// selected province's red, green and blue are lifted by 0.1 - 25, or 255 from 230 up - its alpha full [exe].
	/// </summary>
	private void WriteLookups()
	{
		int count = Math.Min(_map.ProvinceCount, Math.Min(LookupSize * LookupSize, Math.Min(_colours.Length, _seconds.Length) / 4));
		var bytes0 = new byte[LookupSize * LookupSize * 4];
		var bytes1 = new byte[LookupSize * LookupSize * 4];
		static byte Lift(byte value) => value >= 230 ? (byte)255 : (byte)(value + 25);
		for (int id = 1; id < count; id++)
		{
			int at = id * 4;
			bool selected = _selected(id);
			if (_style == 0)
			{
				bytes0[at] = _knownWell(id) >= 1f ? (byte)255 : (byte)128;
				bytes0[at + 1] = selected ? (byte)25 : (byte)0;
				bytes1[at] = _seconds[at];
				bytes1[at + 1] = selected ? (byte)25 : (byte)0;
				bytes1[at + 2] = _seconds[at + 2];
			}
			else
			{
				for (int c = 0; c < 3; c++)
				{
					bytes0[at + c] = selected ? Lift(_colours[at + c]) : _colours[at + c];
					bytes1[at + c] = selected ? Lift(_seconds[at + c]) : _seconds[at + c];
				}
			}
			bytes0[at + 3] = 255;
			bytes1[at + 3] = 255;
		}
		_lookup0.SetData(LookupSize, LookupSize, false, Image.Format.Rgba8, bytes0);
		_lookup1.SetData(LookupSize, LookupSize, false, Image.Format.Rgba8, bytes1);
		_lookup0Texture.Update(_lookup0);
		_lookup1Texture.Update(_lookup1);
	}


	/// <summary>The province each texel of the sea's rasters is, read nearest; made once, the map never changing.</summary>
	private ushort[]? _seaSamples;

	/// <summary>
	/// The fog's four samples for each texel, by the province each is - <see cref="Near"/> for a pixel of no province within
	/// three pixels of one, nought for one further - made once, as only how well each province is known changes.
	/// </summary>
	private ushort[]? _fogSamples;

	private const ushort Near = ushort.MaxValue;

	/// <summary>The sea's two rasters: each texel the province under it, read nearest, grey off any province [exe].</summary>
	private void BuildSeaRasters(uint[] first, uint[] second)
	{
		_seaSamples ??= SeaSamples();
		var bytes0 = new byte[RasterSize * RasterSize * 4];
		var bytes1 = new byte[RasterSize * RasterSize * 4];
		for (int at = 0; at < _seaSamples.Length; at++)
		{
			int id = _seaSamples[at];
			Put(bytes0, at, id > 0 && id < first.Length ? first[id] : OffProvince);
			Put(bytes1, at, id > 0 && id < second.Length ? second[id] : OffProvince);
		}
		_sea0.SetData(RasterSize, RasterSize, false, Image.Format.Rgba8, bytes0);
		_sea1.SetData(RasterSize, RasterSize, false, Image.Format.Rgba8, bytes1);
		_sea0Texture.Update(_sea0);
		_sea1Texture.Update(_sea1);
	}

	private ushort[] SeaSamples()
	{
		ReadOnlySpan<ushort> raster = _map.Raster;
		var samples = new ushort[RasterSize * RasterSize];
		for (int y = 0; y < RasterSize; y++)
		{
			int row = (int)((long)y * _map.Height / RasterSize);
			for (int x = 0; x < RasterSize; x++)
			{
				samples[y * RasterSize + x] = raster[row * _map.Width + (int)((long)x * _map.Width / RasterSize)];
			}
		}
		return samples;
	}

	private static void Put(byte[] bytes, int texel, uint argb)
	{
		int at = texel * 4;
		bytes[at] = (byte)(argb >> 16);
		bytes[at + 1] = (byte)(argb >> 8);
		bytes[at + 2] = (byte)argb;
		bytes[at + 3] = (byte)(argb >> 24);
	}

	/// <summary>
	/// The fog: each texel four raster samples blended - 1.0 a province known well, 0.6 one known less, a pixel of no
	/// province 1.0 within three pixels of one - by the fractions of where the texel falls [exe].
	/// </summary>
	private void BuildFog(Func<int, float> knownWell)
	{
		_fogSamples ??= FogSamples();
		var worth = new float[_map.ProvinceCount];
		for (int id = 1; id < worth.Length; id++)
		{
			worth[id] = knownWell(id) >= 1f ? 1f : 0.6f;
		}
		float Of(ushort sample) => sample == Near ? 1f : sample < worth.Length ? worth[sample] : 0f;
		var bytes = new byte[RasterSize * RasterSize];
		for (int y = 0; y < RasterSize; y++)
		{
			float yf = (float)y * _map.Height / RasterSize;
			float fy = yf - MathF.Floor(yf);
			for (int x = 0; x < RasterSize; x++)
			{
				float xf = (float)x * _map.Width / RasterSize;
				float fx = xf - MathF.Floor(xf);
				int at = (y * RasterSize + x) * 4;
				float a = Of(_fogSamples[at]);
				float b = Of(_fogSamples[at + 1]);
				float c = Of(_fogSamples[at + 2]);
				float d = Of(_fogSamples[at + 3]);
				float value = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
				bytes[y * RasterSize + x] = (byte)(255f * value);
			}
		}
		_fog.SetData(RasterSize, RasterSize, false, Image.Format.L8, bytes);
		_fogTexture.Update(_fog);
	}

	private ushort[] FogSamples()
	{
		ReadOnlySpan<ushort> raster = _map.Raster;
		int width = _map.Width;
		int height = _map.Height;
		var samples = new ushort[RasterSize * RasterSize * 4];
		for (int y = 0; y < RasterSize; y++)
		{
			float yf = (float)y * height / RasterSize;
			int y0 = Math.Clamp((int)MathF.Floor(yf + 0.5f), 0, height - 1);
			int y1 = Math.Clamp((int)MathF.Floor(yf + 1.5f), 0, height - 1);
			for (int x = 0; x < RasterSize; x++)
			{
				float xf = (float)x * width / RasterSize;
				int x0 = Math.Clamp((int)MathF.Floor(xf + 0.5f), 0, width - 1);
				int x1 = Math.Clamp((int)MathF.Floor(xf + 1.5f), 0, width - 1);
				int at = (y * RasterSize + x) * 4;
				samples[at] = Sample(raster, x0, y0);
				samples[at + 1] = Sample(raster, x1, y0);
				samples[at + 2] = Sample(raster, x0, y1);
				samples[at + 3] = Sample(raster, x1, y1);
			}
		}
		return samples;
	}

	private ushort Sample(ReadOnlySpan<ushort> raster, int x, int y)
	{
		ushort id = raster[y * _map.Width + x];
		if (id > 0)
		{
			return id;
		}
		for (int d = 1; d <= 3; d++)
		{
			if (Province(raster, x - d, y) || Province(raster, x + d, y) || Province(raster, x, y - d) || Province(raster, x, y + d))
			{
				return Near;
			}
		}
		return 0;
	}

	private bool Province(ReadOnlySpan<ushort> raster, int x, int y)
	{
		return x >= 0 && y >= 0 && x < _map.Width && y < _map.Height && raster[y * _map.Width + x] > 0;
	}
}
