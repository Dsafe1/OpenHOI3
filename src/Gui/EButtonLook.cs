namespace OpenHOI3.Gui;

/// <summary>
/// The four ways the original draws a button, which are the four techniques of its
/// shader <c>gfx/FX/buttonstate.fx</c>: <c>up</c>, <c>over</c>, <c>down</c> and
/// <c>disable</c>. The numbers are what <see cref="CButtonShader"/> hands its shader.
/// </summary>
public enum EButtonLook
{
	Up = 0,
	Over = 1,
	Down = 2,
	Disabled = 3,
}
