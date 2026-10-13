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
		// The remainder avoids r == 1.
		double r = (double)(_rand.Next() % RandMax) / RandMax;
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
				return call.Return(Math.Floor(r * u) + 1);
			}
			case 2:
			{
				int l = call.Integer(1);
				int u = call.Integer(2);
				if (l > u)
				{
					throw new CLuaError("bad argument #2 to 'random' (interval is empty)");
				}
				return call.Return(Math.Floor(r * unchecked(u - l + 1)) + l);
			}
			default:
				throw new CLuaError("wrong number of arguments");
		}
	}

	private int RandomSeed(CLuaCall call)
	{
		_rand = new CRuntimeRand(unchecked((uint)call.Integer(1)));
		return 0;
	}
}
