using System.IO;
using System.Linq;
using Godot;

namespace OpenHOI3.Debug;

/// <summary>
/// <c>--shadercheck=folder</c>: every <c>.gdshader</c> in a folder - what <c>OpenHOI3.SaveCheck shaders ... out=folder</c>
/// writes - given to the renderer to compile, its name printed before it so that each <c>SHADER ERROR</c> Godot prints
/// can be told whose it is, and the program ended. It needs a window: a headless run compiles nothing. See
/// <c>docs/mechanics/shaders.md</c>, section 5.
/// </summary>
public static class CShaderCompileCheck
{
	public const string Switch = "--shadercheck=";

	public static void Run(string folder, SceneTree tree)
	{
		string[] files = Directory.Exists(folder)
			? Directory.GetFiles(folder, "*.gdshader").OrderBy(f => f, System.StringComparer.OrdinalIgnoreCase).ToArray()
			: System.Array.Empty<string>();
		foreach (string file in files)
		{
			GD.Print("shadercheck: " + Path.GetFileName(file));
			var shader = new Shader { Code = File.ReadAllText(file) };
			// Asking for its parameters has the renderer compile it now.
			RenderingServer.GetShaderParameterList(shader.GetRid());
		}
		GD.Print($"shadercheck: {files.Length} shaders given to the renderer");
		tree.Quit();
	}
}
