namespace OpenHOI3.Sim.Plans;

/// <summary>An icon of a battle plan: where it stands about its origin, in map pixels, its colour and its turn in radians.</summary>
public sealed class CPlanIcon
{
	public string Type { get; set; } = "";

	public int Colour { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public float OriginX { get; set; }

	public float OriginY { get; set; }

	public float Rotation { get; set; }

	public bool IsSelected { get; set; }
}
