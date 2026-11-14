using System;
using System.Numerics;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The click's marks: how a projection grows, pulses, fades and turns, and how its picture is laid on the map, against
/// <c>docs/mechanics/movement.md</c>, section 7.
/// </summary>
public class ProjectionMarkTests
{
	/// <summary>The install's legal_selection_projection.</summary>
	private static readonly CProjectionType Legal = new()
	{
		Name = CProjectionTypes.Legal, TextureFile = @"gfx\test\legal.tga", Size = 4f, Pulsating = true, PulseLowest = 0.8f,
		PulseSpeed = 1f, Expanding = 0.3f, Duration = 1f, Fadeout = 1f,
	};

	private static CProjectionMark Mark(CProjectionType type, float start) => new(type, 1, true, 100f, 0f, 200f, start);

	[Fact]
	public void TheLegalMarkGrowsPulsesFadesAndIsGoneAfterTwoSeconds()
	{
		const float start = 1000f;
		CProjectionMark mark = Mark(Legal, start);

		// Growing: from a tenth of a unit to 4 in 0.3 s, whole.
		CProjectionFrame first = mark.Step(start)!.Value;
		Assert.Equal(0.1f, first.Scale, 4);
		Assert.Equal(1f, first.Alpha);
		CProjectionFrame half = mark.Step(start + 0.15f)!.Value;
		Assert.Equal(2.05f, half.Scale, 2);
		Assert.Equal(1f, half.Alpha);
		CProjectionFrame grown = mark.Step(start + 0.29f)!.Value;
		Assert.True(grown.Scale < 4f);
		// Grown: the pulse starts from its last frame of growing, at the middle of its range, 0.9, and rises.
		CProjectionFrame pulsing = mark.Step(start + 0.31f)!.Value;
		Assert.Equal(4f, pulsing.Scale);
		float since = (start + 0.31f) - (start + 0.29f);
		Assert.Equal(0.8f + 0.2f * (MathF.Sin(3.14f * since) + 1f) / 2f, pulsing.Alpha, 4);
		Assert.InRange(pulsing.Alpha, 0.9f, 0.91f);
		// Half a second later the sine is at its top: 0.8 + 0.2 × (sin(3.14 × 0.5) + 1) / 2, nearly one.
		Assert.Equal(1f, mark.Step(start + 0.79f)!.Value.Alpha, 3);
		// Fading from a second: halfway through it, half the pulse's alpha.
		CProjectionFrame fading = mark.Step(start + 1.5f)!.Value;
		float pulse = 0.8f + 0.2f * (MathF.Sin(3.14f * (1.5f - 0.29f)) + 1f) / 2f;
		Assert.Equal(pulse * 0.5f, fading.Alpha, 2);
		Assert.Equal(4f, fading.Scale);
		Assert.Equal(0f, fading.Angle);
		Assert.False(mark.Done);
		// At two seconds it is over, and stays over.
		Assert.Null(mark.Step(start + 2f));
		Assert.True(mark.Done);
		Assert.Null(mark.Step(start + 1.5f));
	}

	[Fact]
	public void AMarkWithNoDurationStaysAndOneWithNoFadeoutGoesAtItsDurationsEnd()
	{
		var forever = new CProjectionType { Name = "forever", Size = 5f, Spin = 0.4f, Expanding = 0.7f };
		CProjectionMark mark = Mark(forever, 0f);
		CProjectionFrame late = mark.Step(500f)!.Value;
		Assert.Equal(5f, late.Scale);
		Assert.Equal(1f, late.Alpha);                                   // not pulsating
		Assert.Equal(500.0 * 3.14 * 0.4, late.Angle, 0);                 // 0.4 of 3.14 radians a second

		var brief = new CProjectionType { Name = "brief", Duration = 1f };
		CProjectionMark gone = Mark(brief, 0f);
		Assert.NotNull(gone.Step(1f));
		Assert.Null(gone.Step(1.01f));
	}

	[Fact]
	public void FadingPulsesEvenATypeThatDoesNot()
	{
		// The original's fade takes the pulse whatever pulsating says: here at its defaults, 0.4 and 3.
		var plain = new CProjectionType { Name = "plain", Duration = 1f, Fadeout = 2f };
		CProjectionMark mark = Mark(plain, 0f);
		Assert.Equal(1f, mark.Step(0.5f)!.Value.Alpha);
		float pulse = 0.4f + 0.6f * (MathF.Sin(3.14f * 3f * 1.5f) + 1f) / 2f;
		Assert.Equal(pulse * 0.75f, mark.Step(1.5f)!.Value.Alpha, 3);
	}

	[Fact]
	public void ThePictureCoversASquareOfItsScaleAboutTheMarkNorthUp()
	{
		float[] world = CD3DMatrix.Translation(90f, 0f, 190f);
		var frame = new CProjectionFrame(4f, 1f, 0f);
		float[] texture = CProjectionMarks.TextureMatrix(world, 100f, 0f, 200f, frame);

		// The effect's vertex at y 0.25 under the world, as project.fx places it.
		Vector2 Uv(float x, float z)
		{
			Vector4 v = CD3DMatrix.Transform(new Vector4(x - 90f, 0.25f, z - 190f, 1f), texture);
			return new Vector2(v.X, v.Y);
		}

		Assert.Equal(0.5f, Uv(100f, 200f).X, 4);                        // the middle in the middle
		Assert.Equal(0.5f, Uv(100f, 200f).Y, 4);
		Assert.Equal(1f, Uv(102f, 200f).X, 4);                          // half the scale east, its right edge
		Assert.Equal(0f, Uv(100f, 202f).Y, 4);                          // half the scale north, its top
		Assert.Equal(1f, Uv(100f, 198f).Y, 4);

		// Turned a quarter, its top is to the east.
		float[] turned = CProjectionMarks.TextureMatrix(world, 100f, 0f, 200f, new CProjectionFrame(4f, 1f, MathF.PI / 2f));
		Vector4 east = CD3DMatrix.Transform(new Vector4(12f, 0.25f, 10f, 1f), turned);
		Assert.Equal(0.5f, east.X, 3);
		Assert.Equal(0f, east.Y, 3);
	}
}
