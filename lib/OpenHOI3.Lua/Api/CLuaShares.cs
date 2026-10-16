using System;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The AI's shares as the original works them, in single precision: what the Lua constructors of
/// <c>CChangeInvestmentCommand</c> and <c>CChangeLeadershipCommand</c> make of the script's numbers, and
/// <c>CAI.FastNormalizeByPriority</c>. See <c>docs/mechanics/ministers.md</c>, sections 3 and 5.
/// </summary>
public static class CLuaShares
{
	/// <summary>What each share is given beyond what it asks, while room is left: the double of <c>(float)0.00005</c>.</summary>
	private static readonly double Extra = (float)0.00005;

	/// <summary>
	/// The order each priority level serves the six production shares in - lend-lease 0, consumer goods 1, production
	/// 2, supplies 3, reinforcement 4, upgrades 5. The engine's own table; no define reaches it.
	/// </summary>
	private static readonly int[][] Priorities =
	{
		new[] { 1, 0, 3, 4, 2, 5 },
		new[] { 0, 1, 3, 2, 4, 5 },
		new[] { 0, 1, 3, 5, 4, 2 },
		new[] { 0, 1, 3, 4, 2, 5 },
		new[] { 0, 4, 1, 3, 2, 5 },
	};

	/// <summary>
	/// The shares a command's Lua constructor makes of the script's numbers: each taken as a float, below nought or not a
	/// number made nought, over their sum - added up in double, then a float - and rounded half away from nought to 15
	/// bits. Nothing else is normalised, so they may sum a 32768th or two over the whole. A sum of nought gives nought
	/// each (ours: the original's 0/0 is the converter's indefinite, which no script makes).
	/// </summary>
	public static CFixed15[] Normalised(ReadOnlySpan<double> numbers)
	{
		var x = new float[numbers.Length];
		double added = 0;
		for (int i = 0; i < numbers.Length; i++)
		{
			float number = (float)numbers[i];
			x[i] = number > 0 ? number : 0;
			added += x[i];
		}
		float sum = (float)added;
		var shares = new CFixed15[numbers.Length];
		for (int i = 0; i < numbers.Length; i++)
		{
			float q = (float)(x[i] / (double)sum);
			if (float.IsNaN(q))
			{
				continue;
			}
			shares[i] = new CFixed15((long)(q * 32768.0 + (q < 0 ? -0.5 : 0.5)));
		}
		return shares;
	}

	/// <summary>
	/// <c>CAI.FastNormalizeByPriority(level, lendLease, consumer, production, supply, reinforce, upgrade)</c>: in the
	/// level's order, each share its figure and a little more, at most what is left of the whole - the first at most
	/// one - and what is left going down by it, never below nought. Answers the six and what is left.
	/// </summary>
	public static int FastNormalizeByPriority(CLuaCall call)
	{
		int level = call.Integer(1);
		if (level < 0 || level >= Priorities.Length)
		{
			throw new CLuaError($"bad argument #1 to 'FastNormalizeByPriority' (no priority level {level})");
		}
		var s = new float[6];
		for (int i = 0; i < s.Length; i++)
		{
			s[i] = (float)call.Number(i + 2);
		}
		float left = Normalise(s, Priorities[level]);
		return call.ReturnMany((double)s[0], (double)s[1], (double)s[2], (double)s[3], (double)s[4], (double)s[5],
			(double)Math.Max(left, 0f));
	}

	/// <summary>The walk itself, on the six in place; answers what is left.</summary>
	public static float Normalise(float[] s, int[] order)
	{
		int i = order[0];
		float v = (float)(s[i] + Extra);
		if (!(1.0f > v))
		{
			v = 1.0f;
		}
		float left = (float)(1.0 - v);
		if (!(left > 0))
		{
			left = 0;
		}
		s[i] = v;
		for (int k = 1; k < order.Length; k++)
		{
			i = order[k];
			v = (float)(s[i] + Extra);
			if (!((double)left > v))
			{
				v = left;
			}
			left = (float)((double)left - v);
			if (!(left > 0))
			{
				left = 0;
			}
			s[i] = v;
		}
		return left;
	}

	/// <summary>The order a level serves the shares in, for a check.</summary>
	public static int[] Order(int level) => Priorities[level];
}
