namespace OpenHOI3.Interface.Screens;

/// <summary>
/// How far a press of a scrollbar's end button moves it, and how often holding the button
/// presses it again, as the original's scrollbar works them out. See
/// <c>docs/mechanics/interface.md</c>, section 2.
/// </summary>
public static class CScrollbarSteps
{
	/// <summary>
	/// The time held that each repeat takes off: half a second times 0.3, the one a float
	/// and the other a double, multiplied in double as the original does.
	/// </summary>
	public const double RepeatSeconds = 0.5 * 0.30000001192092896;

	/// <summary>What Shift multiplies a step by, in thousandths: 10 written as the original floors it.</summary>
	private const long ShiftFactor = 10000;

	/// <summary>What Ctrl multiplies a step by, in thousandths: 100 written as the original floors it.</summary>
	private const long CtrlFactor = 100000;

	/// <summary>
	/// A press's step for a declared step size, in thousandths: a step of exactly a
	/// thousandth counts as ten of them, and any other as itself.
	/// </summary>
	public static long PressStep(long declared)
	{
		return declared == 1 ? declared * 10000 / 1000 : declared;
	}

	/// <summary>
	/// The step of one press: ten times as long with Shift down, a hundred times with Ctrl, and
	/// a thousand with both - Shift's asked first, as the original asks them.
	/// </summary>
	public static long Step(long pressStep, bool ctrl, bool shift)
	{
		long step = pressStep;
		if (shift)
		{
			step = ShiftFactor * step / 1000;
		}
		if (ctrl)
		{
			step = CtrlFactor * step / 1000;
		}
		return step;
	}

	/// <summary>
	/// How many presses a frame of holding makes, the button having been held that long.
	/// The original counts the whole time held down in repeats each frame, starting again
	/// from it the next, so the longer the button is held the more presses each frame makes.
	/// </summary>
	public static int Repeats(double secondsHeld)
	{
		float held = (float)secondsHeld;
		int repeats = 0;
		while (RepeatSeconds < held)
		{
			held = (float)(held - RepeatSeconds);
			repeats++;
		}
		return repeats;
	}
}
