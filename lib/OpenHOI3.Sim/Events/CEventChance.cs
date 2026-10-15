using System;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Events;

/// <summary>
/// The two figures a mean time to happen gives, in single precision as the original works them:
/// an event's chance of firing in a day, and an option's weight for the AI. See
/// <c>docs/mechanics/events.md</c>, sections 3 and 5.
/// </summary>
public static class CEventChance
{
	/// <summary>
	/// The chance in a day: <c>1 - 0.5^(1 / days)</c>, days multiplied by each modifier whose
	/// conditions hold; certain where the days come to nothing.
	/// </summary>
	public static float OfDay(CGameState state, CMeanTimeToHappen mtth, CEventScope scope, CScriptReport? report = null)
	{
		float days = mtth.Days;
		foreach (CMeanTimeToHappenModifier modifier in mtth.Modifiers)
		{
			if (CConditions.Holds(state, modifier.Condition, scope, report))
			{
				days = (float)(days * ((float)modifier.Factor.Raw / 1000.0));
			}
		}
		if (!(days > 0f))
		{
			return 1f;
		}
		// The original goes by exp(log(0.5) / days), and exp(log()) of that again, rounding to
		// single precision at each step.
		float power = (float)(Math.Log(0.5) / days);
		float half = (float)Math.Exp(power);
		half = (float)Math.Exp((float)Math.Log(half));
		return (float)(1.0 - half);
	}

	/// <summary>
	/// The original's <c>CMeanTimeToHappen::GetDays</c>: the days in thousandths, multiplied in whole numbers by each
	/// modifier whose conditions hold, in order. What a rebel type scores in a province (<c>rebels.md</c>, sections 4
	/// and 9).
	/// </summary>
	public static long Days(CGameState state, CMeanTimeToHappen mtth, CEventScope scope, CScriptReport? report = null)
	{
		long days = mtth.Days * 1000L;
		foreach (CMeanTimeToHappenModifier modifier in mtth.Modifiers)
		{
			if (CConditions.Holds(state, modifier.Condition, scope, report))
			{
				days = days * modifier.Factor.Raw / 1000;
			}
		}
		return days;
	}

	/// <summary>
	/// An option's weight for the AI: its days in thousandths, multiplied in whole numbers by each
	/// modifier whose conditions hold, back to days, times its own factor.
	/// </summary>
	public static float Weight(CGameState state, CMeanTimeToHappen chance, CEventScope scope, CScriptReport? report = null)
	{
		long days = chance.Days * 1000L;
		foreach (CMeanTimeToHappenModifier modifier in chance.Modifiers)
		{
			if (CConditions.Holds(state, modifier.Condition, scope, report))
			{
				days = days * modifier.Factor.Raw / 1000;
			}
		}
		float factor = (float)(chance.AiFactor.Raw / 1000.0);
		return (float)((float)(days / 1000.0) * factor);
	}
}
