namespace OpenHOI3.FrontEnd;

/// <summary>
/// The options panel reached from the main menu. The front end's copy is a stub in
/// the interface files - a single back button - because the real controls live in
/// settings.gui, which the original loads through CSettingsScreen.
/// </summary>
public sealed class CFrontendSettingsScreen : CFrontEndView
{
	public override string WindowName => "settings";

	protected override void OnAttached()
	{
		BindBack("back_button");
	}
}
