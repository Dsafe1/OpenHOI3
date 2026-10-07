using Godot;
using OpenHOI3.Sim;

namespace OpenHOI3.Gfx;

/// <summary>
/// The seam between the engine-free libraries and Godot's own value types.
///
/// The libraries under <c>lib/</c> hold colours and vectors in types of their own so
/// that they run with no engine behind them. This is the one place those are turned
/// into what the engine draws with.
/// </summary>
public static class CEngineTypes
{
	public static Color ToGodot(this CColor color)
	{
		return new Color(color.R, color.G, color.B, color.A);
	}

	public static Vector2 ToGodot(this System.Numerics.Vector2 vector)
	{
		return new Vector2(vector.X, vector.Y);
	}
}
