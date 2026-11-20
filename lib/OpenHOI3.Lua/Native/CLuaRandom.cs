using System;
using System.Text;
using OpenHOI3.Sim;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// <c>math.random</c> and <c>math.randomseed</c> of one state on a C runtime <c>rand()</c> of its own,
/// seeded 1 as a new thread's is. The original keeps a state on each thread that runs Lua, and Lua's two
/// functions are the C runtime's, whose generator is the thread's (<c>reversing/findings/FINDINGS-luahost.md</c>);
/// a state here is not tied to a thread, so it carries its generator with it, and what it draws does not
/// depend on which thread runs it. Lua 5.1.4's <c>lmathlib.c</c>, line for line.
/// </summary>
internal sealed class CLuaRandom
{
	private const int RandMax = 0x7FFF;

	private CRuntimeRand _rand = new(1);

	/// <summary>
	/// Where a draw is answered from instead - a value the original's <c>rand()</c> gave, or null to leave the draw this
	/// state's own: the measuring instrument of <c>docs/replay.md</c>, section 9. The state's own generator moves on either way.
	/// </summary>
	public Func<int?>? Feed { get; set; }

	/// <summary>Puts the two in the state's <c>math</c> table in place of the library's own.</summary>
	public void Install(CLuaState lua)
	{
		lua.SetGlobal("__ohoi_random", (CLuaFunction)Random);
		lua.SetGlobal("__ohoi_randomseed", (CLuaFunction)RandomSeed);
		lua.Run(Encoding.Latin1.GetBytes("math.random = __ohoi_random math.randomseed = __ohoi_randomseed " +
			"__ohoi_random = nil __ohoi_randomseed = nil"), "=OpenHOI3");
	}

	private int Random(CLuaCall call)
	{
		int drawn = _rand.Next();
		drawn = Feed?.Invoke() ?? drawn;
		// The remainder avoids r == 1.
		double r = X87((double)(drawn % RandMax) / RandMax);
		switch (call.ArgumentCount)
		{
			case 0:
				return call.Return(r);
			case 1:
			{
				int u = call.Integer(1);
				if (u < 1)
				{
					throw new CLuaError("bad argument #1 to 'random' (interval is empty)");
				}
				return call.Return(Math.Floor(X87(r * u)) + 1);
			}
			case 2:
			{
				int l = call.Integer(1);
				int u = call.Integer(2);
				if (l > u)
				{
					throw new CLuaError("bad argument #2 to 'random' (interval is empty)");
				}
				return call.Return(X87(Math.Floor(X87(r * unchecked(u - l + 1))) + l));
			}
			default:
				throw new CLuaError("wrong number of arguments");
		}
	}

	/// <summary>
	/// A result as the original's x87 left it - rounded to 24 bits of mantissa, to nearest, ties to even, the exponent
	/// kept - under the original's Lua 5.1.4, whose arithmetic is rounded so in <c>native/lua</c>'s <c>luaconf.h</c>;
	/// LuaJIT's is full double precision and so is this under it (<c>docs/mechanics/lua.md</c>, Ours).
	/// </summary>
	private static double X87(double x)
	{
		if (CLuaInterpreter.Chosen != ELuaInterpreter.Lua514)
		{
			return x;
		}
		ulong bits = BitConverter.DoubleToUInt64Bits(x);
		if (((bits >> 52) & 0x7FF) == 0x7FF)
		{
			return x;
		}
		bits += 0x0FFFFFFFUL + ((bits >> 29) & 1);
		bits &= ~0x1FFFFFFFUL;
		return BitConverter.UInt64BitsToDouble(bits);
	}

	private int RandomSeed(CLuaCall call)
	{
		_rand = new CRuntimeRand(unchecked((uint)call.Integer(1)));
		return 0;
	}
}
