using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CFixedPoint64</c>, the original's second kind of number - 64 bits with 15 binary places, a share of IC or of
/// leadership - as a script is handed one: <c>Get()</c> the value times 2^-15 rounded to a float, <c>GetTruncated()</c>
/// and <c>GetRounded()</c> both the value shifted down 15 places, toward minus infinity, as a 32-bit integer - the
/// original registers one function under both names. Its arithmetic is not built. See <c>docs/mechanics/lua.md</c>,
/// section 5.1.
/// </summary>
public static class CFixedPoint64Class
{
	public const string Name = "CFixedPoint64";

	/// <summary>2^-15, the double the original multiplies by.</summary>
	private const double Unit = 1.0 / CFixed15.Scale;

	public static CLuaClass Define()
	{
		return new CLuaClass(Name)
			.Method("Get", call => call.Return((double)Get(Raw(call))))
			.Method("GetTruncated", call => call.Return(Whole(Raw(call))))
			.Method("GetRounded", call => call.Return(Whole(Raw(call))));
	}

	/// <summary>What <c>Get()</c> answers: the value times 2^-15, worked in double and rounded to a float.</summary>
	public static float Get(long raw) => (float)(raw * Unit);

	/// <summary>What <c>GetTruncated()</c> and <c>GetRounded()</c> answer: the value shifted down 15 places, its low 32 bits.</summary>
	public static int Whole(long raw) => (int)(raw >> CFixed15.Places);

	private static long Raw(CLuaCall call) => call.Object<CFixed15>(1, Name).Raw;
}
