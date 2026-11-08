namespace OpenHOI3.Sim.Plans;

/// <summary>A text sign of a battle plan. It has no colour: every one is drawn in the palette's first.</summary>
public sealed class CPlanText
{
	public string Text { get; set; } = "";

	public float X { get; set; }

	public float Y { get; set; }

	public float OriginX { get; set; }

	public float OriginY { get; set; }

	public float Size { get; set; } = 2f;

	public float Rotation { get; set; }

	public bool IsSelected { get; set; }
}
