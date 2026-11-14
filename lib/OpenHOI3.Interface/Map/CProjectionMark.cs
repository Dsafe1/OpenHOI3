using System;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// One projection on the map - the mark a right click leaves on the province ordered - and how it plays out in real time,
/// paused or not, by its type's figures: it grows from a tenth of a unit to its size, pulses, turns, and after its duration
/// fades and is gone. Worked as the original works it, a float clock of seconds and its sums in doubles
/// (<c>docs/mechanics/movement.md</c>, section 7; <c>reversing/findings/FINDINGS-movement.md</c>, section 13.3).
/// </summary>
public sealed class CProjectionMark
{
	/// <summary>What a projection starts at before it grows [exe].</summary>
	public const float LeastScale = 0.1f;

	/// <summary>The original's π, 3.14 as a float [exe].</summary>
	public const float Pi = 3.14f;

	/// <param name="type">Its declaration.</param>
	/// <param name="province">The province it lies on.</param>
	/// <param name="onLand">Whether it is cut to the province's land, as on a land province; else a square of the sea.</param>
	/// <param name="x">Its middle's world x.</param>
	/// <param name="height">The height of the province's anchor it hangs on: 0 on land, 0.3 at sea.</param>
	/// <param name="z">Its middle's world z, north.</param>
	/// <param name="now">The clock, in seconds.</param>
	public CProjectionMark(CProjectionType type, int province, bool onLand, float x, float height, float z, float now)
	{
		Type = type;
		Province = province;
		OnLand = onLand;
		X = x;
		Height = height;
		Z = z;
		Start = now;
		Phase = now;
	}

	public CProjectionType Type { get; }

	public int Province { get; }

	public bool OnLand { get; }

	public float X { get; }

	public float Height { get; }

	public float Z { get; }

	/// <summary>When it was made.</summary>
	public float Start { get; }

	/// <summary>Where its pulse starts from: the clock while it grows, so the last frame of its growing.</summary>
	public float Phase { get; private set; }

	/// <summary>Whether it is over, to be taken off the map.</summary>
	public bool Done { get; private set; }

	/// <summary>
	/// The frame at this clock, or null once it is over - the frame it is found over drawing nothing.
	/// </summary>
	public CProjectionFrame? Step(float now)
	{
		if (Done)
		{
			return null;
		}
		CProjectionType type = Type;
		float elapsed = (float)((double)now - Start);
		float scale = type.Size;
		if ((double)type.Expanding + Start > now)
		{
			scale = (float)((double)(float)((double)elapsed / type.Expanding) * ((double)type.Size - LeastScale) + LeastScale);
			Phase = now;
		}
		float alpha = 1f;
		if (type.Duration > 0f && (double)elapsed > type.Duration)
		{
			if (type.Fadeout <= 0f || (double)type.Fadeout + type.Duration <= elapsed)
			{
				Done = true;
				return null;
			}
			// Fading pulses whether or not the type does.
			alpha = Pulse(now);
			float faded = (elapsed - type.Duration) / type.Fadeout;
			alpha = (float)(alpha - (double)alpha * faded);
		}
		else if (scale >= type.Size && type.Pulsating)
		{
			alpha = Pulse(now);
		}
		float angle = (float)((double)elapsed * Pi * type.Spin);
		return new CProjectionFrame(scale, alpha, angle);
	}

	/// <summary>The pulse's alpha: from pulseLowest to one, a sine of the time since <see cref="Phase"/> [exe].</summary>
	private float Pulse(float now)
	{
		float sine = MathF.Sin((float)(((double)now - Phase) * Pi * Type.PulseSpeed));
		float share = (float)((sine + 1.0) * 0.5);
		return (float)(Type.PulseLowest + (1.0 - Type.PulseLowest) * share);
	}
}
