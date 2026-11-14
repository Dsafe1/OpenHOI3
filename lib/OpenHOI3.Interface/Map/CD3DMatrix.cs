using System;
using System.Numerics;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The D3DX matrices the original hands its map's effects, row-major as D3DX keeps them and as HLSL's
/// <c>mul(row vector, M)</c> reads them - <c>D3DXMatrixTranslation</c>, <c>D3DXMatrixLookAtLH</c>,
/// <c>D3DXMatrixPerspectiveFovLH</c>, <c>D3DXMatrixOrthoLH</c> and the product - each sixteen floats, row after row. See
/// <c>docs/mechanics/mapdraw.md</c>, section 3.
/// </summary>
public static class CD3DMatrix
{
	public static float[] Identity() => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

	public static float[] Translation(float x, float y, float z) => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, x, y, z, 1 };

	/// <summary>A left-handed view from the eye towards a point, up the given way.</summary>
	public static float[] LookAtLH(Vector3 eye, Vector3 at, Vector3 up)
	{
		Vector3 z = Vector3.Normalize(at - eye);
		Vector3 x = Vector3.Normalize(Vector3.Cross(up, z));
		Vector3 y = Vector3.Cross(z, x);
		return new float[]
		{
			x.X, y.X, z.X, 0,
			x.Y, y.Y, z.Y, 0,
			x.Z, y.Z, z.Z, 0,
			-Vector3.Dot(x, eye), -Vector3.Dot(y, eye), -Vector3.Dot(z, eye), 1,
		};
	}

	/// <summary>A left-handed perspective: the field of view top to bottom, the width over the height, the near and far planes.</summary>
	public static float[] PerspectiveFovLH(float fieldOfView, float aspect, float near, float far)
	{
		float yScale = 1f / MathF.Tan(fieldOfView / 2f);
		float xScale = yScale / aspect;
		return new float[]
		{
			xScale, 0, 0, 0,
			0, yScale, 0, 0,
			0, 0, far / (far - near), 1,
			0, 0, -near * far / (far - near), 0,
		};
	}

	/// <summary>A left-handed orthographic projection, as <c>D3DXMatrixOrthoLH</c>: the width and height seen, the near and far planes.</summary>
	public static float[] OrthoLH(float width, float height, float near, float far)
	{
		return new float[]
		{
			2f / width, 0, 0, 0,
			0, 2f / height, 0, 0,
			0, 0, 1f / (far - near), 0,
			0, 0, near / (near - far), 1,
		};
	}

	/// <summary><c>a × b</c>, as <c>D3DXMatrixMultiply</c>: the row vector through a, then b.</summary>
	public static float[] Multiply(float[] a, float[] b)
	{
		var product = new float[16];
		for (int row = 0; row < 4; row++)
		{
			for (int column = 0; column < 4; column++)
			{
				float sum = 0;
				for (int k = 0; k < 4; k++)
				{
					sum += a[row * 4 + k] * b[k * 4 + column];
				}
				product[row * 4 + column] = sum;
			}
		}
		return product;
	}

	/// <summary>A row vector through a matrix, as HLSL's <c>mul(v, M)</c>.</summary>
	public static Vector4 Transform(Vector4 v, float[] m)
	{
		return new Vector4(
			v.X * m[0] + v.Y * m[4] + v.Z * m[8] + v.W * m[12],
			v.X * m[1] + v.Y * m[5] + v.Z * m[9] + v.W * m[13],
			v.X * m[2] + v.Y * m[6] + v.Z * m[10] + v.W * m[14],
			v.X * m[3] + v.Y * m[7] + v.Z * m[11] + v.W * m[15]);
	}
}
