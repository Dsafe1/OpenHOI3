using System;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The original's single-precision figures, as the Lua API hands them over: each step rounded to a
/// <c>float</c> where its code rounds one. See <c>docs/mechanics/lua.md</c>, sections 4 and 5.
/// </summary>
public static class CLuaFloats
{
	/// <summary>The half a thousandth added before a number is cut to thousandths: the float 0.0005, widened.</summary>
	private const double HalfThousandth = (double)0.0005f;

	/// <summary>Thousandths over 1000, the nearest float: <c>CFixedPoint:Get()</c>.</summary>
	public static float Thousandths(long raw) => (float)(raw / 1000.0);

	/// <summary>Thousandths made a float first and then divided, as the original's SSE code does it.</summary>
	public static float SingleThousandths(int raw) => (float)((float)raw / 1000.0);

	/// <summary>
	/// A Lua number as luabind turns it into a <c>CFixedPoint</c>'s thousandths: to a float, the half
	/// thousandth added and rounded, times 1000 and rounded, then down to a whole number.
	/// </summary>
	public static int ToThousandths(double number)
	{
		float value = (float)number;
		float raised = (float)(value + HalfThousandth);
		float scaled = (float)(raised * 1000.0);
		return (int)Math.Floor(scaled);
	}

	/// <summary>
	/// The distance between two places on the alignment triangle, thousandths, worked by way of
	/// single precision as the original does it (<c>docs/mechanics/lua.md</c>, section 4.3).
	/// </summary>
	public static int AlignmentDistance(long ax, long ay, long bx, long by)
	{
		int dx = (int)(ax - bx);
		int dy = (int)(ay - by);
		int squares = unchecked((int)((long)dx * dx / 1000) + (int)((long)dy * dy / 1000));
		float f = (float)((float)squares / 1000.0);
		float root = (float)Math.Sqrt(f);
		float raised = (float)(root + HalfThousandth);
		float scaled = (float)(raised * 1000.0);
		return (int)Math.Floor(scaled);
	}
}
