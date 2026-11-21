using System;
using System.Numerics;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// Where the mark of a right click lies and how its picture is laid on the map: on a province's land the picture is
/// projected from above onto that province's own triangles, so it is cut to the province; at sea onto a square 20 units a
/// side about the mark. The picture covers a square <see cref="CProjectionFrame.Scale"/> a side, north up, turned by the
/// frame's angle. See <c>docs/mechanics/movement.md</c>, section 7; <c>reversing/findings/FINDINGS-movement.md</c>,
/// section 13.4.
/// </summary>
public static class CProjectionMarks
{
	/// <summary>The height a sea zone's anchor stands at, a land province's standing at nought [exe].</summary>
	public const float SeaAnchorHeight = 0.3f;

	/// <summary>How far above the mark the picture is looked down at from, and the point looked at [exe].</summary>
	public const float EyeHeight = 50f;
	public const float AtHeight = 49f;

	/// <summary>The near and far planes of the look down [exe].</summary>
	public const float Near = 1f;
	public const float Far = 70f;

	/// <summary>Half the side of the square drawn at sea [exe].</summary>
	public const float SeaHalfSide = 10f;

	/// <summary>The sea's square as its four corners in turn, drawn as a fan of two triangles [exe].</summary>
	public static readonly Vector2[] SeaCorners =
	{
		new(-SeaHalfSide, -SeaHalfSide), new(-SeaHalfSide, SeaHalfSide), new(SeaHalfSide, SeaHalfSide), new(SeaHalfSide, -SeaHalfSide),
	};

	/// <summary>The fan's two triangles.</summary>
	public static readonly ushort[] SeaTriangles = { 0, 1, 2, 0, 2, 3 };

	/// <summary>
	/// What turns clip space into the picture's coordinates: u from x, v down from y [exe]. Its z column - the float's
	/// largest number and its least - is the original's; nothing samples the third coordinate it makes.
	/// </summary>
	public static readonly float[] Bias =
	{
		0.5f, 0, 0, 0,
		0, -0.5f, 0, 0,
		0, 0, float.MaxValue, 0,
		0.5f, 0.5f, 1.17549435E-38f, 1,
	};

	/// <summary>The height of the anchor a province's marks hang on.</summary>
	public static float AnchorHeight(bool sea) => sea ? SeaAnchorHeight : 0f;

	/// <summary>
	/// Where a unit's mark lies in a province - where the unit would stand in it, the original's <c>GetPosition</c>: a
	/// fleet in a land province at its naval base, an air unit at its own base there at the air base - or, aboard a
	/// carrier, the naval base - and anything else at the province's unit point. World x and z, z north.
	/// </summary>
	public static (float X, float Z) PointOf(CGameState state, CMap map, CUnit unit, int province)
	{
		bool sea = map.IsSea(province);
		if (unit.Kind == EUnitKind.Navy && !sea)
		{
			CMapPosition port = map.NavalBasePositions[province];
			return (port.X, port.Y);
		}
		if (unit.Kind == EUnitKind.Air && unit.Location == province && unit.Base == province)
		{
			if (unit.CarriedBy is null)
			{
				return CMapBuildings.PlaceOfRole(state, map.BuildingPositions, map.LabelPositions, province, CMovementRules.AirBase);
			}
			if (!sea)
			{
				CMapPosition port = map.NavalBasePositions[province];
				return (port.X, port.Y);
			}
		}
		CMapPosition point = map.Positions[province];
		return (point.X, point.Y);
	}

	/// <summary>
	/// The picture's coordinates for what is drawn under a world matrix: the world, then a look straight down from
	/// <see cref="EyeHeight"/> above the mark with up turned by the angle from north, then a square of the scale a side
	/// seen flat, then <see cref="Bias"/> [exe].
	/// </summary>
	public static float[] TextureMatrix(float[] world, float x, float height, float z, CProjectionFrame frame)
	{
		var eye = new Vector3(x, height + EyeHeight, z);
		var at = new Vector3(x, height + AtHeight, z);
		var up = new Vector3(MathF.Sin(frame.Angle), 0f, MathF.Cos(frame.Angle));
		float[] look = CD3DMatrix.Multiply(CD3DMatrix.LookAtLH(eye, at, up), CD3DMatrix.OrthoLH(frame.Scale, frame.Scale, Near, Far));
		return CD3DMatrix.Multiply(world, CD3DMatrix.Multiply(look, Bias));
	}
}
