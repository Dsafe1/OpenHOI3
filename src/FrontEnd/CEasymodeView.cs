namespace OpenHOI3.FrontEnd;

/// <summary>
/// The quickstart panel, offering the four major powers directly. Reached from the
/// main menu's toggle button, which each panel carries so the pair swap between
/// themselves.
/// </summary>
public sealed class CEasymodeView : CFrontEndView
{
	public override string WindowName => "easymode_panel";

	protected override void OnAttached()
	{
		Bind("toggle_quick_normal", Host.Show<CFrontEndState>);
		Bind("tutorial_button", Host.Show<CTutorialScreen>);
		Bind("exit_button", Host.Quit);

		// Each starts a 1936 campaign as that country, so they need the same scenario
		// loading as single player.
		foreach (string country in new[] { "usa", "ger", "sov", "jap" })
		{
			BindPlaceholder($"quickstart_{country}");
		}
	}
}
