using System;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The original's map camera, as far as it decides what is drawn. It hangs straight over
/// the map and looks down through a lens of a fixed angle, so how high it is and how many
/// pixels a map unit takes on screen are one figure told two ways. The original's rules
/// are in heights; the map here is drawn at a zoom. See
/// <c>docs/mechanics/mapunits.md</c>, section 3.
/// </summary>
public static class CMapCamera
{
	/// <summary>The angle the camera sees from the top of the screen to the bottom, in radians.</summary>
	public const float FieldOfView = 0.785f;

	/// <summary>How low the camera goes, and how high.</summary>
	public const float LowestHeight = 40f;
	public const float HighestHeight = 1000f;

	/// <summary>Where it is when a game starts.</summary>
	public const float StartHeight = 330f;

	/// <summary>
	/// The original's <c>counter_distance</c> setting as a new install has it: under it a
	/// unit is seen from near, at it and above from far. The player's own is in their settings.
	/// </summary>
	public const float CounterDistance = 100f;

	/// <summary>
	/// From this height up the map is seen from far: nothing is on it, no counter and
	/// no building.
	/// </summary>
	public const float FarViewHeight = 500f;

	/// <summary>How many map units fit between the top of the screen and the bottom, for each unit of height.</summary>
	private static readonly float SeenAUnitOfHeight = 2f * MathF.Tan(FieldOfView / 2f);

	/// <summary>The zoom - pixels of the screen to a map unit - that shows what the camera sees from a height.</summary>
	public static float ZoomAt(float height, float screenHeight)
	{
		return screenHeight / (SeenAUnitOfHeight * height);
	}

	/// <summary>The height the camera would be at to show the map at a zoom.</summary>
	public static float HeightAt(float zoom, float screenHeight)
	{
		return zoom <= 0f ? float.PositiveInfinity : screenHeight / (SeenAUnitOfHeight * zoom);
	}

	/// <summary>Whether the map is seen from far at a height: bare, with nothing on it.</summary>
	public static bool IsFarView(float height)
	{
		return height >= FarViewHeight;
	}

	/// <summary>
	/// Whether units are seen from near at a height: the near counter, not the far one - under the player's
	/// <c>counter_distance</c>, 0 to 500 by the game options' slider.
	/// </summary>
	public static bool IsNear(float height, float counterDistance = CounterDistance)
	{
		return height < counterDistance;
	}
}
