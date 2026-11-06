using OpenHOI3.Sim.Settings;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// One of the player's areas handed to the AI or taken back, as the country screens' arrows and the production screen's
/// automatic-deployment box set it: an area's flag to 0 or 1, or the Technology setting to 0 to 3 with its flag set to
/// whether it is above nought, as the technology screen's arrows keep it; then each played country's ministers made or
/// dropped by it. The original writes the player's record directly and not by a command; here every change to the game is
/// one. See <c>docs/mechanics/frontend.md</c>, section 4.4.
/// </summary>
public sealed class CSetAutomationCommand : CCommand
{
	private readonly EAutomation _area;
	private readonly int _value;

	public CSetAutomationCommand(EAutomation area, int value)
	{
		_area = area;
		_value = value;
	}

	public override bool MayExecute(CGameState state)
	{
		return _area == EAutomation.Technology
			? _value is >= 0 and < CPlayerAutomation.TechnologySettings
			: _area != EAutomation.Armies && _value is 0 or 1;
	}

	public override void Execute(CGameState state)
	{
		CPlayerAutomation automation = state.Automation;
		if (_area == EAutomation.Technology)
		{
			automation.Technology = _value;
			automation.Flags[(int)EAutomation.Technology] = _value != 0 ? 1 : 0;
		}
		else
		{
			automation.Flags[(int)_area] = _value;
		}
		// Production handed over puts the slider automation back to Manual Control, so that the screen never names a
		// priority the AI is not following - ours, the original keeping the level (compatibility.md, entry 19).
		if (_area == EAutomation.Production && _value == 1)
		{
			automation.Sliders = 0;
			automation.SlidersDue = false;
		}
		CAutomation.SetMinisters(state);
	}
}
