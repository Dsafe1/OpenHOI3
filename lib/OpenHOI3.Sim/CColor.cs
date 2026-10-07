namespace OpenHOI3.Sim;

/// <summary>
/// A colour as the game data carries one: four components from 0 to 1.
///
/// Our own type, and the name is not recovered from the binary. It exists so that the
/// model and the readers can hold a colour without referencing the engine; the client
/// converts at the point where one reaches the screen.
/// </summary>
public readonly record struct CColor(float R, float G, float B, float A = 1f)
{
	public static readonly CColor White = new(1f, 1f, 1f);
	public static readonly CColor Gray = new(0.5f, 0.5f, 0.5f);
}
