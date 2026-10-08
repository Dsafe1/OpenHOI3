namespace OpenHOI3.Content.Gui;

/// <summary>An <c>iconType</c>: a sprite, drawn where it is put and at the size of its texture.</summary>
public sealed class CGuiIconType : CGuiType
{
	/// <summary>The sprite's name in the <c>.gfx</c> files.</summary>
	public string Sprite { get; init; } = "";

	/// <summary>Which frame of a sprite sheet to start on, counted from one. Zero when the file names none.</summary>
	public int Frame { get; init; }

	/// <summary>Times the texture's own size. One when the file names none.</summary>
	public float Scale { get; init; } = 1f;

	/// <summary>In radians.</summary>
	public float Rotation { get; init; }
}
