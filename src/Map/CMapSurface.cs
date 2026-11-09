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

	/// <summary>The rivers' and the surf's clock, set once a frame for every material that reads it.</summary>
	private const string SharedTime = "fx_map_time";

	private static readonly Dictionary<string, string> Shared = new()
	{
		["ViewMatrix"] = SharedView,
		["ProjectionMatrix"] = SharedProjection,
	};

	private static readonly Dictionary<string, string> TimedShared = new(Shared) { ["vTime"] = SharedTime };

	private CMap _map = null!;
	private CGuiContext _gui = null!;
	private IReadOnlyList<CQuadTile> _tiles = null!;
	private Content.Map.CMapCache _cache = null!;
	private int _columns;

	/// <summary>Each tile's land and beaches, and the copy of the map its world matrix was last set for.</summary>
	private readonly List<CSurfaceTile> _parts = new();

	/// <summary>The sea's quads, one for each way of drawing it.</summary>
	private readonly List<MeshInstance3D> _seas = new();

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
	/// <param name="waves">settings.txt's <c>waves</c>: whether the surf is made at all, as the original makes its meshes only then.</param>
	/// <param name="trees">settings.txt's <c>renderTrees</c>: whether the trees are planted, as the original plants them only then.</param>
	public void Setup(CMap map, CGuiContext gui, Content.Map.CMapCache cache, IReadOnlyList<CQuadTile> tiles, int columns,
		bool simpleWater, bool waves, bool trees, Content.Map.CBorderCache? corners = null)
	{
		_plantTrees = trees;
		_cache = cache;
		_corners = corners;
		_map = map;
		_gui = gui;
		_tiles = tiles;
		_columns = columns;
		_simpleWater = simpleWater;
		_waves = waves;
	}

	private bool _waves;

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
		AddChild(_rivers);
		AddChild(_surf);
		BuildLand();
		BuildSea();
		BuildWaterBorders();
		BuildRivers();
		BuildSurf();
		AddChild(_trees);
		PlantTrees();
		AddChild(_overlays);
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
		RenderingServer.GlobalShaderParameterAdd(SharedTime, RenderingServer.GlobalShaderParameterType.Float, 0f);
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
					ApplyTrees();
				}
			}
		}
	}

	private bool Far => _level > 0;

	/// <summary>
	/// The player's <c>simpleWater</c>: the sea drawn by water.fx's simple techniques, a flat colour, where on, and by the near
	/// and far ones otherwise. Changed by the game options' Apply, it shows on the next frame, as the original's does; the
	/// techniques it wants are made then, and the rest of its set made ahead as at the start.
	/// </summary>
	public bool SimpleWater
	{
		get => _simpleWater;
		set
		{
			if (_simpleWater == value)
			{
				return;
			}
			_simpleWater = value;
			if (IsInsideTree())
			{
				ShowTechniques();
				_prepareIn = PrepareGap;
			}
		}
	}

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
		// The same style's other height made with it, so that the first crossing of 500 does not make 2,700 materials. They
		// are not drawn ahead of their use: drawing a way under the one shown flickered, and saved a first zoom inwards only
		// some 15 ms.
		if (_wayNodes[way ^ 1] is null)
		{
			MakeWay(way ^ 1);
		}
		_shownWay = way;
		// The sea likewise: its technique's own material, the other height's made with it.
		string sea = WaterTechnique;
		MakeSea(sea);
		string otherSea = sea.Contains("Near") ? sea.Replace("Near", "Far") : sea.Replace("Far", "Near");
		if (otherSea != sea && !_seaWays.ContainsKey(otherSea))
		{
			MakeSea(otherSea);
		}
		_shownSea = sea;
		ApplyCullMask();
	}

	private string? _shownSea;

	/// <summary>
	/// The render layers: everything always drawn on the first, each way's land and beaches on one of its own, the land
	/// borders on another. *Ours*: what is shown is chosen by the camera's cull mask and not by hiding instances - showing or
	/// hiding a few thousand instances cost the rendering server some 120 ms at every crossing of 500, where a mask is one
	/// call and no instance changes.
	/// </summary>
	private const uint BaseLayer = 1u;
	private const uint BorderLayer = 1u << 5;

	/// <summary>The surf's two techniques, one under the first of terrain_sheet_heights and one above, after the sea's six.</summary>
	private const uint SurfNearLayer = 1u << 12;
	private const uint SurfFarLayer = 1u << 13;

	/// <summary>The trees, drawn only under height 100 in the terrain and weather modes.</summary>
	private const uint TreeLayer = 1u << 14;

	private static uint WayLayer(int way) => 1u << (1 + way);

	private Camera3D _camera3D = null!;
	private bool _bordersShown;

	private void ApplyCullMask()
	{
		uint Sea(string? technique) => technique is not null && _seaWays.TryGetValue(technique, out var sea) ? sea.Layer : 0;
		_camera3D.CullMask = BaseLayer | (_shownWay >= 0 ? WayLayer(_shownWay) : 0) | (_bordersShown ? BorderLayer : 0) | Sea(_shownSea)
			| (Far ? SurfFarLayer : SurfNearLayer) | (_treesShown ? TreeLayer : 0);
	}

	/// <summary>
	/// The frames before the first way or sea not yet made is made, and between one and the next: so that the first choice of
	/// a map mode drawn another way - every mode but terrain and weather draws the land by the General techniques, the air and
	/// naval modes the sea by the Color ones - does not stall on some 2,700 materials. They are made and not drawn: drawing
	/// each under the one shown to warm it flickered the map six times as a game began, and made a first switch no faster.
	/// *Ours.*
	/// </summary>
	private const int PrepareGap = 15;
	private int _prepareIn = 2 * PrepareGap;

	/// <summary>Makes the first way, then the first sea technique, that nothing has made yet; nothing once all are.</summary>
	private void PrepareNext()
	{
		for (int way = 0; way < CSurfaceTile.Ways; way++)
		{
			if (_wayNodes[way] is null)
			{
				MakeWay(way);
				return;
			}
		}
		string[] seas = _simpleWater
			? new[] { "WaterSimple", "WaterSimpleColor" }
			: new[] { "WaterNear", "WaterFar", "WaterNearColor", "WaterFarColor" };
		foreach (string sea in seas)
		{
			if (!_seaWays.ContainsKey(sea))
			{
				MakeSea(sea);
				return;
			}
		}
		_prepareIn = int.MaxValue;
	}

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
		Texture2D? index = _cache.Find(CQuadCache.TextureFile(tile.Index)) is { } indexFile ? _gui.Textures.LoadFromFile(indexFile) : null;
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
		ApplyCopies(_camera);
		ApplyTrees();
		ApplyHierarchyView(_camera);
		ApplyFrontView(_camera);
		ApplyPlanView(_camera);
		ApplyRingView(_camera);
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
	/// <summary>The console's <c>debug render</c>: the layers that are a node of their own here.</summary>
	public void SetLayers(bool trees, bool borders, bool rivers, bool water, bool waterBorders)
	{
		_trees.Visible = trees;
		_borders.Visible = borders;
		_rivers.Visible = rivers;
		_waterBorders.Visible = waterBorders;
		_surf.Visible = water;
		foreach (MeshInstance3D sea in _seas)
		{
			sea.Visible = water;
		}
	}

	public override void _ExitTree()
	{
		FreeTreeSlots();
		ClearHierarchy();
		SetRings(new List<CAirBaseCircle>());
		foreach (Node node in GetChildren().Concat(_borders.GetChildren()).Concat(_waterBorders.GetChildren()).Concat(_rivers.GetChildren()))
		{
			if (node is MeshInstance3D instance && instance.Mesh is { } mesh)
			{
				instance.Mesh = null;
				instance.MaterialOverride = null;
				mesh.Dispose();
			}
		}
		// The surf's instances share a mesh between its two techniques, given back once, after them.
		foreach (Node node in _surf.GetChildren())
		{
			if (node is MeshInstance3D instance)
			{
				instance.Mesh = null;
				instance.MaterialOverride = null;
			}
		}
		foreach (ArrayMesh mesh in _surfMeshes)
		{
			mesh.Dispose();
		}
		_surfMeshes.Clear();
		foreach (ShaderMaterial material in _copyMaterials)
		{
			material.Dispose();
		}
		_copyMaterials.Clear();
		_copied.Clear();
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
		if (_camera is not null && --_prepareIn <= 0)
		{
			_prepareIn = PrepareGap;
			PrepareNext();
		}
		if (_shownSea is { } shown && _seaWays.TryGetValue(shown, out var sea))
		{
			Set(sea.Material, sea.Translations, "Time", (float)-_clock);
		}
		// The rivers' and the surf's vTime: the original's clock, whose unit was not read - seconds here, ours. Its wrap at a
		// hundred moves the rivers' flow 30 repeats and the surf's 20, whole, so nothing jumps.
		RenderingServer.GlobalShaderParameterSet(SharedTime, (float)_clock);
		// The trees shown the frame their planting is done, where the camera already wants them.
		if (_planting is { IsCompleted: true } && !_plantingSeen)
		{
			_plantingSeen = true;
			ApplyTrees();
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

	/// <summary>Drawn after the sea and the surf and over everything before, the depth test off [exe] (FINDINGS-mapdraw.md, B.8).</summary>
	private const int WaterBorderPriority = 5;

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

	// ------------------------------------------------------------------------------------------------- rivers and surf

	public const string RiverFile = "river.fx";
	public const string RiverTechnique = "RiverShader";
	public const string SurfFile = "beach.fx";

	/// <summary>The rivers drawn after the beaches and before the land borders, writing no depth (FINDINGS-mapdraw.md, C.1).</summary>
	private const int RiverPriority = 1;

	/// <summary>The surf drawn after the sea and before the water borders (C.1).</summary>
	private const int SurfPriority = 4;

	/// <summary>The height the surf is drawn at, between the sea's and the land's [exe].</summary>
	private const float WaterHeight = 0.31f;

	/// <summary>A strip's vertices are counted in sixteen bits, so a tile's rivers are cut into meshes of no more.</summary>
	private const int MeshVertices = 65535;

	private static readonly Dictionary<string, string> RiverStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"),
		("DESTBLEND", "INVSRCALPHA"), ("ZWRITEENABLE", "0"));

	/// <summary>BeachShader sets its own SRCALPHA / ONE; BeachShaderColor leaves what the sea left, SRCALPHA / INVSRCALPHA (C.2).</summary>
	private static readonly Dictionary<string, string> SurfStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"),
		("DESTBLEND", "INVSRCALPHA"));

	private readonly Node3D _rivers = new() { Name = "Rivers" };
	private readonly Node3D _surf = new() { Name = "Surf" };

	/// <summary>The surf's meshes, each drawn by two instances, one for each technique, and so given back apart.</summary>
	private readonly List<ArrayMesh> _surfMeshes = new();

	/// <summary>Every set of three materials made for the copies of the map a width west, where it is and a width east.</summary>
	private readonly List<ShaderMaterial> _copyMaterials = new();

	/// <summary>Each river or surf instance with the middle of its tile, the copy it was last given and its three materials.</summary>
	private readonly List<(MeshInstance3D Instance, int MiddleX, int Copy, ShaderMaterial[] Materials)> _copied = new();

	/// <summary>
	/// Three materials of an effect, one for each copy of the map, the world matrix each copy's shift. *Ours*, as the water
	/// borders': the vertices hold the map's own coordinates, and the original's world matrix - a tile's middle less the
	/// camera's place - is folded into them and the shared view, the product the same.
	/// </summary>
	private ShaderMaterial[] CopyMaterials(Shader shader, CGodotShader?[] translations, int priority, Action<ShaderMaterial> set)
	{
		var materials = new ShaderMaterial[3];
		for (int copy = 0; copy < 3; copy++)
		{
			var material = new ShaderMaterial { Shader = shader, RenderPriority = priority };
			Set(material, translations, "WorldMatrix", Matrix(CD3DMatrix.Translation((copy - 1) * _map.Width, 0, 0)));
			set(material);
			materials[copy] = material;
			_copyMaterials.Add(material);
		}
		return materials;
	}

	/// <summary>An instance drawn through the middle copy's material until the camera says otherwise.</summary>
	private void AddCopied(Node3D parent, string name, Mesh mesh, ShaderMaterial[] materials, int middleX, uint layer)
	{
		MeshInstance3D instance = Instance(name, mesh, materials[1], layer);
		parent.AddChild(instance);
		_copied.Add((instance, middleX, 1, materials));
	}

	/// <summary>Each river and surf instance given the copy of the map nearest the camera.</summary>
	private void ApplyCopies(CMapSurfaceCamera camera)
	{
		for (int i = 0; i < _copied.Count; i++)
		{
			(MeshInstance3D instance, int middleX, int copy, ShaderMaterial[] materials) = _copied[i];
			int wanted = 1 + (int)MathF.Round(camera.Shift(middleX) / _map.Width);
			if (wanted != copy)
			{
				instance.MaterialOverride = materials[wanted];
				_copied[i] = (instance, middleX, wanted, materials);
			}
		}
	}

	/// <summary>
	/// The rivers traced out of the map's river bitmap and made into strips as the original makes them at the map's build
	/// (FINDINGS-mapdraw.md, C.6), drawn through river.fx at every height, a tile's rivers - by the tile of their source - in
	/// one mesh, and the mouths in another through river_exit.dds. *Ours*: the sources are read from the bitmap, where the
	/// original takes them from map.bin's list, which holds the same pixels in the same order while that cache is the map's.
	/// </summary>
	private void BuildRivers()
	{
		if (_map.RiversFile is not { } file)
		{
			return;
		}
		var effect = _gui.Effects.Get(RiverFile, RiverTechnique, EShaderTarget.Spatial, device: RiverStates, shared: TimedShared);
		if (effect.Shader is null)
		{
			return;
		}
		CGodotShader?[] translations = { effect.Translation };
		Texture2D? water = File("colormap_water.dds");
		Texture2D? exitPicture = File("river_exit.dds");
		void Common(ShaderMaterial material)
		{
			// The colour map's size twice, the texture kept at the picture's own size - ours, as the land's.
			float width = water?.GetWidth() ?? 1, height = water?.GetHeight() ?? 1;
			Set(material, translations, "ColorMapWidth", width);
			Set(material, translations, "ColorMapHeight", height);
			Set(material, translations, "ColorMapTextureWidth", width);
			Set(material, translations, "ColorMapTextureHeight", height);
			Set(material, translations, "MapWidth", (float)_map.Width);
			Set(material, translations, "MapHeight", (float)_map.Height);
			Set(material, translations, "AbsoluteWorldMatrix", Matrix(CD3DMatrix.Translation(0, 0, 0)));
			SetTexture(material, translations, "tex1", File("river_movement.tga"));
			SetTexture(material, translations, "tex3", water);
		}
		ShaderMaterial[] bodies = CopyMaterials(effect.Shader, translations, RiverPriority, material =>
		{
			Common(material);
			SetTexture(material, translations, "tex0", File("river_body.tga"));
		});
		ShaderMaterial[]? mouths = null;

		byte[] raster = CRiverTracer.Raster(CBitmapFile.Read(file));
		void Warn(string problem) => GD.PushWarning($"map: {problem}");
		IReadOnlyList<(int X, int Y)> sources = CMapBin.RiverSources(_cache, raster, _map.Width, _map.Height, Warn);
		List<CRiverPiece> pieces = CRiverTracer.Trace(raster, _map.Width, _map.Height, sources, _map.IsWaterPixel, Warn);
		float frameWidth = exitPicture is { } picture ? 128f / picture.GetWidth() : 0.25f;
		List<CRiverStrip> strips = CRiverMeshes.Build(pieces, (x, y) => _map.IsSea(_map.ProvinceAt(x, y)), frameWidth);
		int columns = _map.Width / CQuadTile.Size + 1;
		var byTile = new SortedDictionary<int, List<int>>();
		for (int i = 0; i < pieces.Count; i++)
		{
			CRiverNode source = pieces[i].Nodes[0];
			int tile = source.X / CQuadTile.Size + columns * (source.Y / CQuadTile.Size);
			if (!byTile.TryGetValue(tile, out List<int>? list))
			{
				byTile[tile] = list = new List<int>();
			}
			list.Add(i);
		}
		foreach ((int tile, List<int> members) in byTile)
		{
			int middleX = tile % columns * CQuadTile.Size + CQuadTile.Size / 2;
			var vertices = new List<CRiverVertex>();
			var indices = new List<ushort>();
			var exitVertices = new List<CRiverVertex>();
			var exitIndices = new List<ushort>();
			int part = 0;
			void Flush()
			{
				if (indices.Count > 0)
				{
					AddCopied(_rivers, $"River{tile}_{part++}", RiverMesh(vertices, indices, effect.Translation), bodies, middleX, BaseLayer);
				}
				vertices.Clear();
				indices.Clear();
			}
			foreach (int i in members)
			{
				CRiverStrip strip = strips[i];
				if (vertices.Count + strip.Vertices.Count > MeshVertices)
				{
					Flush();
				}
				Append(vertices, indices, strip.Vertices);
				if (strip.Exit is { } exit)
				{
					Append(exitVertices, exitIndices, exit);
				}
			}
			Flush();
			if (exitIndices.Count > 0)
			{
				mouths ??= CopyMaterials(effect.Shader, translations, RiverPriority, material =>
				{
					Common(material);
					SetTexture(material, translations, "tex0", exitPicture);
				});
				AddCopied(_rivers, $"RiverMouths{tile}", RiverMesh(exitVertices, exitIndices, effect.Translation), mouths, middleX, BaseLayer);
			}
		}
	}

	/// <summary>A strip's vertices added to a mesh's, its triangles as a list.</summary>
	private static void Append(List<CRiverVertex> vertices, List<ushort> indices, IReadOnlyList<CRiverVertex> strip)
	{
		int first = vertices.Count;
		vertices.AddRange(strip);
		foreach (int index in CRiverStrip.StripTriangles(strip.Count))
		{
			indices.Add((ushort)(first + index));
		}
	}

	private static ArrayMesh RiverMesh(List<CRiverVertex> vertices, List<ushort> indices, CGodotShader? translation)
	{
		return BuildMesh(indices.ToArray(), vertices.Count, translation, (semantic, v) => semantic switch
		{
			"POSITION" => new Vector4(vertices[v].Position.X, vertices[v].Position.Y, vertices[v].Position.Z, 1),
			"TEXCOORD0" => new Vector4(vertices[v].Body.X, vertices[v].Body.Y, 0, 1),
			"TEXCOORD1" => new Vector4(vertices[v].Flow.X, vertices[v].Flow.Y, 0, 1),
			_ => null,
		});
	}

	/// <summary>
	/// The surf: each tile's wave pixels made into quads as the original makes them where settings.txt's <c>waves</c> is on
	/// (FINDINGS-mapdraw.md, C.7), one mesh a tile drawn through beach.fx's BeachShader near and BeachShaderColor far - the
	/// foam over the shallows and the coast's black line. *Ours*: each vertex's x and z are the map's own, twice the tile's
	/// middle added to the four shorts' half pixels, so that the copies' three materials serve every tile.
	/// </summary>
	private void BuildSurf()
	{
		if (!_waves)
		{
			return;
		}
		var near = _gui.Effects.Get(SurfFile, "BeachShader", EShaderTarget.Spatial, device: SurfStates, shared: TimedShared);
		var far = _gui.Effects.Get(SurfFile, "BeachShaderColor", EShaderTarget.Spatial, device: SurfStates, shared: TimedShared);
		if (near.Shader is null || far.Shader is null)
		{
			return;
		}
		ShaderMaterial[] Materials(Shader shader, CGodotShader? translation, string alpha)
		{
			CGodotShader?[] translations = { translation };
			return CopyMaterials(shader, translations, SurfPriority, material =>
			{
				Set(material, translations, "vWaterHeight", WaterHeight);
				SetTexture(material, translations, "tex0", File("shore.tga"));
				SetTexture(material, translations, "tex1", File(alpha));
				SetTexture(material, translations, "tex2", File("shore_alpha.tga"));
			});
		}
		ShaderMaterial[] nearMaterials = Materials(near.Shader, near.Translation, "shore_alpha.tga");
		ShaderMaterial[] farMaterials = Materials(far.Shader, far.Translation, "shore_alpha_outline.tga");
		int skipped = 0;
		foreach (CQuadTile tile in _tiles)
		{
			CWavePixel[] pixels = CWavePixel.Parse(tile.ShoreRecords);
			if (pixels.Length == 0)
			{
				continue;
			}
			(int midX, int midY) = tile.Middle(_columns);
			List<short> shorts = CSurfQuads.Vertices(pixels, midX, midY, out int bad);
			skipped += bad;
			int quads = shorts.Count / 16;
			var indices = new ushort[quads * CSurfQuads.Triangles.Length];
			for (int q = 0; q < quads; q++)
			{
				for (int k = 0; k < CSurfQuads.Triangles.Length; k++)
				{
					indices[q * CSurfQuads.Triangles.Length + k] = (ushort)(4 * q + CSurfQuads.Triangles[k]);
				}
			}
			ArrayMesh mesh = BuildMesh(indices, quads * 4, near.Translation, (semantic, v) => semantic == "POSITION"
				? new Vector4(shorts[4 * v] + 2 * midX, shorts[4 * v + 1] + 2 * midY, shorts[4 * v + 2], shorts[4 * v + 3])
				: null);
			_surfMeshes.Add(mesh);
			AddCopied(_surf, $"SurfNear{tile.Index}", mesh, nearMaterials, midX, SurfNearLayer);
			AddCopied(_surf, $"SurfFar{tile.Index}", mesh, farMaterials, midX, SurfFarLayer);
		}
		if (skipped > 0)
		{
			GD.PushWarning($"map: {skipped} wave pixels of the quad cache are of no known type and make no surf");
		}
	}

	// ----------------------------------------------------------------------------------------------------------- trees

	public const string TreeTechnique = "TreeShader";

	/// <summary>The trees drawn last of the surface, after the sea zones' borders (FINDINGS-mapdraw.md, C.1).</summary>
	private const int TreePriority = 6;

	/// <summary>Trees are drawn only under this height [exe].</summary>
	private const float TreeHeight = 100f;

	/// <summary>The tiles that may show trees at once, as the original's slots [exe].</summary>
	private const int TreeSlots = 16;

	private static readonly Dictionary<string, string> TreeStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"),
		("DESTBLEND", "INVSRCALPHA"));

	private readonly Node3D _trees = new() { Name = "Trees" };
	private bool _plantTrees;
	private bool _treesShown;
	private bool _plantingSeen;
	private System.Threading.Tasks.Task<CTreeField?>? _planting;
	private ShaderMaterial[][]? _treeMaterials;
	private CGodotShader? _treeTranslation;

	/// <summary>Each slot's tile, or -1, and the instances drawing it, one for each tree type it holds.</summary>
	private readonly (int Tile, List<MeshInstance3D> Instances)[] _treeSlots =
		Enumerable.Range(0, TreeSlots).Select(_ => (-1, new List<MeshInstance3D>())).ToArray();

	private Func<int, bool>? _treeSnow;

	/// <summary>
	/// Whether a province's trees are drawn snowed - in the weather mode, colder than −5 - or null in any other mode. Set as
	/// the mode is chosen, which frees every slot, as the original's choosing a mode does; a slot's snow is then fixed until
	/// it is freed again [exe].
	/// </summary>
	public Func<int, bool>? TreeSnow
	{
		get => _treeSnow;
		set
		{
			_treeSnow = value;
			FreeTreeSlots();
			ApplyTrees();
		}
	}

	/// <summary>
	/// The trees planted as the original plants them as the map is made (FINDINGS-mapdraw.md, C.8), where settings.txt's
	/// <c>renderTrees</c> is on. *Ours*: on a thread of its own, about 0.4 s in the optimised build, so that making the
	/// surface does not wait on it; no trees are drawn until it is done.
	/// </summary>
	private void PlantTrees()
	{
		if (!_plantTrees || _map.TerrainFile is not { } file || _map.TreeTypes.Count == 0)
		{
			return;
		}
		var effect = Terrain(TreeTechnique, TreeStates);
		if (effect.Shader is null)
		{
			return;
		}
		_treeTranslation = effect.Translation;
		CGodotShader?[] translations = { effect.Translation };
		_treeMaterials = _map.TreeTypes.Select(type => CopyMaterials(effect.Shader, translations, TreePriority, material =>
		{
			Set(material, translations, "vAlpha", 1f);
			Set(material, translations, "BorderWidth", (float)CQuadTile.Size);
			Set(material, translations, "BorderHeight", (float)CQuadTile.Size);
			Set(material, translations, "AbsoluteWorldMatrix", Matrix(CD3DMatrix.Translation(0, 0, 0)));
			SetTexture(material, translations, "tex0", _gui.Textures.LoadFromGamePath("map/" + type.Texture));
		})).ToArray();
		CMap map = _map;
		_planting = System.Threading.Tasks.Task.Run<CTreeField?>(() =>
		{
			byte[] terrain = CBitmapFile.Read(file).NorthRaster();
			return CTreeField.Build(terrain, map.Width, map.Height, map.ProvinceAt, map.IsSea, map.TreeTypeOfIndex,
				map.TreeRowOfIndex, map.TreeTypes.Select(t => t.Density).ToArray());
		});
	}

	/// <summary>
	/// The trees under height 100 in the terrain and weather modes [exe]: each tile in view, in the tiles' order, that has no
	/// slot takes a free one - one with no tile, else one whose tile is out of view - until the sixteen are taken, as the
	/// original's slots are filled; a slot keeps its tile while it stays in view.
	/// </summary>
	private void ApplyTrees()
	{
		bool shown = _camera is { } camera && _style == 0 && camera.Height < TreeHeight
			&& _planting is { IsCompletedSuccessfully: true, Result: not null };
		if (shown != _treesShown)
		{
			_treesShown = shown;
			ApplyCullMask();
		}
		if (!shown)
		{
			return;
		}
		CTreeField field = _planting!.Result!;
		List<int> visible = VisibleTiles(_camera!, field.Columns, field.Tiles.Length);
		foreach (int tile in visible)
		{
			if (Array.Exists(_treeSlots, s => s.Tile == tile))
			{
				continue;
			}
			int free = Array.FindIndex(_treeSlots, s => s.Tile < 0);
			if (free < 0)
			{
				free = Array.FindIndex(_treeSlots, s => !visible.Contains(s.Tile));
			}
			if (free < 0)
			{
				break;
			}
			FillTreeSlot(free, tile, field);
		}
	}

	/// <summary>
	/// The tiles the camera looks down on, in the tiles' order: the ground in view, the camera looking straight down, two
	/// pixels more each way for a tree's reach, the map wrapping east and west. *Ours*, for the original's list of visible
	/// tiles, which was not read.
	/// </summary>
	private List<int> VisibleTiles(CMapSurfaceCamera camera, int columns, int count)
	{
		float halfHeight = camera.Height * MathF.Tan(CMapSurfaceCamera.FieldOfView / 2f) + 2;
		float halfWidth = (halfHeight - 2) * (_screen.X / MathF.Max(1, _screen.Y)) + 2;
		int rows = count / columns;
		int rowFrom = Math.Max(0, (int)MathF.Floor((camera.Z - halfHeight) / CQuadTile.Size));
		int rowTo = Math.Min(rows - 1, (int)MathF.Floor((camera.Z + halfHeight) / CQuadTile.Size));
		var columnsSeen = new SortedSet<int>();
		for (int x = (int)MathF.Floor(camera.X - halfWidth); x <= (int)MathF.Ceiling(camera.X + halfWidth); x++)
		{
			columnsSeen.Add(((x % _map.Width) + _map.Width) % _map.Width / CQuadTile.Size);
		}
		var tiles = new List<int>();
		for (int row = rowFrom; row <= rowTo; row++)
		{
			foreach (int column in columnsSeen)
			{
				tiles.Add(column + columns * row);
			}
		}
		return tiles;
	}

	/// <summary>A slot given a tile: a mesh for each tree type the tile holds, each tree its 16 vertices (C.8).</summary>
	private void FillTreeSlot(int slot, int tile, CTreeField field)
	{
		ClearTreeSlot(slot);
		List<MeshInstance3D> instances = _treeSlots[slot].Instances;
		int middleX = tile % field.Columns * CQuadTile.Size + CQuadTile.Size / 2;
		for (int type = 0; type < _treeMaterials!.Length; type++)
		{
			List<float> vertices = field.Vertices(tile, type, _treeSnow);
			int trees = vertices.Count / (CTreeField.VerticesPerTree * CTreeField.Stride);
			if (trees == 0)
			{
				continue;
			}
			var indices = new ushort[trees * CTreeField.Triangles.Length];
			for (int p = 0; p < trees; p++)
			{
				for (int k = 0; k < CTreeField.Triangles.Length; k++)
				{
					indices[p * CTreeField.Triangles.Length + k] = (ushort)(CTreeField.VerticesPerTree * p + CTreeField.Triangles[k]);
				}
			}
			ArrayMesh mesh = BuildMesh(indices, trees * CTreeField.VerticesPerTree, _treeTranslation, (semantic, v) => semantic switch
			{
				"POSITION" => new Vector4(vertices[5 * v], vertices[5 * v + 1], vertices[5 * v + 2], 1),
				"TEXCOORD0" => new Vector4(vertices[5 * v + 3], vertices[5 * v + 4], 0, 1),
				_ => null,
			});
			ShaderMaterial[] materials = _treeMaterials[type];
			int copy = _camera is { } camera ? 1 + (int)MathF.Round(camera.Shift(middleX) / _map.Width) : 1;
			MeshInstance3D instance = Instance($"Trees{tile}_{type}", mesh, materials[copy], TreeLayer);
			_trees.AddChild(instance);
			_copied.Add((instance, middleX, copy, materials));
			instances.Add(instance);
		}
		_treeSlots[slot] = (tile, instances);
	}

	private void ClearTreeSlot(int slot)
	{
		List<MeshInstance3D> instances = _treeSlots[slot].Instances;
		_copied.RemoveAll(c => instances.Contains(c.Instance));
		foreach (MeshInstance3D instance in instances)
		{
			Mesh? mesh = instance.Mesh;
			instance.Mesh = null;
			instance.MaterialOverride = null;
			_trees.RemoveChild(instance);
			instance.QueueFree();
			mesh?.Dispose();
		}
		instances.Clear();
		_treeSlots[slot] = (-1, instances);
	}

	private void FreeTreeSlots()
	{
		for (int slot = 0; slot < TreeSlots; slot++)
		{
			ClearTreeSlot(slot);
		}
	}

	// ----------------------------------------------------------------------- the hierarchy's lines and the air bases' rings

	public const string HierarchyFile = "hierarchy.fx";
	public const string HierarchyTechnique = "Hierarchy";
	public const string HierarchyPicture = "gfx/mapitems/hierarchy_line.tga";
	public const string CircleFile = "circle.fx";
	public const string FrontFile = "front.fx";
	public const string FrontTechnique = "Front";
	public const string FrontPicture = "gfx/mapitems/front_arrow.tga";
	public const string CirclePicture = "map/Sector.dds";

	/// <summary>
	/// The rings drawn after the whole map and its pictures, and the hierarchy's lines after them, both before the counters,
	/// which are this project's 2D layer over the surface (FINDINGS-airmap.md, 8.4; FINDINGS-hierarchy.md, 7.4).
	/// </summary>
	private const int RingPriority = 7;
	private const int HierarchyPriority = 8;

	/// <summary>The rings stand 1.0 above the ground [exe].</summary>
	private const float RingHeight = 1.0f;

	/// <summary>Neither is depth tested or writes depth: the rings' pass turns both off, the lines' draw does [exe].</summary>
	private static readonly Dictionary<string, string> OverlayStates = Baseline(("ALPHABLENDENABLE", "1"), ("SRCBLEND", "SRCALPHA"),
		("DESTBLEND", "INVSRCALPHA"), ("ZENABLE", "0"), ("ZWRITEENABLE", "0"));

	private readonly Node3D _overlays = new() { Name = "Overlays" };
	private readonly Dictionary<int, List<MeshInstance3D>> _fronts = new();
	private ShaderMaterial? _frontMaterial;
	private CGodotShader?[] _frontTranslations = Array.Empty<CGodotShader?>();

	/// <summary>The lines' two materials: the mesh where it is, and the mesh a map's width east [exe].</summary>
	private ShaderMaterial[]? _hierarchyMaterials;
	private CGodotShader?[] _hierarchyTranslations = Array.Empty<CGodotShader?>();
	private readonly List<MeshInstance3D> _hierarchyInstances = new();
	private ArrayMesh? _hierarchyMesh;
	private List<CHierarchyLine> _hierarchyLines = new();

	/// <summary>Each ring's instance, material and centre, its world matrix moved to the copy of the map nearest the camera.</summary>
	private readonly List<(MeshInstance3D Instance, ShaderMaterial Material, CAirBaseCircle Circle)> _rings = new();
	private CGodotShader?[] _ringTranslations = Array.Empty<CGodotShader?>();
	private Shader? _ringShader;
	private List<CAirBaseCircle> _ringCircles = new();

	/// <summary>
	/// The selected unit's hierarchy as the original draws it through hierarchy.fx (FINDINGS-hierarchy.md, section 7): four
	/// vertices a line, from the unit's unit point to its commander's, half as wide as (size + 1) × 0.2, its v running by
	/// length, (u, v, range) in the second channel and its colour in the third; one mesh drawn where it is and a map's width
	/// east. Made again only where the lines differ; none, an empty list. Positions in the map's own coordinates, y north.
	/// </summary>
	public void SetHierarchy(List<CHierarchyLine> lines)
	{
		if (lines.Count == _hierarchyLines.Count && lines.SequenceEqual(_hierarchyLines))
		{
			return;
		}
		_hierarchyLines = lines;
		ClearHierarchy();
		if (lines.Count == 0 || HierarchyMaterials() is not { } materials)
		{
			return;
		}
		var vertices = new List<(Godot.Vector2 Position, Vector3 UvRange, Color Colour)>();
		var indices = new List<ushort>();
		Span<System.Numerics.Vector2> corners = stackalloc System.Numerics.Vector2[4];
		foreach (CHierarchyLine line in lines)
		{
			if (!CHierarchyLines.Corners(line, corners, out float pictures) || vertices.Count + 4 > MeshVertices)
			{
				continue;
			}
			float range = line.IsBroken ? 0f : 1f;
			Color colour = line.Colour.ToGodot();
			ushort n = (ushort)vertices.Count;
			vertices.Add((new Godot.Vector2(corners[0].X, corners[0].Y), new Vector3(0, 0, range), colour));
			vertices.Add((new Godot.Vector2(corners[1].X, corners[1].Y), new Vector3(1, 0, range), colour));
			vertices.Add((new Godot.Vector2(corners[2].X, corners[2].Y), new Vector3(0, pictures, range), colour));
			vertices.Add((new Godot.Vector2(corners[3].X, corners[3].Y), new Vector3(1, pictures, range), colour));
			indices.AddRange(new[] { n, (ushort)(n + 1), (ushort)(n + 2), (ushort)(n + 1), (ushort)(n + 3), (ushort)(n + 2) });
		}
		if (indices.Count == 0)
		{
			return;
		}
		_hierarchyMesh = BuildMesh(indices.ToArray(), vertices.Count, _hierarchyTranslations[0], (semantic, v) => semantic switch
		{
			"POSITION" => new Vector4(vertices[v].Position.X, vertices[v].Position.Y, 0, 1),
			"TEXCOORD0" => new Vector4(vertices[v].UvRange.X, vertices[v].UvRange.Y, vertices[v].UvRange.Z, 1),
			"COLOR0" or "COLOR" => new Vector4(vertices[v].Colour.R, vertices[v].Colour.G, vertices[v].Colour.B, vertices[v].Colour.A),
			_ => null,
		});
		foreach (ShaderMaterial material in materials)
		{
			MeshInstance3D instance = Instance("Hierarchy", _hierarchyMesh, material);
			_overlays.AddChild(instance);
			_hierarchyInstances.Add(instance);
		}
		if (_camera is { } camera)
		{
			ApplyHierarchyView(camera);
		}
	}

	/// <summary>One unit's front lines drawn, a mesh each, in the place of any it had (mapunits.md, section 11).</summary>
	public void SetFront(int unit, IReadOnlyList<CFrontLines.CStrip> strips)
	{
		ClearFront(unit);
		if (FrontMaterial() is not { } material)
		{
			return;
		}
		var instances = new List<MeshInstance3D>();
		foreach (CFrontLines.CStrip strip in strips)
		{
			ArrayMesh mesh = BuildMesh(strip.Indices, strip.Positions.Length, _frontTranslations[0], (semantic, v) => semantic switch
			{
				"POSITION" => new Vector4(strip.Positions[v].X, strip.Positions[v].Y, 0, 1),
				"TEXCOORD0" => new Vector4(strip.Uvs[v].X, strip.Uvs[v].Y, 0, 1),
				_ => null,
			});
			MeshInstance3D instance = Instance("Front", mesh, material);
			_overlays.AddChild(instance);
			instances.Add(instance);
		}
		_fronts[unit] = instances;
		if (_camera is { } camera)
		{
			ApplyFrontView(camera);
		}
	}

	public void ClearFront(int unit)
	{
		if (!_fronts.Remove(unit, out List<MeshInstance3D>? instances))
		{
			return;
		}
		foreach (MeshInstance3D instance in instances)
		{
			Mesh? mesh = instance.Mesh;
			instance.Mesh = null;
			_overlays.RemoveChild(instance);
			instance.QueueFree();
			mesh?.Dispose();
		}
	}

	private ShaderMaterial? FrontMaterial()
	{
		if (_frontMaterial is not null)
		{
			return _frontMaterial;
		}
		var effect = _gui.Effects.Get(FrontFile, FrontTechnique, EShaderTarget.Spatial, device: OverlayStates);
		if (effect.Shader is null)
		{
			return null;
		}
		_frontTranslations = new[] { effect.Translation };
		_frontMaterial = new ShaderMaterial { Shader = effect.Shader, RenderPriority = RingPriority };
		// *Ours*: the shared view holds the camera's place, so the effect's camera position is nought.
		Set(_frontMaterial, _frontTranslations, "CameraPosition", Godot.Vector2.Zero);
		SetTexture(_frontMaterial, _frontTranslations, "ArrowTexture", _gui.Textures.LoadFromGamePath(FrontPicture));
		return _frontMaterial;
	}

	/// <summary>The lines' view × projection, drawn once and not again a map's width over [exe].</summary>
	private void ApplyFrontView(CMapSurfaceCamera camera)
	{
		if (_frontMaterial is not null)
		{
			Set(_frontMaterial, _frontTranslations, "WorldViewProjectionMatrix", Matrix(CD3DMatrix.Multiply(camera.ViewAt, camera.Projection)));
		}
	}

	/// <summary>The lines' phase, which runs the arrowheads; the effect's Time.</summary>
	public void SetHierarchyPhase(float phase)
	{
		if (_hierarchyMaterials is { } materials)
		{
			foreach (ShaderMaterial material in materials)
			{
				Set(material, _hierarchyTranslations, "Time", phase);
			}
		}
	}

	private ShaderMaterial[]? HierarchyMaterials()
	{
		if (_hierarchyMaterials is not null)
		{
			return _hierarchyMaterials;
		}
		var effect = _gui.Effects.Get(HierarchyFile, HierarchyTechnique, EShaderTarget.Spatial, device: OverlayStates);
		if (effect.Shader is null)
		{
			return null;
		}
		_hierarchyTranslations = new[] { effect.Translation };
		Texture2D? picture = _gui.Textures.LoadFromGamePath(HierarchyPicture);
		_hierarchyMaterials = new ShaderMaterial[2];
		for (int copy = 0; copy < 2; copy++)
		{
			var material = new ShaderMaterial { Shader = effect.Shader, RenderPriority = HierarchyPriority };
			// *Ours*: the shared view holds the camera's place, so the effect's camera position is nought.
			Set(material, _hierarchyTranslations, "CameraPosition", Godot.Vector2.Zero);
			SetTexture(material, _hierarchyTranslations, "LineTexture", picture);
			_hierarchyMaterials[copy] = material;
			_copyMaterials.Add(material);
		}
		return _hierarchyMaterials;
	}

	private void ClearHierarchy()
	{
		foreach (MeshInstance3D instance in _hierarchyInstances)
		{
			instance.Mesh = null;
			_overlays.RemoveChild(instance);
			instance.QueueFree();
		}
		_hierarchyInstances.Clear();
		_hierarchyMesh?.Dispose();
		_hierarchyMesh = null;
	}

	/// <summary>The lines' view × projection, the second a map's width east [exe].</summary>
	private void ApplyHierarchyView(CMapSurfaceCamera camera)
	{
		if (_hierarchyMaterials is not { } materials)
		{
			return;
		}
		float[] viewProjection = CD3DMatrix.Multiply(camera.ViewAt, camera.Projection);
		for (int copy = 0; copy < 2; copy++)
		{
			float[] world = CD3DMatrix.Translation(copy * _map.Width, 0, 0);
			Set(materials[copy], _hierarchyTranslations, "WorldViewProjectionMatrix", Matrix(CD3DMatrix.Multiply(world, viewProjection)));
		}
	}

	/// <summary>
	/// The air map mode's rings as the original draws them through circle.fx (FINDINGS-airmap.md, section 8): nine vertices
	/// about the air base, Sector.dds mirrored into each quarter with its corner (1, 1) at the centre, two triangles a quarter,
	/// 1.0 above the ground; vStrength the share and the dimming. Made again only where the rings differ; none, an empty list.
	/// </summary>
	public void SetRings(List<CAirBaseCircle> circles)
	{
		if (circles.Count == _ringCircles.Count && circles.SequenceEqual(_ringCircles))
		{
			return;
		}
		_ringCircles = circles;
		foreach ((MeshInstance3D instance, ShaderMaterial material, CAirBaseCircle _) in _rings)
		{
			Mesh? mesh = instance.Mesh;
			instance.Mesh = null;
			instance.MaterialOverride = null;
			_overlays.RemoveChild(instance);
			instance.QueueFree();
			mesh?.Dispose();
			material.Dispose();
		}
		_rings.Clear();
		if (circles.Count == 0)
		{
			return;
		}
		if (_ringShader is null)
		{
			var effect = _gui.Effects.Get(CircleFile, "Standard", EShaderTarget.Spatial, device: OverlayStates, shared: Shared);
			if (effect.Shader is null)
			{
				return;
			}
			_ringShader = effect.Shader;
			_ringTranslations = new[] { effect.Translation };
		}
		Texture2D? sector = _gui.Textures.LoadFromGamePath(CirclePicture);
		// The nine corners and the quarters' triangles [exe].
		ushort[] indices = { 3, 2, 1, 3, 1, 0, 0, 1, 8, 0, 8, 7, 4, 3, 0, 4, 0, 5, 5, 0, 7, 5, 7, 6 };
		foreach (CAirBaseCircle circle in circles)
		{
			float r = circle.Radius;
			(float X, float Z, float U, float V)[] corners =
			{
				(0, 0, 1, 1), (0, r, 1, 0), (-r, r, 0, 0), (-r, 0, 0, 1), (-r, -r, 0, 0),
				(0, -r, 1, 0), (r, -r, 0, 0), (r, 0, 0, 1), (r, r, 0, 0),
			};
			ArrayMesh mesh = BuildMesh(indices, corners.Length, _ringTranslations[0], (semantic, v) => semantic switch
			{
				"POSITION" => new Vector4(corners[v].X, 0, corners[v].Z, 1),
				"TEXCOORD0" => new Vector4(corners[v].U, corners[v].V, 0, 1),
				_ => null,
			});
			var material = new ShaderMaterial { Shader = _ringShader, RenderPriority = RingPriority };
			Set(material, _ringTranslations, "vStrength", new Godot.Vector2(circle.Share, circle.Dim));
			SetTexture(material, _ringTranslations, "SectorTex", sector);
			MeshInstance3D instance = Instance($"Ring{circle.Province}", mesh, material);
			_overlays.AddChild(instance);
			_rings.Add((instance, material, circle));
		}
		if (_camera is { } camera)
		{
			ApplyRingView(camera);
		}
	}

	/// <summary>Each ring's world matrix: its centre 1.0 up, moved to the copy of the map nearest the camera.</summary>
	private void ApplyRingView(CMapSurfaceCamera camera)
	{
		foreach ((MeshInstance3D _, ShaderMaterial material, CAirBaseCircle circle) in _rings)
		{
			float shift = camera.Shift(circle.X);
			Set(material, _ringTranslations, "WorldMatrix", Matrix(CD3DMatrix.Translation(circle.X + shift, RingHeight, circle.Y)));
		}
	}

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

	/// <summary>The land borders drawn after the land, the beaches and the rivers and before the sea, as the scene draws them (C.1).</summary>
	private const int BorderPriority = 2;
	private const int SeaPriority = 3;

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
		MeshInstance3D sea = Instance($"Sea{technique}", _seaMesh, material, layer);
		sea.Visible = _surf.Visible;
		_seas.Add(sea);
		AddChild(sea);
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
