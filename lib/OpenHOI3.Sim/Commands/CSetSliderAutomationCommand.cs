using OpenHOI3.Sim.Settings;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The production screen's slider automation set, 0 Manual Control to 3 Prioritize Reinforcement; a level above nought
/// sets the sliders by it at the next hour, where the original's arrow sets them at once. See
/// <c>docs/mechanics/frontend.md</c>, section 4.4; <c>docs/mechanics/ministers.md</c>, section 5.
/// </summary>
public sealed class CSetSliderAutomationCommand : CCommand
{
	private readonly int _level;

	public CSetSliderAutomationCommand(int level)
	{
		_level = level;
	}

	public override bool MayExecute(CGameState state)
	{
		return _level is >= 0 and < CPlayerAutomation.SliderLevels;
	}

	public override void Execute(CGameState state)
	{
		state.Automation.Sliders = _level;
		state.Automation.SlidersDue = _level > 0;
	}
}
