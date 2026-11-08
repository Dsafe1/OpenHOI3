using System;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// The noise a beach is wobbled by: Perlin's reference noise, its tables drawn from the C runtime's <c>rand</c>, summed over five
/// octaves as Musgrave's fractal Brownian motion. Every step is the original's, in single precision stored after double arithmetic,
/// so that a beach comes out to the bit. The tables hang on how many times <c>rand</c> had been called before the first beach of the
/// process, which is no property of the map. See <c>docs/mechanics/mapcache.md</c>, section 8.
/// </summary>
public sealed class CBeachNoise
{
	/// <summary>
	/// *Ours*: the calls of <c>rand</c> before the tables are drawn. The original's count varies from one run to the next and has no
	/// right value for a map it has not built; 292 is the one that made Their Finest Hour's shipped cache, so that map comes out whole.
	/// </summary>
	public const int DefaultCallsBefore = 292;

	private const int TableSize = 256;
	private const int Mask = 255;
	private const float Offset = 4096f;
	private const int Octaves = 5;
	private const double Inverse = 0.00390625;

	private static readonly double H = (double)0.3f;
	private static readonly double Lacunarity = (double)0.4f;

	private readonly int[] _permutation = new int[TableSize * 2 + 2];
	private readonly float[,] _gradients = new float[TableSize * 2 + 2, 3];
	private readonly double[] _exponents = new double[Octaves + 1];

	public CBeachNoise(int callsBefore = DefaultCallsBefore)
	{
		uint state = 1;
		for (int i = 0; i < callsBefore; i++)
		{
			Next(ref state);
		}
		for (int i = 0; i < TableSize; i++)
		{
			_permutation[i] = i;
			// The one and two dimensional tables' draws, which nothing here reads.
			Next(ref state);
			Next(ref state);
			Next(ref state);
			for (int axis = 0; axis < 3; axis++)
			{
				_gradients[i, axis] = (float)((double)(float)((Next(ref state) & 511) - 256) * Inverse);
			}
		}
		for (int i = TableSize - 1; i > 0; i--)
		{
			int kept = _permutation[i];
			int j = Next(ref state) & Mask;
			_permutation[i] = _permutation[j];
			_permutation[j] = kept;
		}
		for (int i = 0; i < TableSize + 2; i++)
		{
			_permutation[TableSize + i] = _permutation[i];
			for (int axis = 0; axis < 3; axis++)
			{
				_gradients[TableSize + i, axis] = _gradients[i, axis];
			}
		}
		double frequency = 1.0;
		for (int i = 0; i <= Octaves; i++)
		{
			_exponents[i] = Math.Pow(frequency, -H);
			frequency *= Lacunarity;
		}
	}

	/// <summary>The C runtime's <c>rand</c>: fifteen bits of a linear congruence.</summary>
	public static int Next(ref uint state)
	{
		state = unchecked(state * 214013 + 2531011);
		return (int)(state >> 16 & 0x7FFF);
	}

	/// <summary>How far a point of the beach is moved: the noise's size, halved until it is at most a half.</summary>
	public float Displacement(float x, float y)
	{
		float value = (float)Fractal(x, y);
		if (value < 0f)
		{
			value = (float)((double)value * -1.0);
		}
		while (value > 0.5f)
		{
			value = (float)((double)value * 0.5);
		}
		return value;
	}

	private double Fractal(float x, float y)
	{
		float z = 0f;
		double value = 0.0;
		float lacunarity = (float)Lacunarity;
		for (int i = 0; i < Octaves; i++)
		{
			float noise = Noise(x, y, z);
			x = (float)((double)x * (double)lacunarity);
			y = (float)((double)y * (double)lacunarity);
			z = (float)((double)z * (double)lacunarity);
			value = (double)noise * _exponents[i] + value;
		}
		return value < -5.0 ? 0.0 : value;
	}

	private static void Setup(float v, out int b0, out int b1, out float r0, out float r1)
	{
		float t = (float)((double)v + (double)Offset);
		int whole = (int)t;
		b0 = whole & Mask;
		b1 = (b0 + 1) & Mask;
		r0 = (float)((double)t - (double)(float)whole);
		r1 = (float)((double)r0 - 1.0);
	}

	private static float Curve(float t)
	{
		double d = t;
		return (float)((3.0 - d * 2.0) * (d * d));
	}

	private static float Lerp(float t, float a, float b)
	{
		return (float)(((double)b - (double)a) * (double)t + (double)a);
	}

	private float At(int index, float x, float y, float z)
	{
		return (float)(((double)_gradients[index, 1] * (double)y + (double)_gradients[index, 0] * (double)x) + (double)_gradients[index, 2] * (double)z);
	}

	private float Noise(float x, float y, float z)
	{
		Setup(x, out int bx0, out int bx1, out float rx0, out float rx1);
		Setup(y, out int by0, out int by1, out float ry0, out float ry1);
		Setup(z, out int bz0, out int bz1, out float rz0, out float rz1);
		int i = _permutation[bx0];
		int j = _permutation[bx1];
		int b00 = _permutation[i + by0];
		int b10 = _permutation[j + by0];
		int b01 = _permutation[i + by1];
		int b11 = _permutation[j + by1];
		float t = Curve(rx0);
		float sy = Curve(ry0);
		float sz = Curve(rz0);
		float a = Lerp(t, At(b00 + bz0, rx0, ry0, rz0), At(b10 + bz0, rx1, ry0, rz0));
		float b = Lerp(t, At(b01 + bz0, rx0, ry1, rz0), At(b11 + bz0, rx1, ry1, rz0));
		float c = Lerp(sy, a, b);
		a = Lerp(t, At(b00 + bz1, rx0, ry0, rz1), At(b10 + bz1, rx1, ry0, rz1));
		b = Lerp(t, At(b01 + bz1, rx0, ry1, rz1), At(b11 + bz1, rx1, ry1, rz1));
		float d = Lerp(sy, a, b);
		return Lerp(sz, c, d);
	}
}
